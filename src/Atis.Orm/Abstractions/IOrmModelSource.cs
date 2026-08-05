namespace Atis.Orm.Abstractions
{
    /// <summary>
    ///     <para>
    ///         Produces the finalized <see cref="IOrmModel"/> for a <see cref="DataContext"/>, running that
    ///         context's <c>OnModelCreating</c> the first time it is asked and never again.
    ///     </para>
    ///     <para>
    ///         This is the only place a model is built, which is what makes the guarantee structural: an
    ///         <see cref="IOrmModel"/> is not a stored service that callers happen to configure first, it
    ///         is produced here, already configured. A caller cannot obtain a model that has not seen
    ///         <c>OnModelCreating</c>, because obtaining one is what runs it.
    ///     </para>
    ///     <para>
    ///         Registered as a singleton, so there is one model per root <see cref="System.IServiceProvider"/>
    ///         — that is, one per configuration type + extension set, shared by every context that hashes
    ///         the same way. See docs/ServiceProviderCachingAndModelLifetime.md.
    ///     </para>
    /// </summary>
    public interface IOrmModelSource
    {
        /// <summary>
        ///     <para>
        ///         The model, built from <paramref name="context"/>'s <c>OnModelCreating</c> if this is the
        ///         first call. Later calls return the same model and ignore
        ///         <paramref name="context"/> — including when it is a different context, or a different
        ///         <see cref="DataContext"/> subclass, whose own <c>OnModelCreating</c> therefore never
        ///         runs.
        ///     </para>
        /// </summary>
        IOrmModel GetModel(DataContext context);
    }
}
