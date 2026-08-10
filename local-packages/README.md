# local-packages

A folder-based NuGet feed committed to the repo, registered as the `atis-local` package source in
[`Nuget.config`](../Nuget.config). It exists for one reason: the benchmark suite compares the current
engine against **Atis.ORM 9.16.4**, its previous generation, and that package was never published to
nuget.org. Vendoring it here means `dotnet restore` works for anyone who clones the repo, with no
private feed, no credentials, and no manual setup.

`*.nupkg` is ignored globally by `.gitignore`; this folder is re-included by an explicit negation so
these two files — and only these two — are tracked.

## Contents

| File | Tracked | Referenced by projects | Purpose |
|---|---|---|---|
| `atis.orm.9.16.4.nupkg` | yes | no | The original package, byte-for-byte as shipped. Kept as the provenance record and as the input to the repack below. |
| `Atis.ORM.Legacy.9.16.4.nupkg` | yes | **yes** | The same package with the assembly renamed. This is what the benchmark project references. |

## Why the repack exists

.NET compares assembly identity **case-insensitively**, so the legacy engine's `Atis.ORM` assembly and
this repo's `Atis.Orm` assembly are the *same assembly* as far as the runtime is concerned. Referencing
both directly produces:

```
MSB3243: No way to resolve conflict between
  "Atis.ORM, Version=9.15.4.0, Culture=neutral, PublicKeyToken=3d00b9517700738d"
  and "Atis.Orm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null".
Choosing "Atis.ORM, Version=9.15.4.0, …" arbitrarily.
```

The build still *succeeds* — which is the trap. Only one of the two DLLs is copied to `bin\`, and
whichever lost fails at run time with
`TypeLoadException: Could not load type 'Atis.Orm.DataContext' from assembly 'Atis.ORM, Version=9.15.4.0'`.

`Atis.ORM.Legacy.9.16.4.nupkg` resolves this by renaming the assembly to `Atis.ORM.Legacy`, which gives
it a distinct identity. What changes:

- the assembly name and module name;
- the strong-name signature, which is **dropped** — it was computed over the old identity and cannot be
  reproduced without the private key, and .NET Core does not verify strong names anyway;
- the `lib/netstandard2.0/*.dll` and `*.xml` file names, and the `<assembly><name>` inside the XML doc;
- the package id and description in the nuspec.

What does **not** change: any IL. Every method body is byte-identical to the shipped package, so the
benchmark measures the real 9.16.4 engine. CLR namespaces also stay `Atis.ORM.*`, so benchmark code
against this package is written exactly as it would be against the real one.

## Regenerating

`Atis.ORM.Legacy.9.16.4.nupkg` is a build artifact that happens to be committed. To rebuild it from the
original:

```
dotnet run --project tools/repack-legacy-atis-orm
```

The tool reads `atis.orm.*.nupkg` from this folder and rewrites the output in place. See
[`tools/repack-legacy-atis-orm/`](../tools/repack-legacy-atis-orm/) for what it does.

## Adding a newer legacy version

1. Copy the new `atis.orm.<version>.nupkg` into this folder.
2. Run the repack command above — it picks the highest version present.
3. Bump the `Atis.ORM.Legacy` version in
   [`src/Atis.Orm.Benchmarks/Atis.Orm.Benchmarks.csproj`](../src/Atis.Orm.Benchmarks/Atis.Orm.Benchmarks.csproj).
4. Delete the superseded pair if the old version is no longer benchmarked.

Note that NuGet caches restored packages by id + version in `packages_rep/`. When replacing a package
*without* bumping its version, clear `packages_rep/atis.orm.legacy/` first or the stale copy is reused.
