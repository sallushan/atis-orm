using Atis.SqlExpressionEngine.SqlExpressions;
using System.Collections.Generic;

namespace Atis.Orm.DataManipulation
{
    /// <summary>
    ///     <para>
    ///         Where one chunk of a streamed column goes: the table, the column, and the primary key of
    ///         the row. Handed to a provider's <c>WriteColumnChunk</c>, which appends the chunk to the
    ///         column's current value.
    ///     </para>
    ///     <para>
    ///         The names are the mapping's own, not yet spelled as SQL. The provider spells them through
    ///         <see cref="Abstractions.ISqlNaming"/>, the same service the query translator uses, so the
    ///         chunk statements reach the same table the insert or update just wrote.
    ///     </para>
    /// </summary>
    public sealed class ColumnChunkTarget
    {
        /// <summary>Constructs the target.</summary>
        public ColumnChunkTarget(SqlTable table, string columnName, IReadOnlyList<KeyValuePair<string, object>> keyColumns)
        {
            this.Table = table;
            this.ColumnName = columnName;
            this.KeyColumns = keyColumns;
        }

        /// <summary>The table the entity is mapped to.</summary>
        public SqlTable Table { get; }

        /// <summary>The database column the chunks are appended to.</summary>
        public string ColumnName { get; }

        /// <summary>The primary key columns, by database column name, with the row's values for them.</summary>
        public IReadOnlyList<KeyValuePair<string, object>> KeyColumns { get; }
    }
}
