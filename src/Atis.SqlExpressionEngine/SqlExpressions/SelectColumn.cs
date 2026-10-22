namespace Atis.SqlExpressionEngine.SqlExpressions
{
    public class SelectColumn
    {
        /// <param name="helperColumn">
        ///     The column is not part of the projection. It was added only so that something else can reach a value
        ///     (the condition of a join, a sub-query, the link to a child query), so it does not count when deciding
        ///     whether a select list is a scalar one.
        /// </param>
        public SelectColumn(SqlExpression columnExpression, string alias, bool scalarColumn, bool helperColumn = false)
        {
            this.ColumnExpression = columnExpression;
            this.Alias = alias;
            this.ScalarColumn = scalarColumn;
            this.HelperColumn = helperColumn;
        }

        public SqlExpression ColumnExpression { get; }
        public string Alias { get; }
        public bool ScalarColumn { get; }
        public bool HelperColumn { get; }

        public override string ToString()
        {
            return $"{this.ColumnExpression} as {this.Alias}";
        }
    }
}
