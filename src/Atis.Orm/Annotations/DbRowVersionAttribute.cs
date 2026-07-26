using System;

namespace Atis.Orm.Annotations
{
    /// <summary>
    ///     <para>
    ///         Marks a property as the store maintained concurrency token of the entity, such as a
    ///         SQL Server <c>ROWVERSION</c> column. The property must be of type <c>byte[]</c>.
    ///     </para>
    ///     <para>
    ///         The column is never written, and is read back after every write. When optimistic
    ///         concurrency is requested it is also added to the <c>WHERE</c> clause of Update and
    ///         Delete, so that a stale entity fails to match any row.
    ///     </para>
    /// </summary>
    /// <seealso cref="Atis.Orm.Metadata.ColumnKind.RowVersion"/>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class DbRowVersionAttribute : Attribute
    {
    }
}
