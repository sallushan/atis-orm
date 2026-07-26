using System;

namespace Atis.Orm.Annotations
{
    /// <summary>
    ///     <para>
    ///         Marks a property as mapping to a store generated key column, such as a SQL Server
    ///         <c>IDENTITY</c> column.
    ///     </para>
    ///     <para>
    ///         The column is never written by Insert or Update; its value is read back from the
    ///         database after Insert and assigned to the entity.
    ///     </para>
    /// </summary>
    /// <seealso cref="Atis.Orm.Metadata.ColumnKind.Identity"/>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class DbIdentityColumnAttribute : Attribute
    {
    }
}
