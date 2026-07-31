using Atis.SqlExpressionEngine.Visitors;
namespace Atis.SqlExpressionEngine.SqlExpressions
{
    // The main difference between this class and SqlDataSourceColumnExpression is that this class represents a column expression
    // that is not tied to a specific data source, while SqlDataSourceColumnExpression represents a column expression that is tied
    // to a specific data source. This class can be used in scenarios where the data source is a fixed keyword, for example,
    // `inserted`, `deleted`, or `output`. In such cases, the data source name is not a variable, but a fixed keyword that
    // represents a specific data source.

    public class SqlColumnExpression : SqlExpression
    {
        public SqlColumnExpression(string dataSourceName, string columnName)
        {
            this.DataSourceName = dataSourceName;
            this.ColumnName = columnName;
        }

        /// <inheritdoc />
        public override SqlExpressionType NodeType => SqlExpressionType.Column;

        /// <summary>
        ///     <para>
        ///         Gets the name of the data source.
        ///     </para>
        /// </summary>
        public string DataSourceName { get; }

        /// <summary>
        ///     <para>
        ///         Gets the name of the column.
        ///     </para>
        /// </summary>
        public string ColumnName { get; }

        /// <summary>
        ///     <para>
        ///         Accepts a visitor to visit this SQL column expression.
        ///     </para>
        /// </summary>
        /// <param name="sqlExpressionVisitor">The visitor to accept.</param>
        /// <returns>The result of visiting this expression.</returns>
        protected internal override SqlExpression Accept(SqlExpressionVisitor sqlExpressionVisitor)
        {
            return sqlExpressionVisitor.VisitSqlColumn(this);
        }

        /// <summary>
        ///     <para>
        ///         Returns a string representation of the SQL column expression.
        ///     </para>
        ///     <para>
        ///         The string representation includes the data source name and column name.
        ///     </para>
        /// </summary>
        /// <returns>A string representation of the SQL column expression.</returns>
        public override string ToString()
        {
            return $"{this.DataSourceName}.{this.ColumnName}";
        }
    }
}
