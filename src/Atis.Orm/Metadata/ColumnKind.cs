namespace Atis.Orm.Metadata
{
    /// <summary>
    ///     <para>
    ///         Describes how a mapped column participates in entity level Insert / Update statements
    ///         and whether its value is produced by the database.
    ///     </para>
    ///     <para>
    ///         This is a persistence concept and therefore lives in <c>Atis.Orm</c>, not in the
    ///         expression engine. It is carried by <see cref="CrudColumn"/> and never reaches
    ///         <see cref="Atis.SqlExpressionEngine.SqlExpressions.TableColumn"/>.
    ///     </para>
    ///     <para>
    ///         The kinds are mutually exclusive by design (this is not a <c>[Flags]</c> enum), which is
    ///         what makes a nonsensical combination such as "insert only and update only" impossible
    ///         to express rather than something that has to be validated.
    ///     </para>
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The resulting column participation is:
    ///         <list type="bullet">
    ///             <item><description><c>INSERT</c> writes <see cref="Regular"/> and <see cref="InsertOnly"/> columns.</description></item>
    ///             <item><description><c>UPDATE</c> writes <see cref="Regular"/> and <see cref="UpdateOnly"/> columns that are not primary keys.</description></item>
    ///             <item><description><see cref="Identity"/>, <see cref="ReadOnly"/> and <see cref="RowVersion"/> are never written, and are read back from the database after a write.</description></item>
    ///         </list>
    ///     </para>
    /// </remarks>
    public enum ColumnKind
    {
        /// <summary>
        ///     An ordinary column. Written on both Insert and Update.
        /// </summary>
        Regular = 0,

        /// <summary>
        ///     <para>
        ///         A store generated key (e.g. SQL Server <c>IDENTITY</c>). Never written; its value is
        ///         read back after Insert and assigned to the entity.
        ///     </para>
        /// </summary>
        Identity,

        /// <summary>
        ///     <para>
        ///         A column whose value the database owns — a computed column, or one that relies on a
        ///         database default. Never written; read back after both Insert and Update.
        ///     </para>
        /// </summary>
        ReadOnly,

        /// <summary>
        ///     <para>
        ///         A store maintained concurrency token (e.g. SQL Server <c>ROWVERSION</c>). Never
        ///         written and read back after every write, but unlike <see cref="ReadOnly"/> it also
        ///         takes part in the <c>WHERE</c> clause of an Update or Delete when optimistic
        ///         concurrency is requested.
        ///     </para>
        /// </summary>
        RowVersion,

        /// <summary>
        ///     <para>
        ///         Written on Insert but never on Update — for values that are set once when the row is
        ///         created, such as a "created by" audit column.
        ///     </para>
        /// </summary>
        InsertOnly,

        /// <summary>
        ///     <para>
        ///         Written on Update but never on Insert — for values that only become meaningful once
        ///         the row is being modified, such as a "modified by" audit column.
        ///     </para>
        /// </summary>
        UpdateOnly,
    }
}
