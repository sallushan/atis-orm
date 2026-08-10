# Atis.Orm.Benchmarks

End-to-end query benchmarks comparing **Atis ORM** against **Dapper**, **EF Core 8**,
**linq2db**, **its own previous generation (Atis.ORM 9.16.4)**, and hand-written ADO.NET, using
[BenchmarkDotNet](https://benchmarkdotnet.org/).

This project is a deliberate **mirror of Dapper's own benchmark suite**
([`benchmarks/Dapper.Tests.Performance`](https://github.com/DapperLib/Dapper/tree/main/benchmarks/Dapper.Tests.Performance)):
same `Posts` schema, same query, same job settings, same connection policy, same summary columns.
That is the point — it means the numbers here can be read against Dapper's published performance
table instead of only against each other.

## Scenarios

| Category | What it measures |
|---|---|
| `ByPk` | One full `Post` entity fetched by primary key: `select * from Posts where Id = @Id`. Ported straight from Dapper's suite. The wide 13-column row with a 2,000-character text payload makes this a materialization-heavy workload, which is where ORMs actually differ. |
| `TopN` | The 100 highest-paid active employees in a department above a salary threshold, projected into an `EmployeeDto` — `WHERE … ORDER BY Salary DESC` + `TOP (100)` + a 4-column projection. Not part of Dapper's suite; kept because it exercises Atis's ordering and projection translation, which a single-row fetch never touches. |

`TopN` carries two settings the `ByPk` scenario does not need, both there for the same reason —
its per-op cost is dominated by SQL Server, not by the client:

- **A covering index** (`IX_Bench_Employee_TopN` on `(DepartmentId, IsActive, Salary DESC)`
  including the projected columns). Without `Salary` as a key column the server sorts ~860 rows on
  every call. That sort is identical for all five contenders, costs ~600 µs, and varies wildly —
  it buried the tens-of-microseconds client-side differences the benchmark exists to measure.
  Adding it dropped the scenario from ~600–800 µs/op to ~110–270 µs/op and made the ranking
  reproducible across runs.
- **`[ProcessCount(5)]`**, raising `LaunchCount` from the config's 1. The residual variance here is
  *between* processes rather than within them: a single benchmark process that contends with SQL
  Server runs uniformly slow across its pilot, warmup and every iteration, which more iterations
  cannot average out but more launches can.

The two scenarios are separate baseline groups. Compare rows *within* a category, never across.

## What makes these numbers trustworthy

Four harness properties, all inherited from Dapper's suite. Changing any of them changes what is
being measured, so change them knowingly:

- **The connection is opened once in `[GlobalSetup]` and reused.** Otherwise connection
  acquisition dominates and every ORM converges on the cost of `SqlConnection.Open()`.
- **Everything is synchronous.** Async would add state-machine and `Task` allocations that swamp
  the differences being measured.
- **A real job**: `Job.ShortRun` with 2 warmups, 10 iterations, and an unroll factor of 500. Warmup
  is what puts every contender in its steady state — JIT-compiled, query plans cached, compiled
  queries cached.
- **The parameter varies.** `BenchmarkBase.Step()` advances the id on every call, cycling 1..5000,
  so each ORM's compiled-query cache and parameter rebinding are genuinely exercised rather than
  sitting on one perfectly warm plan.

### Connection policy per contender

Matching Dapper's suite exactly:

| Contender | Connection |
|---|---|
| Hand Coded, Dapper, Atis `First` | share the one connection opened in setup |
| EF Core, linq2db, Atis `First (Own Connection)` | own long-lived context built once in setup, acquiring a pooled connection per query |
| Atis (legacy 9.16.4) | same two modes, but over its **own** connection — see below |

Atis appears twice on purpose. `First` is comparable to Dapper and the hand-coded baseline;
`First (Own Connection)` is comparable to EF Core. Reporting both keeps the connection cost visible
instead of hiding it inside a single number.

The legacy contender is the one place the shared-connection rule cannot be honoured literally. Its
`SqlDataLibrary` constructs **System.Data.SqlClient** commands and assigns the connection to them, so
handing it the Microsoft.Data.SqlClient connection everyone else shares throws on the first query. It
instead gets its own System.Data.SqlClient connection to the same database, opened once in setup and
reused — the same *arrangement*, over a different ADO.NET provider. Treat provider-level differences as
part of the legacy number.

## The legacy Atis.ORM contender

`Atis (legacy 9.16.4)` runs the previous-generation engine on both scenarios so the rewrite can be read
against what it replaced, in the same table, under the same harness. Three things to know:

- **It is a repacked package.** `Atis.ORM` and this repo's `Atis.Orm` are the same assembly identity to
  the CLR (comparison is case-insensitive), so the two cannot load in one process. The benchmark
  references `Atis.ORM.Legacy`, which is the shipped package with only the assembly name changed — no
  IL is modified. See [`local-packages/README.md`](../../local-packages/README.md).
- **It needs its own entity types.** The legacy engine requires entities to derive from
  `Atis.ORM.Record`, and only skips the inherited `RecordState` property for `Record` subclasses.
  Putting that base class on the shared `Post`/`Employee` would leak `RecordState` into EF Core's and
  linq2db's mappings, so `LegacyPost` and `LegacyEmployee` mirror them column-for-column instead. Same
  table, same CLR types, same materialization work.
- **The query shape is equivalent, not identical in spelling.** The legacy API has no `First`, so the
  `ByPk` row is `Where(…).FirstOrDefault()` — which appends `Top(1)` and takes the first row, the same
  SQL the current engine's `First(predicate)` emits. `TopN` uses `OrderByDesc`/`Top` where the LINQ
  providers use `OrderByDescending`/`Take`. Both engines were verified to return identical rows and
  identical `TopN` ordering before these numbers were taken.

The headline structural difference the `ByPk` row prices: the legacy engine has **no compiled-query
cache**, so `QueryTranslator.Translate` runs on every single execution, where the current engine's
steady state is a cache hit plus parameter rebinding.

## Prerequisites

- A reachable SQL Server. By default it connects to `Server=.` with integrated security.
  Override with the `ATIS_BENCH_SQL` environment variable:

  ```powershell
  $env:ATIS_BENCH_SQL = "Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=True"
  ```

No NuGet setup is needed for the legacy contender: `Atis.ORM.Legacy` is vendored in
[`local-packages/`](../../local-packages/) and restored from the folder source registered in the repo's
`Nuget.config`. Restoring from *outside* the repo root will not see that source — build through the
solution or the project path so the repo's `Nuget.config` applies.

The harness creates and seeds a dedicated `AtisOrmBenchDb` database on first run: 5,001 `Posts`
rows (Dapper's own bootstrap script) plus 5,000 employees across 5 departments. It is separate from
the unit-test database, so it never interferes with functional tests. Seeding is idempotent and
only pays its cost once.

## Running

BenchmarkDotNet requires a **Release** build for meaningful numbers:

```powershell
dotnet run -c Release --project src/Atis.Orm.Benchmarks
```

Run one scenario:

```powershell
dotnet run -c Release --project src/Atis.Orm.Benchmarks -- --anyCategories ByPk
dotnet run -c Release --project src/Atis.Orm.Benchmarks -- --anyCategories TopN
```

Run one ORM:

```powershell
dotnet run -c Release --project src/Atis.Orm.Benchmarks -- --filter *Atis*
```

Note that filtering out the baseline class drops the `Ratio` column — ratios need the baseline in
the same run, so include `*HandCoded*` (or `*TopN*`) if you want them.

Results print to the console and are written to `BenchmarkDotNet.Artifacts/`.

A full run takes roughly 12–15 minutes: every benchmark is launched 3 times (5 for `TopN`) and each
launch does 2 warmup plus 10 measured iterations. Filter to one scenario or one ORM while iterating.

## Reading the results

All contenders are joined into one summary, ordered fastest to slowest, with an `ORM` column
naming the library and a `Categories` column naming the scenario. `Ratio` is against that
scenario's baseline — `Hand Coded / SqlCommand` for `ByPk`, `Raw ADO.NET` for `TopN` — so it reads
as "how much overhead this ORM adds over hand-written ADO.NET".

### What this suite can and cannot resolve

Every row includes a real SQL Server round trip, and on a development machine that round trip's
variance is large — measured relative standard deviation is routinely 10–20% of the mean. That sets
a hard floor on what the numbers can tell you. Measured over eight runs on one machine:

**Not resolvable.** The leading cluster — `Hand Coded`, `Dapper Query<T> (buffered)`,
`Dapper QueryFirstOrDefault<T>`, `LINQ to DB First (Compiled)` and `Atis First` — all land in a
~53–74 µs band and reorder freely between runs. The hand-coded baseline itself has placed anywhere
from 1st to 6th. **Do not read an ordering within that cluster as a result.** If two rows are within
~30% of each other, the honest conclusion is that this suite cannot tell them apart.

**Reproducible.** These held on every run:

- `EF Core First` / `First (No Tracking)` and `Dapper Query<T> (unbuffered)` are consistently slower
  than the leading cluster.
- `LINQ to DB First (Compiled)` is consistently faster than its uncompiled `First`.
- `Atis First` is consistently faster than `Atis First (Own Connection)` — i.e. pooled connection
  acquisition costs a measurable ~10–15 µs.
- Allocations are stable to the byte and are not subject to timing noise at all. `Allocated` is the
  most trustworthy column in the table: EF Core's 64 KB on `TopN` versus everyone else's ~19–29 KB
  is a real, repeatable result.

If you need to resolve the leading cluster, this harness is the wrong instrument — you would need to
take the database out of the measurement.

Two more things to keep in mind:

- **`ShortRun` is 10 iterations.** `StdDev` is often a meaningful fraction of `Mean`, so treat
  small gaps between adjacent rows as noise, and re-run before believing any single row. Dapper's
  suite has the same property. Adjacent rows swapping places between runs (Raw ADO.NET and Dapper
  routinely do) means they are indistinguishable, not that one won.
- **A local SQL Server round trip is tens of microseconds** and is included in every row. It sets a
  floor that compresses the apparent differences between ORMs — the hand-coded baseline is not
  close to zero.
- **Server-side work is the enemy of this measurement.** Any scenario whose query makes SQL Server
  do real work measures the server, not the ORM. If you add a scenario, index it so the server side
  is trivial — otherwise the numbers will move around and rank the contenders differently on every
  run. This is what the `TopN` covering index is for.

## Known Atis constraints exercised here

- **Top-level `ORDER BY` needs a projection.** A full-entity ordered result
  (`Employees.Where(...).OrderByDescending(...).ToList()`) generates invalid SQL
  (`Incorrect syntax near 'ORDER'`) in Atis's execution path, even though `TranslateToSql` produces
  valid SQL for the same expression. Adding a `.Select(new EmployeeDto { ... })` projection fixes
  it. All contenders in the `TopN` scenario use that projection, so the comparison stays fair.
  Projections must use **member-init** (`new T { X = ... }`); a plain constructor call
  (`new T(...)`) fails with "Members of the new expression are not set".
- **The default `ILogger` writes the whole translation trace to `Console.Out`** on every
  compiled-query cache miss. `SilentLoggerExtension` replaces it here — see the comments in that
  file for why that matters inside a benchmark specifically.

## Adding scenarios

Add a class per ORM under `Benchmarks/`, deriving from `BenchmarkBase`, with a `[Description]`
naming the ORM and a `[BenchmarkCategory]` naming the scenario (add the constant to `Scenarios`).
Call `Step()` first in every method, keep every ORM's variant returning the same shape, and give
exactly one method in the category `Baseline = true`.
