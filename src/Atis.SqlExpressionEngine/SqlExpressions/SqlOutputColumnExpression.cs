using Atis.SqlExpressionEngine.Visitors;

namespace Atis.SqlExpressionEngine.SqlExpressions
{
    /// <summary>
    ///     <para>
    ///         A column of the rows a data-manipulation statement returns — the OUTPUT clause of an
    ///         UPDATE, and its equivalents.
    ///     </para>
    ///     <para>
    ///         Distinct from <see cref="SqlDataSourceColumnExpression"/>, which is bound to a data
    ///         source in the query and carries that source's alias. This one is not: it names the
    ///         row image it comes from (<see cref="SqlOutputSource"/>) and leaves the dialect to
    ///         render it, so no vendor keyword is baked into the expression tree.
    ///     </para>
    /// </summary>
    public class SqlOutputColumnExpression : SqlExpression
    {
        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="SqlOutputColumnExpression"/> class.
        ///     </para>
        /// </summary>
        /// <param name="source">The row image the column is read from.</param>
        /// <param name="columnName">The name of the column.</param>
        public SqlOutputColumnExpression(SqlOutputSource source, string columnName)
        {
            this.Source = source;
            this.ColumnName = columnName;
        }

        /// <inheritdoc />
        public override SqlExpressionType NodeType => SqlExpressionType.OutputColumn;

        /// <summary>
        ///     <para>
        ///         Gets the row image the column is read from.
        ///     </para>
        /// </summary>
        public SqlOutputSource Source { get; }

        /// <summary>
        ///     <para>
        ///         Gets the name of the column.
        ///     </para>
        /// </summary>
        public string ColumnName { get; }

        /// <summary>
        ///     <para>
        ///         Accepts a visitor to visit this SQL output column expression.
        ///     </para>
        /// </summary>
        /// <param name="sqlExpressionVisitor">The visitor to accept.</param>
        /// <returns>The result of visiting this expression.</returns>
        protected internal override SqlExpression Accept(SqlExpressionVisitor sqlExpressionVisitor)
        {
            return sqlExpressionVisitor.VisitSqlOutputColumn(this);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{this.Source}.{this.ColumnName}";
        }
    }
}
