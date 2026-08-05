# Service Provider Caching & `IOrmModel` Lifetime

This document explains **how a `DataContext` obtains its services**, **why an `IOrmModel`
is shared across some `DataContext`s but not others**, and **how to isolate the model per
context** when you need it. The behavior is subtle, so understanding the cache key is the key
to avoiding surprises.

---

## How a `DataContext` gets its services

Each `DataContext` lazily builds its `IServiceProvider` the first time a service is needed:

```csharp
// DataContext.ServiceProvider (simplified)
OnConfiguring(_config);
_serviceScope = OrmServiceManager.Instance
    .GetOrAdd(_config)                       // cached ROOT provider (shared)
    .GetRequiredService<IServiceScopeFactory>()
    .CreateScope();                          // a SCOPE per DataContext instance
_serviceScope.ServiceProvider
    .GetRequiredService<IDataContextServices>()
    .Initialize(this, _config);              // seed the scope with THIS context's config
_serviceProvider = _serviceScope.ServiceProvider;
```

There are two distinct layers here:

1. **The root `IServiceProvider`** — returned by `OrmServiceManager.Instance.GetOrAdd(_config)`
   and **cached**. This is where **singletons** (including `IOrmModelSource`, and therefore the
   model itself) live.
2. **A scope** — created per `DataContext` instance via `CreateScope()`. This is where
   **scoped** services (e.g. `IQueryCompiler`, `IQueryTranslator`, `IDatabaseAdapter`) live.

So two `DataContext` instances may share the same *root* provider (and therefore the same
singletons) while still getting their own *scope* (and therefore their own scoped services).

---

## The cache key — what makes two `DataContext`s share a provider

`OrmServiceManager` derives from `ServiceManagerBase` in the framework-agnostic
`Atzonix.DependencyInjection` package. Its cache is a **process-wide `static` dictionary**, and
the key is computed (roughly) as:

```csharp
// ServiceManagerBase.GetKey (default implementation)
var hash = new HashCode();
hash.Add(config.GetType());                              // the configuration's concrete TYPE
foreach (var ext in config.Extensions.OrderBy(...))
    hash.Add(ext.GetType());                             // each extension's TYPE
return hash.ToHashCode();
```

That means the cache key depends on **only two things**:

- the **concrete type** of the configuration object (e.g. `DataContextConfiguration`), and
- the **set of extension types** registered on it (e.g. `SqlServerExtension`).

`OrmServiceManager` then overrides `GetKey` to additionally fold in anything the extensions declare
via `IServiceProviderCacheKeyContributor` (see "The exception" below).

The key deliberately does **not** include:

- the **`DataContext` subclass type** — `Atzonix.DependencyInjection` is a generic library and
  knows nothing about `DataContext`;
- the **configuration instance identity** — a fresh `new DataContextConfiguration()` with the
  same extensions hashes to the *same* key;
- the **connection string** or any other instance-level value carried by an extension, unless that
  extension explicitly contributes it.

> The unit test `OrmServiceManager_SameLogicalConfig_ReturnsSameServiceProvider` asserts
> exactly this: two configs with **different connection strings** but the same type + extension
> set return the **same** cached `IServiceProvider`.

---

## Instance-level options: the initialization pattern

Because the cache key ignores the connection string, a service registration must **never capture an
extension instance**. This registration is a bug:

```csharp
// WRONG — the delegate closes over the extension that built the provider FIRST.
public void AddServices(IServiceCollection services)
{
    var builder = new OrmServiceBuilder(services);
    builder.TryAdd<IDbCommunication>(sp => new SqlDbCommunication(_connectionString));
}
```

`AddServices` runs **once**, for the first configuration that misses the cache. Every later
`DataContext` with the same key reuses that provider — and therefore that captured connection string.
Two contexts pointed at different databases would both talk to the first one, silently.

The fix is the same one EF Core uses: keep the options on the extension instance, and hand the *live*
configuration to the scope. `IDataContextServices` is a **scoped** service that `DataContext`
initializes with its own configuration right after `CreateScope()`, before anything else resolves.
Registrations then read their options through it:

```csharp
// RIGHT — non-capturing; the options come from the scope being resolved into.
builder.TryAdd<IDbCommunication>(sp => CreateDbCommunication(sp));

private static IDbCommunication CreateDbCommunication(IServiceProvider sp)
{
    var options = sp.GetContextExtension<SqlServerExtension>();   // this context's extension
    return new SqlDbCommunication(options.ConnectionString, options.CommandTimeout);
}
```

Rules of thumb for extension authors:

- Put instance-level options (connection string, timeouts, an external `DbConnection`) on **public
  properties of the extension**, and read them with `sp.GetContextExtension<TExtension>()` inside the
  factory delegate.
- A factory delegate must not touch `this`. If it needs a helper, make it `static`.
- Only **scoped** and **transient** services may read `IDataContextServices`. A **singleton** that
  captured it would pin the first context's scope forever (a captive dependency).
- A scope created directly off the root provider is never initialized, so resolving an
  options-reading service from it throws rather than guessing.

---

## The exception: options a *singleton* depends on

The initialization pattern only works for services resolved **per scope**. A singleton is built once
per root provider, so it cannot pick up a different value per context — and in this codebase it is
worse than that: `SimpleCompiledQuery` captures the singleton `IDbParameterFactory`, and compiled
queries live in the process-wide singleton `ICompiledQueryCacheProvider`. A per-context value that
reached a singleton would leak across contexts *and* outlive them.

Such an option must **split the cache** instead. Implement `IServiceProviderCacheKeyContributor` on
the extension; `OrmServiceManager.GetKey` folds the contribution into the key, so configurations that
differ resolve to genuinely different providers (and therefore different models, singletons, and
compiled-query caches):

```csharp
public class SqlServerExtension : IServiceContextExtension, IServiceProviderCacheKeyContributor
{
    public DbProviderFactory ProviderFactory { get; }

    // The ADO.NET client reaches the singleton IDbParameterFactory, so it cannot be per-scope.
    public object GetServiceProviderCacheKey() => this.ProviderFactory;
}
```

Only then is it legal for `AddServices` to capture the value:

```csharp
var providerFactory = this.ProviderFactory;   // legal ONLY because it is in the cache key
builder.TryAdd<IDbParameterFactory>(sp => new SqlDbParameterFactory(
    sp.GetRequiredService<IDbParameterNameGenerator>(), providerFactory));
```

Use this sparingly — every distinct contribution costs another cached provider and another model. The
decision procedure is simply:

| Who consumes the option? | Mechanism |
|---|---|
| Scoped / transient services (connection string, timeout, external connection) | Read per scope from `IDataContextServices`. **Keep out of the key.** |
| A singleton (the ADO.NET client) | `IServiceProviderCacheKeyContributor`. **Put in the key.** |

---

## Service lifetimes

Lifetimes are declared in `OrmServiceBuilder`. The important ones:

| Lifetime      | Examples                                                                 | Shared across…                          |
|---------------|--------------------------------------------------------------------------|-----------------------------------------|
| **Singleton** | `IOrmModelSource`, `IEntityMetadataBuilder`, `ISqlExpressionFactory`, `ILogger` | the **root provider** (all its scopes)  |
| **Scoped**    | `IDataContextServices`, `IOrmModel`, `IModel`, `IQueryCompiler`, `IQueryTranslator`, `IExpressionPreprocessorProvider`, `ILinqToSqlConverter`, `IDatabaseAdapter`, `IDbCommunication`, `IAsyncQueryProvider`, `IEntityPersister` | a single **scope**                      |
| **Transient** | `ILambdaParameterToDataSourceMapper`                                     | nothing — new instance per resolution   |

`IOrmModel` is **scoped**, but the model it hands back is not: the singleton `IOrmModelSource` owns
one model and returns that same instance to every scope. So there is still exactly **one model per
root `IServiceProvider`** — the scoped registration is about *how you get it*, not how many there
are (see "Building the model" below).

Putting it together:

> **There is one model per root `IServiceProvider`, and one root provider per
> `(configuration type + extension types)` — shared process-wide. It is _not_ one per
> `DataContext` subclass.**

---

## Building the model: resolving it is what builds it

`OnModelCreating` runs when the model is **resolved**, not when some caller remembers to ask for it
first. The registrations are:

```csharp
this.TryAdd<IOrmModelSource, OrmModelSource>();                                  // Singleton
this.TryAdd<IOrmModel>(p => p.GetRequiredService<IDataContextServices>().Model);  // Scoped
this.TryAdd<IModel>(p => p.GetRequiredService<IOrmModel>());                      // Scoped
```

`IDataContextServices.Model` is lazy and calls `IOrmModelSource.GetModel(context)`, which runs that
context's `OnModelCreating` behind a double-checked flag and never again. This is the shape EF Core
uses — `IModelSource` singleton, `IModel` scoped and registered as
`p => p.GetService<IDbContextServices>().Model`.

The point is that **there is no way to obtain a model that skipped `OnModelCreating`**, because
obtaining one is what runs it. Before this, every entry point that could name an entity type had to
touch `DataContext.Model` before doing anything else; forgetting to would derive that entity's
mapping from annotations and cache it, losing the fluent configuration for the life of the process.

Two consequences worth knowing:

- **A scope no `DataContext` created cannot resolve the model.** It has no context to build it from,
  so `IOrmModel` throws there, exactly as the options-reading services do.
- **`OnModelCreating` must not ask for the model.** Creating a queryable or starting a write from
  inside it is a loop; it is reported as such rather than left to overflow the stack. Configure
  entities on the `ModelBuilder` you were handed.

So if two **different** `DataContext` subclasses use the same configuration type and the same
extension set, they resolve the **same** `IOrmModelSource` and therefore the same model. Whichever
one reaches for the model first runs **its** `OnModelCreating`; the other subclass reuses that
already-built model and **its own `OnModelCreating` never runs**.

If all your `DataContext`s are meant to share one model, this is exactly what you want. If they
are meant to have different models, read on.

---

## How to isolate the model per `DataContext`

To get a **separate provider and a separate `IOrmModel`** (so each context's `OnModelCreating`
runs), give each `DataContext` a **distinct configuration _type_**. A new *instance* of the
same configuration type is **not** enough — remember the key is based on the configuration
*type*, not the instance.

**Shared model (default) — both contexts use `DataContextConfiguration`:**

```csharp
public class SalesDataContext : DataContext
{
    protected override void OnConfiguring(DataContextConfiguration config)
        => config.UseSqlServer(connectionString);
}

public class HrDataContext : DataContext
{
    protected override void OnConfiguring(DataContextConfiguration config)
        => config.UseSqlServer(connectionString);
}
// Same config type + same extension type => SAME provider => SAME IOrmModel.
// Only the first context's OnModelCreating runs.
```

**Isolated models — each context uses its own configuration subclass:**

```csharp
public sealed class SalesDbConfig : DataContextConfiguration { }
public sealed class HrDbConfig    : DataContextConfiguration { }

public class SalesDataContext : DataContext
{
    public SalesDataContext() : base(new SalesDbConfig()) { }
    protected override void OnConfiguring(DataContextConfiguration config)
        => config.UseSqlServer(connectionString);
}

public class HrDataContext : DataContext
{
    public HrDataContext() : base(new HrDbConfig()) { }
    protected override void OnConfiguring(DataContextConfiguration config)
        => config.UseSqlServer(connectionString);
}
// Different config TYPES => different cache keys => separate providers
// => separate IOrmModel => each OnModelCreating runs.
```

---

## TL;DR

- The model is **one per root `IServiceProvider`**, owned by the singleton `IOrmModelSource`.
  `IOrmModel` itself is **scoped** and comes from `IDataContextServices.Model`, so resolving it is
  what runs `OnModelCreating`.
- Root providers are cached **process-wide**, keyed by **configuration type + extension types**
  (not by `DataContext` type, config instance, or connection string).
- `OnModelCreating` runs **once per provider**; contexts sharing a provider share one model.
- To isolate a context's model, use a **distinct `DataContextConfiguration` subclass** for it.
- Because the key ignores instance-level options, service registrations must read them from the
  scoped `IDataContextServices` (`sp.GetContextExtension<T>()`) instead of capturing an extension.
- An option a **singleton** depends on cannot work that way; contribute it to the key with
  `IServiceProviderCacheKeyContributor` instead.
