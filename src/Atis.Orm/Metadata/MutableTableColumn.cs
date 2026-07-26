using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Atis.Orm.Metadata
{
    internal class MutableTableColumn
    {
        public string DatabaseColumnName { get; set; }
        public string ModelPropertyName { get; }
        public bool IsPrimaryKey { get; set; }

        // The persistence side of the column. It is held here so that the fluent surface has a single
        // mutation target; the split into EntityMetadata (query) and EntityCrudMetadata (persistence)
        // happens only when the mutable state is built.
        public ColumnKind Kind { get; set; } = ColumnKind.Regular;
        public bool IsRequired { get; set; }
        public string RequiredFieldTitle { get; set; }

        public MutableTableColumn(TableColumn tableColumn)
        {
            if (tableColumn is null)
                throw new ArgumentNullException(nameof(tableColumn));
            this.DatabaseColumnName = tableColumn.DatabaseColumnName;
            this.ModelPropertyName = tableColumn.ModelPropertyName ?? throw new ArgumentNullException(nameof(tableColumn.ModelPropertyName));
            this.IsPrimaryKey = tableColumn.IsPrimaryKey;
        }

        public MutableTableColumn(string databaseColumnName, string modelPropertyName, bool isPrimaryKey)
        {
            this.DatabaseColumnName = databaseColumnName ?? throw new ArgumentNullException(nameof(databaseColumnName));
            this.ModelPropertyName = modelPropertyName ?? throw new ArgumentNullException(nameof(modelPropertyName));
            this.IsPrimaryKey = isPrimaryKey;
        }
    }
}
