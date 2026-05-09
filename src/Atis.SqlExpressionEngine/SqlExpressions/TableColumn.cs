namespace Atis.SqlExpressionEngine.SqlExpressions
{
    public class TableColumn
    {
        public TableColumn(string databaseColumnName, string modelPropertyName)
            : this(databaseColumnName, modelPropertyName, isPrimaryKey: false)
        {
        }

        public TableColumn(string databaseColumnName, string modelPropertyName, bool isPrimaryKey)
        {
            this.DatabaseColumnName = databaseColumnName;
            this.ModelPropertyName = modelPropertyName;
            this.IsPrimaryKey = isPrimaryKey;
        }

        public string DatabaseColumnName { get; }
        public string ModelPropertyName { get; }
        public bool IsPrimaryKey { get; }
    }
}
