# Child collections in a projection

Design notes from the working session of 2026-10-21 to 2026-10-23. The first version is on the branch
`child-collections` (commit `9998492`). The branch `child-collections-linq` has the same runtime plus the placement
rule in section 0, which closes two of the open items by refusing the forms that caused them.

A projection can select a sub-query as a list:

```csharp
db.Person.Select(p => new { p.Age, Shoes = p.NavShoes.ToList() })
```

The engine cannot return a list inside a row, so it splits the query in two (or more): one query for the
parent rows, and one query per child collection. The rows are put together in memory.

This document records how that works, what we tried, and why we chose the join design over `exists`.

## 0. Placement rule (`child-collections-linq` only)

A child collection is the `ToList()` or `ToArray()` of a sub-query inside a `Select`. It must be in the **last**
`Select` of the query. A terminal such as `First()` or `ToList()` may follow it. `Where`, `OrderBy`, `Join`, `Take`,
`First(predicate)` and another `Select` may not. A child collection inside another child collection is refused too.

`ChildCollectionPlacementValidator` checks this once per cache miss, from `QueryCompiler.Compile`, and throws a
`NotSupportedException` that names the operator that follows.

Why: the list is part of the shape of the result. Written before a `Where`, it reads as if the lists were loaded before
the filter. They are not, and the reader cannot tell. Fixing the position removes that doubt.

Only the `ToList()` / `ToArray()` marker is restricted. A plain sub-query in a projection (a join group, or one used by
`Any` / `Count`) stays inside the SQL and is not affected. That is why the wrap code described below stays in the
engine: `GroupJoin` needs it.

## 1. How the engine builds the query

Two kinds of queries meet here. It helps to keep them apart.

- **The main query.** A `SqlSelectExpression` is created when the engine reaches the root of the method chain
  (`db.Person`). Each method that follows (`Select`, `Where`, `OrderBy`, `Take`) changes that one object.
- **A query found inside an argument.** For example `p.NavShoes` inside a `Select`. It gets its own root and its own
  `SqlSelectExpression`. When a query converter (such as `Select` or `Where`) receives it from a child converter that
  is *not* a query converter, `TransformConvertedChild` freezes it into a `SqlDerivedTableExpression`
  (`LinqToSqlExpressionConverterBase.cs`).

For a member whose CLR type is a list, `CompositeMemberAssignmentConverterBase` (line 40) wraps that derived table in a
`SqlQueryableExpression`. So after `Select`, `Shoes` is a Queryable column whose inner query is correlated to the
outer one:

```sql
select a_1.AGE as Age,
       Queryable: { select a_2.Id, a_2.Age, a_2.Style, a_2.PersonId
                    from Shoes as a_2
                    where a_1.ID = a_2.PersonId } as Shoes
from dbo.Person as a_1
```

`a_1.ID` is the **link column**: the parent value the sub-query uses to find its rows.

### What a wrap does

With the placement rule a child collection is never wrapped, because nothing follows its `Select`. The wrap still
happens for plain sub-queries, and the extractor was first written against it, so it is described here.

`Where(x => x.Age > 5)` after that `Select` filters on a projected column, so the engine wraps the query
(`SqlSelectExpression.WrapQuery`). In this order:

1. Add the link column to the select list if it is not there (`a_1.ID as SubQueryCol1`). This needs the Queryable
   column to still be in the list, because that is how the engine finds which columns it uses.
2. Take the Queryable column out and keep it aside.
3. Freeze what is left into a derived table and reset the main query so that it reads from that table.
4. Rewrite the kept-aside Queryable: `a_1.ID` becomes `a_3.SubQueryCol1`.
5. Add the Queryable back to the new query shape.

Then the predicate is rewritten to the new table and added as the `where`. `OrderBy` and `Take` that follow do **not**
wrap again. The query handed to the extractor is:

```sql
select top (3) a_3.Age, a_3.SubQueryCol1, Queryable: { ... where a_3.SubQueryCol1 = a_2.PersonId } as Shoes
from ( select a_1.AGE as Age, a_1.ID as SubQueryCol1 from dbo.Person as a_1 ) as a_3
where a_3.Age > 5
order by a_3.Age asc
```

Where `OrderBy` and `Take` sit depends on how the LINQ is written:

| LINQ | Result |
|---|---|
| `OrderBy.Take.Select` | `TOP` and `ORDER BY` are inside a derived table. The `Select` is the outer query. |
| `Select.OrderBy.Take` | One flat query. `ORDER BY` uses the alias (`order by Age`). **Refused by the placement rule.** |
| `OrderBy.Select` | Flat. `ORDER BY` uses the full column (`a_1.AGE`). |
| `Select.Where.OrderBy.Take` | `Where` forces the wrap, so `TOP` is on a query that reads from a derived table. **Refused by the placement rule.** |

## 2. Splitting the query

`ChildQueryExtractor.Extract` turns the final query into:

- the **parent query**: the final query without the Queryable columns, plus a hidden first column `ChildKeyN` for each
  link column (first, because a sequential reader must see the key before the object is built);
- one **child query** per Queryable column.

### The first logical trap: `TOP` and the link must not share a level

A tempting child query is:

```sql
select ... from Shoes a_2
where exists (select top 3 ... from (<parent>) a_3
              where a_3.Age > 5 and a_2.PersonId = a_3.SubQueryCol1
              order by Age)
```

This is wrong. SQL runs `where`, then `order by`, then `top`. The link is in the same `where`, so only the one linked
parent is left when `TOP` runs, and it always passes. Shoes of persons outside the real top 3 come back.

The fix is to run the whole parent query, `TOP` included, as a derived table, and apply the link **outside** it:

```sql
exists (select 1
        from (select top 3 ... order by Age) as outerQuery
        where outerQuery.SubQueryCol1 = a_2.PersonId)
```

### The second trap: the parent query runs twice

The parent rows come from one statement. The child query repeats the parent query inside itself, so the same text runs
again. If `ORDER BY Age` has ties, `TOP 3` may pick a different tied row each time. Then a parent gets an empty list
and a child set is returned for a parent that was not selected. There is no error. Wrapping does not help, because it
does not reduce the number of executions.

`MakeRowLimitsDeterministic` handles it. For every query in the `FROM` chain that has `Top` or `Offset`:

- if the query reads one table with a primary key, the key is added to the `ORDER BY` (or becomes it);
- if it cannot be fixed and has no `ORDER BY`, it throws `NotSupportedException`;
- if it cannot be fixed but has an `ORDER BY`, it is left as the developer wrote it (see the open items).

Other ways to avoid the second execution were considered and not taken:

- **Send the parent's keys back as values** (`where PersonId in (@k1, ...)`). No tie problem and no race, but SQL
  Server accepts about 2100 parameters, long lists bloat the plan cache, and non-equality links would need tuples.
- **Put the parent rows in a temp table** and join to it. Deterministic, but SQL Server specific and needs a batch
  model the engine does not have.

## 3. Join or exists

Both give a correct set of child rows. The decision is about telling **which parent a child row belongs to**.

### The join design (chosen)

```sql
select NavShoes.Id, NavShoes.Age, NavShoes.Style, NavShoes.PersonId,   -- Item
       keys.ChildKey1 as Key1                                          -- Key
from ( select distinct p.ChildKey1 as ChildKey1
       from ( <parent query, TOP included> ) as p ) as keys
inner join Shoes as NavShoes on (keys.ChildKey1 = NavShoes.PersonId)
```

`BuildChildQuery` does not invent the join condition. It rewrites the sub-query so that its parent references point at
the `keys` table, then asks the engine to join it. `JoinPredicateExtractor` lifts every comparison that mentions the
keys table (`=`, `<>`, `<`, `<=`, `>`, `>=`) out of the sub-query's `where` and into the `ON` clause. If the
sub-query cannot be lifted (it has `group by`, `distinct`, `top`, a parent reference outside the top-level `where`, or
an `or` involving the parent) it falls back to `cross apply`.

### The exists design (not chosen)

```sql
select ... from Shoes a_2
where exists (select 1 from (<parent query, TOP included>) outerQuery
              where outerQuery.SubQueryCol1 = a_2.PersonId)
```

This is simpler to build: no keys table, no `distinct`, no join resolution, no `cross apply` fallback, the whole `where`
can move inside without splitting it, and a one-column child never needs a query shape. It is correct for the *set* of
child rows.

### Why join still won

The result of `exists` does not say which parent owns a row. To attach rows in memory you must do one of two things.

1. **Match on child columns.** Add the child's link columns to its output and look them up. This is correct only when
   every link condition is an equality. Example: `c.Region == p.Region && c.Amount > p.Threshold`, with parents
   (X, 10) and (X, 100) and one child (X, 50). `exists` keeps the child because the first parent passes. A match on
   `Region` alone gives it to both parents, which is wrong for the second. Adding columns to the child's `select` also
   changes the meaning of a child that uses `distinct` or `group by`.
2. **Evaluate the condition in memory.** This needs the original LINQ as a delegate, and SQL and C# disagree on
   nulls, text collation, trailing spaces, numeric precision and dates. A dictionary built from the child's value would
   miss rows the database matched.

The join design avoids both. The keys that come back are the **parent's own values**, taken from the `keys` table, so
each parent looks itself up with exactly the value it holds. The database decides which child rows match each key,
for any condition that fits in `ON` (and `cross apply` for the rest). The child's own `select` is left alone. The key
set is the parent columns the sub-query uses, not necessarily an FK.

### If an equality-only fast path is wanted later

A restricted in-memory design is sound if it is kept strict:

1. Take the conditions inside the `exists` that mention the parent.
2. Each must be a binary comparison between a child-side and a parent-side expression, joined by `and`.
3. Anything else that mentions the parent (`or`, a function, a boolean member) throws `NotSupportedException`.
4. Conditions that involve only child columns need no handling.
5. Project each child-side expression into the child output.
6. In memory, check every comparison for each parent/child pair, using a dictionary on the equality ones to find
   candidates.
7. Restrict the link types to ones where SQL and C# agree (`int`, `long`, `Guid`, maybe `string` with a fixed comparer).
   Treat `null` as "no match".

## 4. A child that selects less than the whole row

`NavShoes.Select(s => s.Style).ToList()` used to throw in `Extract`:
`QueryShape property type is 'SqlDataSourceColumnExpression' and is not a valid SqlQueryShapeExpression`.

Cause (confirmed by a test): the join step adds the child's link column (`PersonId` as `SubQueryCol1`) to the child's
select list so the `ON` clause can read it. `SqlDerivedTableExpression.CreateQueryShape` accepted a scalar select only
when it had exactly one column, so the extra column made it fall through to the throw.

Fix: a **helper column** flag.

- `SelectColumn.HelperColumn` marks a column added only so something else can reach a value.
  It is set on `SubQueryColN` (from `WrapQuery` and from `JoinPredicateExtractor`), on `ChildKeyN`, and on the
  row-number column of `ApplyJoinRewriter`.
- `SqlSelectListExpression.FindScalarColumn` is the one rule: a select list is scalar when exactly one non-helper
  column exists and that column is scalar. `IsScalar` and `ScalarSelectColumn` expose it.
- It is used by `SqlDerivedTableExpression.CreateQueryShape`, `SqlStandaloneSelectExpression.CreateQueryShape` and
  `SqlSelectExpression.ShouldSubQueryBeProjectedAsJoin`.
- Every place that copies a `SelectColumn` carries the flag (3 in `SqlExpressionVisitor`, `SqlExpressionFactory`,
  the select copy maker, `ApplyJoinRewriter`). The flag is part of the expression hash.

A lost flag fails loudly for a scalar child (the old throw comes back), so the tests catch a missed copy site.

## 5. Materialization

`QueryExecutor.LoadChildCollections` runs every child query first and reads it completely. `ElementFactoryBuilder`
builds, for each child, a row factory: `new ChildRow(item, key)`. The key columns come from the child query's shape
(`Key1`, `Key2`, ...). `ChildCollections` groups the items by key. The parent factory reads its own key values first
(by ascending ordinal, because the reader may be sequential), then asks for the list and builds the object.

- A scalar child reads `Item` as one column. `BuildScalarReadExpression` falls back to ordinal 0 when it cannot find
  the column in the ordinal map. That is correct because `Item` is always the first column. It works by position.
- An anonymous-type child is built by constructor parameter name, so an extra helper column in the select list is never
  read.
- A child with no link columns gets `NoKey`, so every parent gets every row.
- A `NULL` in any part of a key matches no parent, as in SQL.

## 6. Tests

| File | What it covers |
|---|---|
| `ChildCollectionProjectionTests` | Translation of the marker; how the query is split; parent `Top` / filter / order kept in the child; the tie-breaker; scalar, computed, anonymous and unlinked children; `distinct` / `take` children with `cross apply`; helper columns carry the flag. No database. |
| `SelectListScalarRuleTests` | The scalar select-list rule with and without helper columns. |
| `ChildCollectionPlacementTests` | The placement rule on in-memory expression trees: allowed and refused spellings, no database. The execution tests add one run through `QueryCompiler`. |
| `ChildCollectionExecutionTests` | 11 executions against a local SQL Server: per-parent grouping and empty lists; class member and array; `Top` without `OrderBy`; cache-hit rebind; async; refusing a non-list member; single-column child; anonymous-type child; child with its own `Take`; single-column child under a parent with `OrderBy` and `Take`. |
| `ChildCollectionMaterializationTests` | 12 executions against a local SQL Server: two-column link; `NULL` link value (empty list, not null); unlinked child (`NoKey`); two child collections in one projection; parents sharing a link value; member types `List`, `IList`, `ICollection`, `IReadOnlyList`, `IEnumerable`; terminals `First`, `FirstOrDefault`, direct `foreach`, `ToArray`, `FirstOrDefaultAsync`; `NULL` inside a single-column child and an anonymous-type child; `NULL` in parent columns. |
| `ChildCollectionsHelperTests` | 19 plain tests, no database, of `ChildCollections.MakeKey`, `ReadKeyValues`, `Add`, `GetList` and `GetArray`. |

`Single()` is not supported by the engine at all (no converter), with or without child collections, so it is not tested.

### Not covered by an execution test

- Ties with a non-unique `ORDER BY`.
- The navigation initializer on child items.

## 7. Open items

1. **Closed by the placement rule.** **Tie-breaker gap (found with `Select.Where.OrderBy.Take`).** The `Where` after the `Select` wraps the query, so
   `TOP` sits on a query that reads from a derived table. `GetPrimaryKeyColumns` only accepts a query that reads a real
   table, so it finds no key, and because an `ORDER BY` exists the query is left alone: `order by a_2.Age asc` with
   ties. The key is available inside (`SubQueryCol1`), but the extractor does not look through the derived table.
   Without an `ORDER BY` the same query throws (from reading the code; not run).
2. **Closed by the placement rule.** **Redundant key.** When `ORDER BY` uses the alias spelling (`Select(Id, ...).OrderBy(Id).Take`), the key check
   compares an alias reference with a full column, so `a_1.ID` is appended a second time. Harmless but untidy.
3. **Non-comparison conditions.** `JoinPredicateExtractor` lifts only binary comparisons. A parent-referencing condition
   of another shape (for example a function call) may stay inside the sub-query and produce a join that refers to a
   sibling table. Not tested.
4. **Anonymous-type child.** The helper column shows up in the child's select list next to the item members. The
   materializer ignores it, but it is untidy.
5. **`ApplyJoinRewriter` line 111** turns the scalar flag off when it adds a row number. That rewrite skips scalar
   children, so the branch should not be reached. Not proven.
6. **Per-parent limits.** A child with its own `Take` loads with `cross apply`, which gives a per-parent limit. A plain
   join cannot.
7. The two materialization test gaps still listed in section 6 (ties, navigation initializer on child items).

## 8. Alternative considered: extract the children as LINQ

Recorded on 2026-10-23, after the placement rule (section 0) was settled. We looked at whether the children could be
pulled out of the **LINQ** expression and run as ordinary queries, instead of being split off the SQL tree afterwards.
We did not build it. This section says what the idea was and what stopped us.

### The idea

If a child collection can only be in the last `Select`, the query before it is flat. A preprocessor could then:

1. Find each `ToList()` / `ToArray()` marker in the last `Select`.
2. Replace it in the main query with a placeholder.
3. Build one more LINQ expression per child, roughly `parent.SelectMany(x => child(x), (x, c) => ...)`.
4. Send every expression through the normal conversion and the normal `Execute` path.

What we hoped to gain:

- **No special SQL surgery.** The engine's own `SelectMany` handling would build the join or apply.
- **Execution hooks.** A before / after hook could treat a child like any other query.
- **Less engine code.** The wrap and hoisting code for child lists could be deleted.

### The challenges

**1. The engine's hoisting cannot be deleted.** The code in `SqlSelectExpression.WrapQuery` that carries a
sub-query column through a wrap is older than child collections. A `GroupJoin` group (`join ... into g`) needs it, and
`g` is later used by `Count()` or `Any()`. We proved it: making that code throw broke 3 of the 801 engine tests
(`GroupJoin_sub_query_selected_in_projection`, `Standard_join_after_where_should_do_the_join_on_sub_query`,
`Queryable_selected_in_projection_then_query_wrapped_and_queryable_used_in_Where`). So only the
`ToList()` / `ToArray()` marker can be restricted. That is what section 0 does.

**2. A child row needs the key of its parent, and the key is hard to know in LINQ.** The child query returns rows like
"this shoe belongs to the parent with key K", and the main query must return the same K for every parent row. K is the
set of parent columns the child uses to link itself. In LINQ that is not visible:

- A navigation such as `x.Shoes` is only a member access. It becomes a join condition (`Shoes.PersonId = Person.Id`)
  inside the converter, from the entity metadata. A preprocessor would have to repeat that expansion.
- For a hand-written sub-query (`shoes.Where(s => s.PersonId == x.Id)`) the key could be found by scanning for members of
  `x`. That works only when `x` is used through members, not as a whole.
- The SQL tree already knows the answer: the engine appends the parent columns the sub-query uses to the select list
  (`SubQueryColN`). The existing extractor reads those. So the SQL level is the natural place to find the key.

**3. The main query would have to carry hidden key columns.** In LINQ that means a wrapper result shape, because a
projection cannot add invisible members to an anonymous type or a DTO. `ElementFactoryBuilder` would then need to read
the wrapper. Today it only has to add hidden columns to the select list.

**4. Matching by primary key does not always work.** The simple version, "key = primary key of the parent entity", fails
when the last `Select` runs over an anonymous type (for example after a join). Then there is no single entity to ask.

**5. Matching by position does not work.** Matching the i-th child group to the i-th parent row needs both queries to
return the parent rows in the same order. With ties in `ORDER BY`, or with `TOP` and no `ORDER BY`, they may not.

**6. The dictionary-row idea.** The engine can already return rows as `Dictionary<string, object>`. We considered
rewriting the last `Select` to also project key columns and reading everything as dictionaries. It would carry the
hidden columns without a wrapper type. But it does not say which columns to add (challenge 2), it needs a second
materializer to build the typed objects (anonymous types, DTOs, entities with their navigation setup), and every value is
boxed and looked up by name. We judged it more complex than the SQL-level step.

**7. The existing extractor already uses the engine's normal path.** `ChildQueryExtractor.BuildChildQuery` builds each
child through `AddDataSourceWithJoinResolution`, which is the same logic a hand-written `SelectMany` goes through. A LINQ
version would end in the same place, so the saving was small.

### What we kept

The SQL-level extractor, with the placement rule in front of it. The rule is what removes the confusing forms, and it
makes the parent flat, so the two open items that came from a wrapped parent can no longer occur.

### Execution hooks (not built)

A hook for validation, logging and timing was part of the motivation. Notes from the discussion:

- The hook should receive the **LINQ the developer wrote**, before preprocessing, at the single entry of
  `QueryExecutor`. After preprocessing it would see inlined macros and frozen values, and it would run only on a cache
  miss.
- With the SQL-level extractor a query that has child collections still gives **one** hook call. The children are run
  inside the executor through `dbAdapter` and never come back through the entry. A LINQ-level extraction would have given
  1 + N calls unless the child calls were suppressed or tagged.
- The consumer has to read the whole expression anyway, because a sub-query in a `Where` or an `Any` is also not a
  separate call. The hook's context could list the child collections (member name and source expression) so they need not
  be searched for.
- A fragment of a child as written (`x.Shoes`) is not a standalone query, so a separate call would not show "the query
  that is about to run". The real child statement exists only after translation.
- If per-child events are needed later, fire the hook again per child with a kind and a link to its parent call.
- "After" must fire when the work is really finished, which for the async paths means after the deferred child loading.

So the hook is not a reason to prefer a LINQ-level extraction. Both approaches share the same runtime.
