using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Atis.Orm
{
    internal class MutableTableColumn
    {
        public string DatabaseColumnName { get; set; }
        public string ModelPropertyName { get; }
        public bool IsPrimaryKey { get; set; }

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
