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
            : this(new ColumnPath(property), kind, isRequired, requiredFieldTitle)
        {
        }

        /// <summary>
        ///     Constructs a <see cref="CrudColumn"/> that may belong to a value object.
        /// </summary>
        /// <param name="path">The route from the entity to the property this column maps to.</param>
        /// <param name="kind">How the column participates in Insert and Update.</param>
        /// <param name="isRequired">Whether the value must be supplied before a write.</param>
        /// <param name="requiredFieldTitle">
        ///     The name to report when required field validation fails, or <c>null</c> to use the
        ///     property name.
        /// </param>
        public CrudColumn(ColumnPath path, ColumnKind kind, bool isRequired, string requiredFieldTitle)
        {
            this.Path = path ?? throw new ArgumentNullException(nameof(path));
            this.Kind = kind;
            this.IsRequired = isRequired;
            this.RequiredFieldTitle = requiredFieldTitle;
        }

        /// <summary>
        ///     The property this column maps to, dotted when it belongs to a value object. Also the key
        ///     that pairs this instance with the matching
        ///     <see cref="Atis.SqlExpressionEngine.SqlExpressions.TableColumn"/>.
        /// </summary>
        public string ModelPropertyName => this.Path.Name;

        /// <summary>
        ///     How the column participates in Insert and Update, and whether the database owns its
        ///     value.
        /// </summary>
        public ColumnKind Kind { get; }

        /// <summary>
        ///     The route to the property this column reads from and writes to.
        /// </summary>
        public ColumnPath Path { get; }

        /// <summary>
        ///     The CLR type of the property at the end of <see cref="Path"/>.
        /// </summary>
        public Type ClrType => this.Path.Type;

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
