namespace Atis.Orm.Abstractions
{
    /// <summary>
    ///     <para>
    ///         Initializes the lazy-loading navigation properties of a freshly materialized entity.
    ///     </para>
    ///     <para>
    ///         Phase 1 supports two property shapes: <c>IQueryable&lt;TRelated&gt;</c> (assigned a
    ///         composable, lazily-executed query) and <c>Func&lt;...&gt;</c> (assigned a delegate that
    ///         builds and runs the query on invocation). Plain single-reference and non-queryable
    ///         collection navigations are left untouched and will be handled by Include / eager loading
    ///         in a later phase.
    ///     </para>
    /// </summary>
    public interface INavigationInitializer
    {
        /// <summary>
        ///     Sets the lazy navigation properties on <paramref name="entity"/>. No-op when
        ///     <paramref name="entity"/> is <c>null</c> or its type is not a mapped entity (e.g. an
        ///     anonymous/DTO projection or a scalar value).
        /// </summary>
        /// <param name="entity">The materialized object whose navigations should be initialized.</param>
        void Initialize(object entity);
    }
}
