using System;
using System.Linq;

namespace Atis.Orm.Annotations
{
    /// <summary>
    ///     <para>
    ///         Maps a property whose type is a composite (value object) class onto several columns of
    ///         the containing entity's own table, instead of a separate table. Each entry in
    ///         <see cref="Properties"/> names a member of the value object's own type, paired
    ///         positionally with the database column in <see cref="Columns"/> it reads from and writes
    ///         to.
    ///     </para>
    ///     <para>
    ///         The mapping is declared per containing property rather than by a naming convention,
    ///         because the same value-object type can be reused on more than one entity property with a
    ///         different set of columns each time.
    ///     </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class ValueObjectAttribute : Attribute
    {
        public ValueObjectAttribute(string[] properties, string[] columns)
        {
            if (properties == null) throw new ArgumentNullException(nameof(properties));
            if (columns == null) throw new ArgumentNullException(nameof(columns));
            if (properties.Length == 0)
                throw new ArgumentException("At least one property must be mapped.", nameof(properties));
            if (properties.Length != columns.Length)
                throw new ArgumentException("Properties and columns must have the same length.", nameof(columns));
            if (properties.Any(string.IsNullOrWhiteSpace) || columns.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("Property and column names cannot be null or whitespace.");

            this.Properties = properties;
            this.Columns = columns;
        }

        /// <summary>
        ///     The value object's own member names, in the same order as <see cref="Columns"/>.
        /// </summary>
        public string[] Properties { get; }

        /// <summary>
        ///     The database column each entry of <see cref="Properties"/> maps to, in the same order.
        /// </summary>
        public string[] Columns { get; }
    }
}
