using System;
using System.Collections.Generic;
using System.Linq;

namespace Atis.Orm.Metadata
{
    /// <summary>
    ///     <para>
    ///         The persistence side of an entity's mapping — everything entity level Insert / Update /
    ///         Delete needs that query translation does not.
    ///     </para>
    ///     <para>
    ///         It is the companion of <see cref="Atis.SqlExpressionEngine.EntityMetadata"/>, not a
    ///         replacement for it: table name, schema, column names and primary keys are read from
    ///         there, and the two are paired on <see cref="CrudColumn.ModelPropertyName"/>. Keeping
    ///         them apart is what allows the expression engine to stay a pure AST layer with no
    ///         knowledge of entity persistence.
    ///     </para>
    /// </summary>
    public class EntityCrudMetadata
    {
        private readonly IReadOnlyDictionary<string, CrudColumn> columnsByPropertyName;

        /// <summary>
        ///     Constructs an <see cref="EntityCrudMetadata"/>.
        /// </summary>
        /// <param name="clrType">The entity type this metadata describes.</param>
        /// <param name="columns">The persistence side of each mapped column.</param>
        public EntityCrudMetadata(Type clrType, IReadOnlyList<CrudColumn> columns)
        {
            this.ClrType = clrType ?? throw new ArgumentNullException(nameof(clrType));
            this.Columns = columns ?? throw new ArgumentNullException(nameof(columns));
            this.columnsByPropertyName = columns.ToDictionary(x => x.ModelPropertyName, StringComparer.Ordinal);
        }

        /// <summary>
        ///     The entity type this metadata describes.
        /// </summary>
        public Type ClrType { get; }

        /// <summary>
        ///     The persistence side of each mapped column, in the same order as
        ///     <see cref="Atis.SqlExpressionEngine.EntityMetadata.SqlColumns"/>.
        /// </summary>
        public IReadOnlyList<CrudColumn> Columns { get; }

        /// <summary>
        ///     Looks up a column by the name of the property it maps to.
        /// </summary>
        public bool TryGetColumn(string modelPropertyName, out CrudColumn column)
        {
            if (modelPropertyName is null)
                throw new ArgumentNullException(nameof(modelPropertyName));
            return this.columnsByPropertyName.TryGetValue(modelPropertyName, out column);
        }

        /// <summary>
        ///     Looks up a column by the name of the property it maps to, returning <c>null</c> when the
        ///     property is not mapped.
        /// </summary>
        public CrudColumn GetColumn(string modelPropertyName)
        {
            return this.TryGetColumn(modelPropertyName, out var column) ? column : null;
        }
    }
}
