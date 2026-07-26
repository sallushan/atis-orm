using System;
using System.Linq.Expressions;

namespace Atis.Orm.Metadata
{
    public class ColumnBuilder<T>
    {
        private readonly MutableEntityMetadata _mutable;
        private readonly string _propertyName;

        internal ColumnBuilder(MutableEntityMetadata mutable, string propertyName)
        {
            _mutable = mutable ?? throw new ArgumentNullException(nameof(mutable));
            _propertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
        }

        public ColumnBuilder<T> SetColumnName(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
                throw new ArgumentNullException(nameof(columnName));
            _mutable.GetOrAddColumn(_propertyName).DatabaseColumnName = columnName;
            return this;
        }

        public ColumnBuilder<T> MarkAsKey()
        {
            _mutable.GetOrAddColumn(_propertyName).IsPrimaryKey = true;
            return this;
        }

        /// <summary>
        ///     <para>
        ///         Sets how this column participates in Insert and Update.
        ///     </para>
        ///     <para>
        ///         Overrides whatever the annotations on the property said. The kinds are mutually
        ///         exclusive, so calling this more than once for the same column simply keeps the last
        ///         value.
        ///     </para>
        /// </summary>
        public ColumnBuilder<T> HasKind(ColumnKind kind)
        {
            _mutable.GetOrAddColumn(_propertyName).Kind = kind;
            return this;
        }

        /// <summary>
        ///     Marks the column as store generated, e.g. a SQL Server <c>IDENTITY</c> column. Never
        ///     written; read back after Insert.
        /// </summary>
        public ColumnBuilder<T> IsIdentity() => this.HasKind(ColumnKind.Identity);

        /// <summary>
        ///     Marks the column as owned by the database — computed, or relying on a database default.
        ///     Never written; read back after Insert and Update.
        /// </summary>
        public ColumnBuilder<T> IsComputed() => this.HasKind(ColumnKind.ReadOnly);

        /// <summary>
        ///     Marks the column as the store maintained concurrency token of the entity. The property
        ///     must be of type <c>byte[]</c>.
        /// </summary>
        public ColumnBuilder<T> IsRowVersion() => this.HasKind(ColumnKind.RowVersion);

        /// <summary>
        ///     Marks the column as written on Insert but never on Update.
        /// </summary>
        public ColumnBuilder<T> InsertOnly() => this.HasKind(ColumnKind.InsertOnly);

        /// <summary>
        ///     Marks the column as written on Update but never on Insert.
        /// </summary>
        public ColumnBuilder<T> UpdateOnly() => this.HasKind(ColumnKind.UpdateOnly);

        /// <summary>
        ///     Requires a value for this column before the entity can be inserted or updated.
        /// </summary>
        /// <param name="fieldTitle">
        ///     The name to report when validation fails, or <c>null</c> to use the property name.
        /// </param>
        public ColumnBuilder<T> IsRequired(string fieldTitle = null)
        {
            var column = _mutable.GetOrAddColumn(_propertyName);
            column.IsRequired = true;
            column.RequiredFieldTitle = string.IsNullOrWhiteSpace(fieldTitle) ? null : fieldTitle;
            return this;
        }
    }
}