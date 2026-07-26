using System;

namespace Atis.Orm.Annotations
{
    /// <summary>
    ///     <para>
    ///         Marks a property as mapping to a column that is written on Insert but never on Update,
    ///         for values that are set once when the row is created — a "created by" audit column,
    ///         for example.
    ///     </para>
    /// </summary>
    /// <seealso cref="Atis.Orm.Metadata.ColumnKind.InsertOnly"/>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class DbInsertOnlyAttribute : Attribute
    {
    }
}
