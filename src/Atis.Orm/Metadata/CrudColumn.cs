using System;
using System.Reflection;

namespace Atis.Orm.Metadata
{
    /// <summary>
    ///     <para>
    ///         The persistence side of a mapped column: how it participates in entity level
    ///         Insert / Update (<see cref="Kind"/>), the property it reads and writes, and whether the
    ///         value is required.
    ///     </para>
    ///     <para>
    ///         This deliberately carries no column <em>name</em> and no primary key flag — those belong
    ///         to <see cref="Atis.SqlExpressionEngine.SqlExpressions.TableColumn"/> and are read from
    ///         the <see cref="Atis.SqlExpressionEngine.EntityMetadata"/> of the same entity. The two are
    ///         paired on <see cref="ModelPropertyName"/>, which is the same key
    ///         <c>SqlTableExpression.GetByPropertyName</c> already uses.
    ///     </para>
    /// </summary>
    public class CrudColumn
    {
        /// <summary>
        ///     Constructs a <see cref="CrudColumn"/>.
        /// </summary>
        /// <param name="property">The property this column maps to.</param>
        /// <param name="kind">How the column participates in Insert and Update.</param>
        /// <param name="isRequired">Whether the value must be supplied before a write.</param>
        /// <param name="requiredFieldTitle">
        ///     The name to report when required field validation fails, or <c>null</c> to use the
        ///     property name.
        /// </param>
        public CrudColumn(PropertyInfo property, ColumnKind kind, bool isRequired, string requiredFieldTitle)
        {
            this.Property = property ?? throw new ArgumentNullException(nameof(property));
            this.Kind = kind;
            this.IsRequired = isRequired;
            this.RequiredFieldTitle = requiredFieldTitle;
        }

        /// <summary>
        ///     The property this column maps to. Also the key that pairs this instance with the
        ///     matching <see cref="Atis.SqlExpressionEngine.SqlExpressions.TableColumn"/>.
        /// </summary>
        public string ModelPropertyName => this.Property.Name;

        /// <summary>
        ///     How the column participates in Insert and Update, and whether the database owns its
        ///     value.
        /// </summary>
        public ColumnKind Kind { get; }

        /// <summary>
        ///     The property this column reads from and writes to.
        /// </summary>
        public PropertyInfo Property { get; }

        /// <summary>
        ///     The CLR type of <see cref="Property"/>.
        /// </summary>
        public Type ClrType => this.Property.PropertyType;

        /// <summary>
        ///     Whether the value must be supplied before the entity can be inserted or updated.
        /// </summary>
        public bool IsRequired { get; }

        /// <summary>
        ///     The name to report when required field validation fails. <c>null</c> means the property
        ///     name is used.
        /// </summary>
        public string RequiredFieldTitle { get; }
    }
}
