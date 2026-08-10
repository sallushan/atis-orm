# repack-legacy-atis-orm

Rebuilds `local-packages/Atis.ORM.Legacy.<version>.nupkg` from the untouched
`local-packages/atis.orm.<version>.nupkg` next to it.

```
dotnet run --project tools/repack-legacy-atis-orm
```

It takes an optional repo-root argument; without one it walks up from its own output directory until it
finds the folder containing `local-packages`, so it can be run from anywhere.

## What it does, and why

The benchmark suite runs the current engine and the previous-generation Atis.ORM 9.16.4 side by side in
one process. It cannot, unmodified: .NET compares assembly identity case-insensitively, so `Atis.ORM`
and `Atis.Orm` collide (`MSB3243`), one gets dropped from `bin\`, and the loser throws
`TypeLoadException` at run time. Renaming the legacy assembly is what makes both loadable at once.

The tool unzips the package, then:

| Step | Detail |
|---|---|
| Assembly identity | `Atis.ORM` → `Atis.ORM.Legacy`, via Mono.Cecil. Assembly name and module name only. |
| Strong name | Dropped (public key cleared, `StrongNameSigned` flag removed). The original signature covers the old identity and cannot be reproduced without the private key; .NET Core does not verify strong names. |
| File names | `lib/netstandard2.0/Atis.ORM.{dll,xml}` → `Atis.ORM.Legacy.{dll,xml}`, plus the `<assembly><name>` element inside the XML doc so IDEs still bind the docs to the assembly. |
| Nuspec | Package id → `Atis.ORM.Legacy`; description prefixed with why it exists. Dependencies are left alone — `Atis.StringExtensions` and `System.Data.SqlClient` are both on nuget.org. |
| OPC plumbing | `_rels/`, `package/`, `[Content_Types].xml` are dropped and regenerated on repack, so they don't keep describing the old package name. |

**No IL is modified.** Mono.Cecil rewrites only the metadata fields named above; every method body is
byte-identical to the shipped package, which is what makes the benchmark number honest. CLR namespaces
also stay `Atis.ORM.*` — only the assembly they live in is renamed.

## Notes

- Deliberately **not** in `atis-orm.sln`: it is build tooling, and a normal solution build should not
  depend on it or on its Mono.Cecil reference.
- Its output is committed (see [`local-packages/README.md`](../../local-packages/README.md)), so this
  only needs running when the vendored legacy package changes.
