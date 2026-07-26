# Atis.Orm.Benchmarks

End-to-end query benchmarks comparing **Atis ORM** against **EF Core 8**, **Dapper**,
**linq2db**, and **raw ADO.NET**, using [BenchmarkDotNet](https://benchmarkdotnet.org/).

All contenders run the *same* logical query against the *same* seeded SQL Server tables and
project into the *same* `EmployeeDto` shape, so each result reflects the full cost of
**translate → execute → hydrate** as a real request would incur it (a fresh context/connection
per invocation).

The query is a realistic top-N list: *the 100 highest-paid active employees in a department
earning above a threshold* — `WHERE … ORDER BY Salary DESC` + `TOP (100)` + a 4-column projection.

## Prerequisites

- A reachable SQL Server. By default it connects to `Server=.` with integrated security.
  Override with the `ATIS_BENCH_SQL` environment variable:

  ```powershell
  $env:ATIS_BENCH_SQL = "Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=True"
  ```

The harness creates and seeds a dedicated `AtisOrmBenchDb` database on first run (5,000
employees across 5 departments via `SqlBulkCopy`). It is separate from the unit-test database,
so it never interferes with functional tests.

## Running

BenchmarkDotNet requires a **Release** build for meaningful numbers:

```powershell
dotnet run -c Release --project src/Atis.Orm.Benchmarks
```

Run a subset with the built-in filter:

```powershell
dotnet run -c Release --project src/Atis.Orm.Benchmarks -- --filter *QueryBenchmarks*
```

Results (mean/op, allocations, ranking) print to the console and are written to
`BenchmarkDotNet.Artifacts/`.

## Reading the results

`RawAdoNet` is the `[Baseline]` — the floor. The `Ratio` column shows how much overhead each
ORM adds over hand-written ADO.NET. Dapper is typically close to the floor; EF Core and linq2db
carry translation overhead; Atis sits alongside them as a translation engine.

## Known Atis constraints exercised here

Two Atis-specific behaviours shaped the benchmark; worth knowing if you extend it:

- **~~Legacy SQL provider.~~** *(resolved)* `Atis.Orm.SqlServer` now creates connections, commands and
  parameters through `DbProviderFactory`, and binds `Microsoft.Data.SqlClient` on its `net8.0` leg —
  the same client every other contender uses. The separate `BenchmarkDatabase.AtisConnectionString`
  (previously normalized with `System.Data.SqlClient`'s builder, because 4.9.0 rejected the spaced
  `Trust Server Certificate` keyword) is now just an alias for `ConnectionString`.
- **Top-level `ORDER BY` needs a projection.** A full-entity ordered result
  (`Employees.Where(...).OrderByDescending(...).ToListAsync()`) currently generates invalid SQL
  (`Incorrect syntax near 'ORDER'`) in Atis's execution path, even though `TranslateToSql` produces
  valid SQL for the same expression. Adding a `.Select(new EmployeeDto { ... })` projection fixes it.
  All five ORMs use that projection, so the comparison stays fair. Projections must use
  **member-init** (`new T { X = ... }`); a plain constructor call (`new T(...)`) fails with
  "Members of the new expression are not set".

## Adding scenarios

Add more `[Benchmark]` methods (grouped in a new class under `Benchmarks/`) for the query shapes
you care about — joins, aggregations, pagination, `IN`/parameterized filters, projections to DTOs.
Keep every ORM's variant returning the same shape so the comparison stays fair.
