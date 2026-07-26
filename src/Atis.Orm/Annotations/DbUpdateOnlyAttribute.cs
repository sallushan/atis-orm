using System;

namespace Atis.Orm.Annotations
{
    /// <summary>
    ///     <para>
    ///         Marks a property as mapping to a column that is written on Update but never on Insert,
    ///         for values that only become meaningful once the row is being modified — a "modified by"
    ///         audit column, for example.
    ///     </para>
    /// </summary>
    /// <seealso cref="Atis.Orm.Metadata.ColumnKind.UpdateOnly"/>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class DbUpdateOnlyAttribute : Attribute
    {
    }
}
