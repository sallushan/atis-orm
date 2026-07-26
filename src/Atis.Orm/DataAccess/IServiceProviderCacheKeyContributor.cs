namespace Atis.Orm.DataAccess
{
    /// <summary>
    ///     <para>
    ///         Implemented by an extension carrying an option that changes the <em>shape</em> of the service
    ///         graph rather than merely supplying a per-context value.
    ///     </para>
    ///     <para>
    ///         Most instance-level options — a connection string, a command timeout — are read per scope from
    ///         <see cref="IDataContextServices"/> and deliberately stay out of the root-provider cache key, so
    ///         contexts pointed at different databases can share one provider. That only works for services
    ///         resolved per scope. An option consumed by a <em>singleton</em> cannot be swapped per context:
    ///         the singleton is built once and, in this codebase, ends up captured by compiled queries in the
    ///         process-wide <see cref="Atis.Orm.Abstractions.ICompiledQueryCacheProvider"/> cache.
    ///     </para>
    ///     <para>
    ///         Such an option must instead split the cache, so that contexts configured differently get
    ///         genuinely different providers. Implement this interface to fold it into the key.
    ///         See docs/ServiceProviderCachingAndModelLifetime.md.
    ///     </para>
    /// </summary>
    public interface IServiceProviderCacheKeyContributor
    {
        /// <summary>
        ///     <para>
        ///         A value whose <see cref="object.GetHashCode"/> distinguishes configurations that must not
        ///         share a root <see cref="System.IServiceProvider"/>. Return <c>null</c> to contribute
        ///         nothing.
        ///     </para>
        ///     <para>
        ///         Keep it coarse. Every distinct value produces another cached provider, and with it another
        ///         model, another compiled-query cache, and another set of singletons.
        ///     </para>
        /// </summary>
        object GetServiceProviderCacheKey();
    }
}
