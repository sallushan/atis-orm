using System;

namespace Atis.Orm.Annotations
{
    /// <summary>
    ///     <para>
    ///         Marks a property as mapping to a column whose value the database owns — a computed
    ///         column, or one that relies on a database default.
    ///     </para>
    ///     <para>
    ///         The column is never written by Insert or Update; its value is read back from the
    ///         database after both.
    ///     </para>
    /// </summary>
    /// <seealso cref="Atis.Orm.Metadata.ColumnKind.ReadOnly"/>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class DbReadOnlyColumnAttribute : Attribute
    {
    }
}
