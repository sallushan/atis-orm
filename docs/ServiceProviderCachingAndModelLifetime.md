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
_serviceProvider = _serviceScope.ServiceProvider;
```

There are two distinct layers here:

1. **The root `IServiceProvider`** — returned by `OrmServiceManager.Instance.GetOrAdd(_config)`
   and **cached**. This is where **singletons** (including `IOrmModel`) live.
2. **A scope** — created per `DataContext` instance via `CreateScope()`. This is where
   **scoped** services (e.g. `IQueryCompiler`, `IQueryTranslator`, `IDatabaseAdapter`) live.

So two `DataContext` instances may share the same *root* provider (and therefore the same
singletons) while still getting their own *scope* (and therefore their own scoped services).

---

## The cache key — what makes two `DataContext`s share a provider

`OrmServiceManager` derives from `ServiceManagerBase` in the framework-agnostic
`Atis.DependencyInjection` package. Its cache is a **process-wide `static` dictionary**, and
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

The key deliberately does **not** include:

- the **`DataContext` subclass type** — `Atis.DependencyInjection` is a generic library and
  knows nothing about `DataContext`;
- the **configuration instance identity** — a fresh `new DataContextConfiguration()` with the
  same extensions hashes to the *same* key;
- the **connection string** or any other instance-level value carried by an extension.

> The unit test `OrmServiceManager_SameLogicalConfig_ReturnsSameServiceProvider` asserts
> exactly this: two configs with **different connection strings** but the same type + extension
> set return the **same** cached `IServiceProvider`.

---

## Service lifetimes

Lifetimes are declared in `OrmServiceBuilder`. The important ones:

| Lifetime      | Examples                                                                 | Shared across…                          |
|---------------|--------------------------------------------------------------------------|-----------------------------------------|
| **Singleton** | `IOrmModel`, `IModel`, `IEntityMetadataBuilder`, `ISqlExpressionFactory`, `ILogger` | the **root provider** (all its scopes)  |
| **Scoped**    | `IQueryCompiler`, `IQueryTranslator`, `ILinqToSqlConverter`, `IDatabaseAdapter`, `IAsyncQueryProvider` | a single **scope**                      |
| **Transient** | `ILambdaParameterToDataSourceMapper`                                     | nothing — new instance per resolution   |

Because **`IOrmModel` is a Singleton**, there is exactly **one `IOrmModel` per root
`IServiceProvider`**, shared by every scope of that provider.

Putting it together:

> **`IOrmModel` is a singleton per root `IServiceProvider`, and there is one root provider per
> `(configuration type + extension types)` — shared process-wide. It is _not_ one per
> `DataContext` subclass.**

---

## Consequence: the model can be shared across different `DataContext`s

The model is built lazily and **exactly once per provider**. `DataContext.Model` calls
`IOrmModel.EnsureModelInitialized(...)`, which uses a double-checked `_modelCreated` flag and
runs the initializer (your `OnModelCreating`) only the first time:

```csharp
public void EnsureModelInitialized(Action modelInitializer)
{
    if (!_modelCreated)
        lock (_modelCreatedLock)
            if (!_modelCreated)
            {
                modelInitializer();   // runs OnModelCreating
                _modelCreated = true; // ...only once per OrmModel instance
            }
}
```

So if two **different** `DataContext` subclasses use the same configuration type and the same
extension set, they resolve the **same** singleton `IOrmModel`. Whichever one touches `.Model`
first runs **its** `OnModelCreating`; the other subclass reuses that already-built model and
**its own `OnModelCreating` never runs**.

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

- `IOrmModel` is **Singleton per root `IServiceProvider`**.
- Root providers are cached **process-wide**, keyed by **configuration type + extension types**
  (not by `DataContext` type, config instance, or connection string).
- `OnModelCreating` runs **once per provider**; contexts sharing a provider share one model.
- To isolate a context's model, use a **distinct `DataContextConfiguration` subclass** for it.
