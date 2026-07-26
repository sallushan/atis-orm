# Choosing the ADO.NET SQL Server client

`Atis.Orm.SqlServer` works with **both** SQL Server clients:

- `System.Data.SqlClient` — the legacy, deprecated provider
- `Microsoft.Data.SqlClient` — the actively maintained one

Nothing in the assembly binds either type directly. Connections, commands and parameters are all
created through `System.Data.Common.DbProviderFactory`, so the client is a configuration choice
rather than a compile-time fact.

---

## The default

`Atis.Orm` and `Atis.Orm.SqlServer` multi-target `netstandard2.0` and `net8.0`, and each leg carries a
working default so `UseSqlServer(connectionString)` needs no extra setup:

```csharp
protected override void OnConfiguring(DataContextConfiguration config)
    => config.UseSqlServer(connectionString);   // whichever client this TFM defaults to
```

The legs differ only because **`Microsoft.Data.SqlClient` 6.x ships no `netstandard2.0` asset** — it
targets `net462`, `net8.0` and `net9.0` only. (5.2.x was the last line with a `netstandard2.0` asset.)

---

## Which leg does my project get?

Verified by restoring a real consumer project on each target framework:

| Your target framework | Leg resolved | Default client | Async connection close |
|---|---|---|---|
| `net8.0`, `net9.0`, `net10.0` | `lib/net8.0` | Microsoft.Data.SqlClient 6.0.2 | real `CloseAsync`/`DisposeAsync` |
| `net6.0`, `net7.0`, `netcoreapp3.1` | `lib/netstandard2.0` | System.Data.SqlClient 4.9.0 | falls back to sync `Close()` |
| `net472`, `net48` (.NET Framework) | `lib/netstandard2.0` | System.Data.SqlClient 4.9.0 | falls back to sync `Close()` |
| `net461` – `net471` | `lib/netstandard2.0` | System.Data.SqlClient 4.9.0 | falls back to sync `Close()` |
| `netstandard2.0` libraries | `lib/netstandard2.0` | System.Data.SqlClient 4.9.0 | falls back to sync `Close()` |

Everything from `net461` upward resolves and works. Two caveats are worth knowing:

**`net6.0` / `net7.0` / `netcoreapp3.1` get the conservative leg.** NuGet will not hand a `net8.0`
asset to a `net7.0` or lower project, so these runtimes fall back to `netstandard2.0` even though they
could support more. In practice that means `CloseConnectionAsync` completes synchronously, and the
default client is the deprecated one. The client is easy to fix — see
[Overriding it](#overriding-it) — the async fallback is not. This was judged acceptable because all
three runtimes are past end of support (netcoreapp3.1 Dec 2022, net7.0 May 2024, net6.0 Nov 2024).

> ### Why not replace the `net8.0` leg with `netstandard2.1`?
>
> It looks tempting: `netstandard2.1` is compatible with everything from `netcoreapp3.0` through
> `net10.0`, so two legs would cover every runtime *and* light up the async paths — no third leg
> needed. It was evaluated and rejected for one reason:
>
> **`Microsoft.Data.SqlClient` 6.x cannot be referenced from a `netstandard2.1` project.** MDS 6.x
> ships `net462`, `net8.0` and `net9.0` only. NuGet does not fail cleanly on this — it silently falls
> back to the *.NET Framework* asset and the compile then breaks:
>
> ```
> warning NU1701: Package 'Microsoft.Data.SqlClient 6.0.2' was restored using
>                 '.NETFramework,Version=v4.6.1, ...' instead of 'netstandard2.1'
> error CS0234: The type or namespace name 'SqlClient' does not exist in 'System.Data'
> ```
>
> A `netstandard2.1` leg is therefore capped at MDS 5.2.2, which would cost `net8.0`+ consumers — the
> current LTS, and where most consumers are — their MDS 6.x default. Helping three end-of-life
> runtimes was not judged worth downgrading the supported one.
>
> If that trade ever looks right, note two mitigations that were verified: NuGet resolves MDS assets
> against the *consumer's* framework independently, so a `net9.0` app on a `netstandard2.1` leg still
> runs MDS's `lib/net8.0` build; and the version cap is only a default — a consumer adding their own
> `Microsoft.Data.SqlClient` 6.x reference resolves and builds cleanly, because this assembly only
> ever touches `SqlClientFactory.Instance`.
>
> The no-compromise option is three legs — `netstandard2.0` + `netstandard2.1` + `net8.0` — at the
> cost of a third build/pack leg and a second MDS version to keep patched.

**`net472` is the practical .NET Framework floor.** `net461` restores, and this is exactly the reach
the library has always had, but consuming a `netstandard2.0` package from .NET Framework below 4.7.2
is well known for needing binding redirects and `NETStandard.Library` shims. On 4.7.2+ that friction
largely disappears.

---

## Overriding it

Pass a `DbProviderFactory` to pin the client explicitly. This works on either leg — a
`netstandard2.0` consumer can opt into `Microsoft.Data.SqlClient`, and a `net8.0` consumer can stay on
the legacy provider — as long as the corresponding package is referenced by the application.

```csharp
config.UseSqlServer(connectionString, Microsoft.Data.SqlClient.SqlClientFactory.Instance);
config.UseSqlServer(connectionString, System.Data.SqlClient.SqlClientFactory.Instance);
```

This is the escape hatch for the older runtimes in the table above. A `net6.0` project that does not
want the deprecated client references `Microsoft.Data.SqlClient` 5.2.x itself (the last line with
`net6.0` / `netstandard2.1` / `netstandard2.0` assets) and pins it:

```csharp
// net6.0 project, with its own PackageReference to Microsoft.Data.SqlClient 5.2.2
config.UseSqlServer(connectionString, Microsoft.Data.SqlClient.SqlClientFactory.Instance);
```

Nothing in `Atis.Orm.SqlServer` binds a client type, so pinning one it was not built against is
supported — it only ever calls `DbProviderFactory.CreateConnection/CreateCommand/CreateParameter`.

## Supplying your own connection

When you hand the context a connection you own, the client is inferred from it, so commands and
parameters are built by the same provider. The context opens the connection when it needs to, and
never disposes it.

```csharp
var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
config.UseSqlServer(connection);                       // client inferred
config.UseSqlServer(connection, providerFactory: f);   // or state it outright
```

Inference tries `DbProviderFactories.GetFactory(connection)` first (available from `net8.0`), then
falls back to the convention every SQL Server client follows — a `SqlClientFactory.Instance` sitting
in the same namespace and assembly as the `SqlConnection`. If neither works it throws and tells you to
pass the factory.

---

## Why the client is not just another per-context option

The connection string is read **per scope** from `IDataContextServices`, so two contexts on different
databases happily share one cached service provider. The client cannot work that way.

`IDbParameterFactory` is a **singleton**, and compiled queries in the process-wide
`ICompiledQueryCacheProvider` capture it. Since a `Microsoft.Data.SqlClient.SqlCommand` refuses a
`System.Data.SqlClient.SqlParameter`, a shared provider handing out parameters from the wrong client
would fail at execution time.

So `SqlServerExtension` implements `IServiceProviderCacheKeyContributor` and contributes its
`DbProviderFactory` to the provider cache key: **configurations on different clients get different
root providers**, each with its own singletons and compiled-query cache. Configurations on the same
client still share one, because `SqlClientFactory.Instance` is itself a singleton. See
[ServiceProviderCachingAndModelLifetime.md](ServiceProviderCachingAndModelLifetime.md).

---

## Mixing clients in one process

Supported, with one caveat worth knowing: each distinct client means another cached root provider,
and therefore another `IOrmModel` whose `OnModelCreating` runs separately. That is usually what you
want when the two halves of an application genuinely talk to different databases; it is wasteful if
you only meant to use one client and configured the other by accident.
