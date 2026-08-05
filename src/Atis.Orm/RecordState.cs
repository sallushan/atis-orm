namespace Atis.Orm
{
    /// <summary>
    ///     <para>
    ///         What <see cref="DataContext.SaveEntity{T}(T)"/> should do with a <see cref="Record"/>.
    ///     </para>
    ///     <para>
    ///         This ORM does not track changes. The state is set by the consumer and read by
    ///         <c>SaveEntity</c>; nothing sets it on the consumer's behalf. <see cref="Unchanged"/> is
    ///         zero so that an entity materialized from a query starts out as a record that
    ///         <c>SaveEntity</c> will leave alone, without the materializer having to do anything.
    ///     </para>
    /// </summary>
    public enum RecordState
    {
        /// <summary>The record matches the database. <c>SaveEntity</c> does nothing.</summary>
        Unchanged = 0,

        /// <summary>The record is new. <c>SaveEntity</c> inserts it.</summary>
        Added,

        /// <summary>The record exists and has been modified. <c>SaveEntity</c> updates it.</summary>
        Updated,

        /// <summary>The record exists and should be removed. <c>SaveEntity</c> deletes it.</summary>
        Deleted,
    }
}
