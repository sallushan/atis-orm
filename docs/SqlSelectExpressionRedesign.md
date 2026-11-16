# SqlSelectExpression redesign

Status: DRAFT for review. Nothing here is built yet.
Written: 2026-11-16.

## 1. Why

`SqlSelectExpression` is about 1,100 lines and does five jobs at once:

1. It holds the query state (sources, clauses, select list).
2. It holds the query shape (the map from what the user wrote to SQL columns).
3. It decides when the query must be wrapped in a sub-query.
4. It does the wrapping and rewrites expressions after it.
5. It manages joins, navigation joins, CTEs and child collections.

It also exposes far more than the outside world needs. The recent bug shows the cost.
`Where(...).Select(p => new { p.EmployeeId, Latest = sub.FirstOrDefault() })` made the
projection add an OUTER APPLY. The apply is a join. The join wrapped the query because
a Where was already there. The wrap did not rewrite the projection, so `p.EmployeeId`
kept the alias of the inner query.

The rule "when you wrap, rewrite everything you are holding" is not written in one place.
Each code path has to remember it. This document puts it in one place.

## 2. What the outside world does today (inventory)

Only the engine project uses `SqlSelectExpression` directly. `Atis.Orm`,
`Atis.Orm.SqlServer` and the benchmarks never touch it. Nothing inherits from it.

| Group | Members | Callers |
|---|---|---|
| Clauses | `ApplyWhere`, `ApplyHaving`, `ApplyGroupBy`, `ApplyOrderBy`, `ApplyTop`, `ApplyDistinct`, `ApplyPaging`, `ApplyRowOffset`, `ApplyRowsPerPage`, `ApplyWhereMultipleFields` | one converter per LINQ method, the factory, `ChildQueryExtractor` |
| Projection | `ApplyProjection` | 11 call sites |
| Joins | `AddJoin`, `AddNavigationJoin`, `AddDataSourceWithJoinResolution`, `TryResolveNavigationDataSource`, `UpdateJoin`, `UpdateJoinCondition` | Join, StandardJoin, SelectMany, NavigationJoinCall, factory, `ChildQueryExtractor` |
| Wrap | `WrapIfRequired` | Join, NavigationJoinCall |
| Shape | `GetQueryShapeForFieldMapping` (16), `GetQueryShapeForDataSourceMapping` (7), `UpdateModelBinding` (8), `SwitchBindingToLastDataSource` | almost every converter |
| Special | `ConvertToRecursiveQuery`, `CreateCopy`, `RemoveGrouping`, `Tag` setter | RecursiveUnion, LinqToSqlBase, SubQueryNavigation |
| Reading state | `DataSources`, `SelectList`, `CteDataSources`, the clause collections, `Top`, `IsDistinct`, `RowOffset`, `RowsPerPage`, `Tag` | the factory (copy to derived table), Insert, DataManipulationBase, GroupByKey, LinqToSqlBase |

Findings:

- Reading state back is done almost only by one method: the factory's
  `ConvertSelectQueryToDeriveTableInternal`. Insert's `ValidateDestination` is the other.
- `UpdateJoinType`, `UpdateJoinName` and every `Has*Applied` except `HasProjectionApplied`
  have no outside callers. The public setters on `Top`, `IsDistinct`, `RowOffset` and
  `RowsPerPage` are only written by the class itself. All `protected virtual` members are
  never overridden.
- Two hidden paths change the query from inside:
  `SqlQueryShapeFieldResolverExpression` keeps a reference to the query and can call
  `AddNavigationJoin` during member resolution. And the factory rebuilds a select query from
  a derived table by replaying public calls (`AddJoin`, `UpdateJoin`, `ApplyWhere`, ...).

## 3. Goals

1. A small public surface. Callers say what they want, not how it is done.
2. The wrap rule lives in one place. Misuse fails loudly, not silently with wrong SQL.
3. The query state is always valid, and the rules are written down and checked.
4. The 941 existing tests keep passing at every step.

Non-goals:

- Database limits. The ORM is not responsible for them (for example, "OFFSET needs an
  ORDER BY on SQL Server" is not a rule of this class).
- Changing the expression nodes. They stay immutable.
- A new expression-handle design (see section 9).

## 4. Responsibility

`SqlSelectExpression` is **the query under construction**. It owns the clauses, the data
sources and the query shape. It exposes operations named for the caller's intent.

It stays the only public type. Behind it, internal helper classes may hold the wrap rules,
the join bookkeeping and the state. Callers cannot see them, so they can change freely.

## 5. Target public surface

| Area | Public members |
|---|---|
| Clauses | `ApplyWhere`, `ApplyHaving`, `ApplyGroupBy`, `ApplyOrderBy`, `ApplyProjection`, `ApplyTop`, `ApplyDistinct`, `ApplyRowOffset`, `ApplyRowsPerPage`, `ApplyPaging` |
| Shape | `GetQueryShapeForFieldMapping`, `GetQueryShapeForDataSourceMapping`, `UpdateModelBinding` |
| Joins | `AddJoin`, `AddDataSourceWithJoinResolution`, `AddNavigationJoin`, `TryResolveNavigationDataSource`, `UpdateJoin` |
| Wrap | `WrapIfRequired(operation)` returning a `WrapResult` (section 6). `Wrap()` is NOT public; make it public later if something needs it. |
| Conversion | `ToDerivedTable(...)`, `FromDerivedTable(...)` (section 8) |
| Other | `CreateCopy`, `Tag`; `ConvertToRecursiveQuery`, `RemoveGrouping`, `SwitchBindingToLastDataSource` for now |

Becomes `internal` or `private`:

- the state collections (`DataSources`, `SelectList`, clause collections, `Top`, ...)
- `IsWrapRequired`, `WrapQuery`, `Initialize`, `ReplaceDataSourceAccessing`
- every `Has*Applied`; `UpdateJoinType`, `UpdateJoinName`
- the `protected virtual` members, and the public setters

Replacements for the two outside readers:

- Insert's `ValidateDestination` (reads about ten fields) becomes one question on the class,
  for example `IsUncomposedTable(out table)`.
- `DataManipulationBase`'s `DataSources.First().Alias` comes from the shape's alias.

## 6. Wrapping

### 6.1 Wrapping may be public

Wrapping never harms the query itself. It moves the current query into a sub-query. The
consumer should know why it asks. So `WrapIfRequired(operation)` stays public. An
unconditional `Wrap()` stays private until something needs it.

The harm is to expressions the caller already holds, because they point at the old aliases.
So a wrap returns a **`WrapResult`**. It has `Rewrite(expression)`, which turns expressions
built against the old query into expressions for the new outer query, for example
`a_1.Col1 + 53` into `a_2.F1 + 53`. The result captures what it needs at the moment of the
wrap, so the old rule "call this right after the wrap, nothing in between" goes away.

`WrapIfRequired(operation)` is for callers that know the operation they are about to do
(Join and NavigationJoinCall). The class decides if a wrap is needed. The caller does not
know the rules.

### 6.2 Wraps that are required

Some wraps are discovered only while applying something (a sub-query becoming an OUTER
APPLY, a navigation inside a lambda body, CTE extraction). For these, the class does it
itself.

Every operation that can wrap goes through **one internal funnel**.

A public operation (for example `ApplyProjection`) creates an **operation context**. The
context holds the list of expressions the whole operation is holding. The context is passed
explicitly into every internal step the operation calls (`AddJoinCore(context, ...)` and so
on). It is not ambient or static state.

```
Prepare(context, operation)   // wraps if required, then rewrites EVERYTHING in the context
```

Why the list belongs to the whole operation and not to each step: the 2026-11-16 bug
happened because `ApplyProjection` held the projection, but the join it called knew only its
own arguments, and the join did the wrapping. If each step rewrote only its own arguments,
the bug would just move one level down. With a shared context, the projection is registered
once and any wrap inside the operation rewrites it. The workaround added on 2026-11-16
(`GetOperationForProjectedApply`) is deleted when this lands.

**Multiple wraps in one operation.** The current rules should not need this, but the design
allows it. Rewrite **immediately after each wrap**, not once at the end. After wrap 1 the
expressions are valid for the current query. If wrap 2 happens, it rewrites them again. A
rewrite matches against the select list of the sub-query made by that particular wrap, so
saving the rewrites for the end would force replaying them in exact order. Immediate
rewriting avoids that. Step 1 adds a counter that logs any operation which wraps more than
once, so we learn from the test suite whether it ever happens.

### 6.3 Safety net: retired aliases

When the query wraps, its old data-source aliases stop existing. The query keeps a set of
these **retired aliases**. Every `Apply*` and join method checks its incoming expressions.
If one references a retired alias it throws at once:

> Expression was built before the query was wrapped. Call WrapIfRequired first and use
> WrapResult.Rewrite on expressions you are holding.

Aliases are GUIDs, so there are no false matches. A correlated reference to an outer query is
never in the retired set, so it stays legal. The cost is one visitor walk per incoming
expression. It is paid once per query shape, because translated queries are cached. This
check stays **on in production**, because it guards correctness.

This is what would have caught the 2026-11-16 bug on the first test run, with a clear message.

## 7. State validity

### 7.1 Rules

The query must always satisfy these. They are about structure only.

1. Every alias used in any clause, the select list or the query shape is defined by this
   query's data sources or belongs to an outer query. It is never a retired alias.
2. The projection is set once. After that, only operations that do not change the shape
   are allowed.
3. Clauses come in a legal order: WHERE before GROUP BY, HAVING needs a grouping, and so on.
   An operation that would break the order wraps first. (This is today's `IsWrapRequired`
   table, written as rules.)
4. A join condition uses only sources that exist at that point. An apply source may use the
   sources before it.
5. A child-collection marker must be in the last Select (rule already enforced).
6. CTE sources are extracted and re-attached consistently (rule already enforced).

### 7.2 Enforcement

- Cheap checks (rule 1 on incoming expressions, rule 2) are always on.
- `AssertValid()` checks all rules. It runs after every public operation **in the test
  project only**. Then the 941 existing tests check the query state after every step, not
  only the final SQL string. This is the safety net for the refactor.

## 8. Select query and derived table

`SqlSelectExpression` builds the `SqlDerivedTableExpression`. The derived table knows nothing
about the select query.

- A builder knows its product. The derived table is a plain data node used by visitors, the
  hash generator and the SQL renderer. It should not drag the builder in with it.
- It lets the state stay private. Today the factory reads about 15 properties for this copy.
- The reverse direction (`CreateUnwrappedSelectQueryFromDerivedTable`) stops replaying public
  calls (each could trigger a wrap). `FromDerivedTable` sets the state directly and remaps
  the aliases.

`ISqlExpressionFactory` is a public interface, so its `ConvertSelectQueryTo...` and
`CreateSelectQuery` methods stay as one-line forwards. Removing them is a separate decision.

The factory's `CreateCopyOfQueryShape` logic moves with the conversion.

## 9. Hidden entry points

`SqlQueryShapeFieldResolverExpression` holds the whole query only to call
`HasProjectionApplied` and `AddNavigationJoin`. It will hold a small internal interface with
those two members instead. The entry point stays, but it is narrow and visible.

## 10. Not now: expression handles

A design where consumers hold a stable reference ("column 1 of this query's output") that
becomes `a_1.Col1` or `a_2.F1` only when the query is finished would remove the rewrite
step entirely. It changes what every converter creates and what every visitor sees. After
sections 6 and 7 are done, the bug class should be closed, and this may not be needed.
Revisit only if rewrite bugs still appear.

## 11. Migration steps

Work on a separate git branch. Every step keeps all tests green and is committed alone.

1. **Scaffold.** Add `AssertValid()` and call it after every public operation in the test
   project. Record the baseline: any rule the current code already breaks. Also add a
   test-only counter that logs every operation that wraps more than once.
2. **WrapResult.** `WrapIfRequired` returns it. `ReplaceDataSourceAccessing` becomes its
   internals. The "right after the wrap" assertion is removed.
3. **Funnel.** Route `ApplyProjection`, `AddJoinedSource` and the other wrapping paths through
   `Prepare`. Delete the 2026-11-16 workaround.
4. **Retired aliases.** Track them and validate incoming expressions.
5. **Surface.** Make state internal. Replace Insert's validation and the DataManipulation
   alias lookup. Remove unused members and the virtuals.
6. **Conversion.** Move `ToDerivedTable` and `FromDerivedTable` into the select query. The
   factory forwards.
7. **Shape back-reference.** Replace it with the narrow interface.
8. **Split.** Move the wrap rules, the join bookkeeping and the state into internal classes
   only if the class is still hard to read after steps 1 to 7.

### Step 1 result (2026-11-16)

Done on branch `refactor/select-expression-invariants`. No behaviour change.

- Added `SqlSelectExpression.Validation.cs` (retired-alias set, `AssertValid`, operation depth,
  wrap counter) and `SqlSelectExpressionDiagnostics` (switch and log). The checks are off
  unless the test project turns them on, which it does through `InternalsVisibleTo`.
  Side effect: inside the test project, overrides of `protected internal` visitor methods must
  also be `protected internal`. Six test overrides were changed.
- Rules checked so far: R1 (retired alias), R2 (shape exists once a select list exists),
  R3 (FROM source first, joins after, unique aliases), R4 (no forward reference in a join
  condition). The clause-order rule (7.1 rule 3) is not checked yet. It is enforced by the
  `IsWrapRequired` table today, and checking it needs a decision per rule first.
- Baseline over all 941 tests: **0 violations and 0 operations that wrap more than once.**
- The first run reported 9 R1 entries in 3 GroupBy tests. They were false positives of the
  rule, not bugs. `g.First()` on a group puts a copy of the grouped source into a sub-query.
  The copy defines its own aliases, so using them inside it is legal. The rule now only
  counts **free** references (an alias used but not defined inside the expression). This
  matters for step 4: the incoming-expression check must use the same free-alias rule.
- Proof that the check works: with the 2026-11-16 workaround switched off, the projected-apply
  test fails immediately with "select column 'EmployeeId' points at an alias this query
  retired when it was wrapped".
- Tests run in `throw` mode by default. Environment variable `ATIS_SELECT_VALIDATION` can be
  `off`, `record` or `throw`. In `record` mode the log goes to `select-invariants.txt` next to
  the test assembly.

### Step 2 result (2026-11-16)

Done. No change to the SQL any test produces.

- New public type `WrapResult` (`Wrapped`, `Rewrite(expression)`, `RewriteAll(expressions)`,
  `WrapResult.NotWrapped`). It captures the old query's select list and the new outer data
  source at the moment of the wrap, so it can be used later. `RewriteAll` returns a new array
  and leaves its input alone (the old method changed the array in place).
- `WrapIfRequired(operation)` is public and returns a `WrapResult` (it used to return nothing).
  The two existing callers (Join, NavigationJoinCall) ignore it for now.
- `ReplaceDataSourceAccessing` and `ReplaceDataSourceAccessingSingle` are deleted. A private
  `CaptureWrap()` builds the result right after a wrap. Its "exactly one data source" check is
  now an internal sanity check, not a rule callers must follow. `WrapQuery()` returns the result.
- Found while doing this: `WrapQueryIfCteReferencesExist` is a second, hidden wrap path (it
  turns the query into a CTE query and re-points the expressions). It already rewrote right
  after its wrap, which is the behaviour section 6.2 asks for. The wrap counter now counts it
  too. Over all tests there are still 0 operations that wrap more than once.
- Tests: 5 new `WrapResultTests` (not wrapped, wrapped, rewrite inside a bigger expression,
  use after the query changed again, `RewriteAll` leaves its input alone). 946 tests pass.

### Step 3 result (2026-11-16)

Done. No change to the SQL any test produces.

- The 2026-11-16 workaround (`GetOperationForProjectedApply` and the early wrap in
  `ApplyProjection`) is deleted. There is no prediction any more.
- `ApplyProjection` puts the projection in a private `InFlight` holder (`this[i]`, `Set`,
  `Apply(WrapResult)`) and passes it down: `AddProjectedApplies` -> `AddDataSourceWithJoinResolutionCore`
  -> `AddJoinedSource` -> `WrapIfRequiredCore` (and the CTE wrap path). Every wrap inside that
  call chain re-points everything in the holder right after the wrap. This is section 6.2's
  funnel, implemented as an optional `InFlight` parameter instead of a separate `Prepare` method.
  The public `AddDataSourceWithJoinResolution` is now a one-line forward with `inFlight: null`.
- `AddProjectedApplies` finds each sub-query by its member path (re-pointing builds new nodes,
  so instances cannot be kept), handles each one once, and never searches what replaced it.
  This keeps the old behaviour and guarantees the loop ends.
- Proof that the shared holder is what protects the projection: with the holder not passed to
  the join, all four projected-apply tests fail on the retired-alias check right after
  `ApplyProjection`.
- Tests added to `ProjectedApplyAfterWhereTests`: two applies in one projection, an apply inside a
  nested `new { }`, and (ignored, see below) an `OrderBy` before the `Select`.
- Found, not fixed, not caused by this step: when the parent has an `OrderBy` and the apply wraps
  it, `WrapQuery` keeps the ORDER BY inside the derived table without TOP, which SQL Server
  rejects, and the ordering is lost. It fails the same way on the commit before this step.
  The test is `[Ignore]`d with that reason. It needs its own decision (ORDER BY should move to
  the outer query, or the wrap should be avoided).
- Results: solution builds, 948 pass, 1 ignored, wrap counter and self-check log still empty.

### Step 4 result (2026-11-16)

Done. No change to the SQL any test produces.

- New private `ThrowIfStale(operation, argument, expression)` in `SqlSelectExpression.Validation.cs`.
  It uses the same free-alias rule as the self-checks (an alias counts only if the expression uses
  it without defining it, so a copy of the grouped source inside a sub-query is fine). It is **always
  on** and only does work once the query has been wrapped (the retired set is empty before that).
  It runs at the start of an operation, before that operation's own wrap.
- It guards: `ApplyWhere`, `ApplyHaving`, `ApplyGroupBy`, `ApplyProjection`, `ApplyOrderBy`,
  `AddJoinedSource` (so `AddJoin`, `AddNavigationJoin` and `AddDataSourceWithJoinResolution`: source and
  join condition), `UpdateJoin`, `UpdateJoinCondition`, `UpdateModelBinding`, `ConvertToRecursiveQuery`.
- The message names the operation and the argument, lists the retired alias, and says what to do:
  call `WrapIfRequired` first, or use `WrapResult.Rewrite`.
- It is a safety net, not a gate: if the walk cannot handle some node type, the operation runs
  (and the failure is logged when the test-only self-checks are on).
- What it does NOT cover: mistakes made inside the class itself (for example the old `ApplyProjection`
  bug). The test-only `AssertValid` covers those. The two checks are complementary.
- Tests: 8 new `StaleExpressionGuardTests` (stale predicate, projection, order by, query shape are
  refused; a rewritten expression, an unrelated outer alias, and anything before the first wrap are
  accepted; the guard works with the self-checks off). 956 pass, 1 ignored. No false positive in the
  existing suite.
- Experiment (production mode, self-checks off): removing the early `WrapIfRequired(NavigationJoin)`
  from `NavigationJoinCallExpressionConverter` makes 6 tests fail loudly (one with the new message),
  so forgetting it cannot give silent wrong SQL. Removing the early `WrapIfRequired(Join)` from
  `JoinQueryMethodExpressionConverter` breaks **no** test: `AddJoinedSource` already wraps and
  re-points its own arguments, so that early call looks redundant for the covered cases. That is a
  candidate for step 5 (fewer calls from converters) and needs one more look at lambda-parameter
  mapping before it is removed.

### Step 5 result (2026-11-16)

Done. No change to the SQL any test produces. The solution builds and 956 tests pass (1 ignored).

Public surface of `SqlSelectExpression` now:

- **Hidden** (`internal`, with private setters): `DataSources`, `CteDataSources`, `SelectList`,
  `WhereClause`, `HavingClause`, `OrderByClause`, `Top`, `IsDistinct`, `RowOffset`, `RowsPerPage`,
  `AutoProjection`, `HasProjectionApplied`. The other `Has*Applied` and `SqlFactory` and `QueryShape`
  are `private`. `IsWrapRequired`, `WrapQuery`, `Initialize`, `OnAfterApply` and the expression
  overload of `WrapIfRequired` are `private` and no longer `virtual` or `protected`.
- **Deleted** (no callers): `UpdateJoinType`, `UpdateJoinName`.
- **Kept public on purpose:** `GroupByClause` (read-only; GroupByKey and the group-by copy path need
  the group key, which is real information about the query) and `Tag` (get and set).
- **Added** to replace direct reads of the state:
  - `FromDataSourceAlias` replaces `DataSources.First().Alias` (DataManipulation converters and
    `ChildQueryExtractor`, 3 places).
  - `TryGetUncomposedTable(out table, out alias)` replaces Insert's ten-field `ValidateDestination`.
- The factory still reads the hidden state directly (same assembly) until step 6 moves the conversion
  into `SqlSelectExpression`.
- Not done: removing the early `WrapIfRequired(Join)` call from `JoinQueryMethodExpressionConverter`.
  It looked redundant (removing it broke no test), but the apply and left-join branches map a lambda
  parameter before `AddJoin` runs, and no test covers those after a `Select`. It stays until a test
  covers that case.
- Breaking change for outside code: anything outside this assembly that read those properties, or
  overrode the old `protected virtual` members, no longer compiles. Nothing in this repository does.

Order matters. Steps 2 to 4 fix the bug class. Steps 5 to 8 are cleanup and can stop early.

## 12. Risks

- Step 1 may find that current code already breaks a rule. Each one is a real finding.
  Decide per case: fix the code, or correct the rule.
- Alias numbering or wrap points could change and break SQL-string tests with no real bug.
  Compare SQL before and after on the whole suite.
- The cache and null-folding behaviour are subtle. Do not touch the hash or cache key in this
  work.

## 13. Open questions

Decided on 2026-11-16:

- `Wrap()` (unconditional) is private. Make it public later only if something needs it.
- The `ISqlExpressionFactory` conversion methods stay as forwards, because the factory can be
  overridden by consumers.
- `ConvertToRecursiveQuery` stays on `SqlSelectExpression`. It is close to the `Apply*`
  operations. If, when we reach it, it can be merged into the operation that calls it
  (RecursiveUnion), do that. Not decided; look at it when we get there.

Still open: none.
