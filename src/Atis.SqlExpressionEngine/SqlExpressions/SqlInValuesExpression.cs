using System;

using Atis.SqlExpressionEngine.Visitors;
namespace Atis.SqlExpressionEngine.SqlExpressions
{
    /// <summary>
    ///     <para>
    ///         Represents a SQL <c>IN (...)</c> test.
    ///     </para>
    /// </summary>
    public class SqlInValuesExpression : SqlExpression
    {
        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="SqlInValuesExpression"/> class.
        ///     </para>
        /// </summary>
        /// <param name="expression">The expression tested for membership.</param>
        /// <param name="values">The value list; see <see cref="Values"/> for the accepted shapes.</param>
        /// <exception cref="ArgumentNullException">Either argument is <c>null</c>.</exception>
        public SqlInValuesExpression(SqlExpression expression, SqlExpression values)
        {
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
            this.Values = values ?? throw new ArgumentNullException(nameof(values));
        }

        /// <summary>Gets the expression tested for membership in <see cref="Values"/>.</summary>
        public SqlExpression Expression { get; }

        /// <summary>
        ///     <para>
        ///         Gets the value list, as a <em>single</em> node. Two shapes are meaningful here, and the node
        ///         type is what distinguishes them - the count never does:
        ///     </para>
        ///     <list type="bullet">
        ///         <item>
        ///             <description>
        ///                 <see cref="SqlParameterExpression"/> - a collection supplied at execution time
        ///                 (<c>departments.Contains(x.Department)</c>). It expands into one placeholder per
        ///                 element when the query is rendered, so the same compiled query serves collections of
        ///                 any length, including none.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 <see cref="SqlCollectionExpression"/> - an inline array
        ///                 (<c>new[] { "HR", dept }.Contains(x.Department)</c>). Its length is fixed by the
        ///                 expression itself, and each element keeps its own nature: a constant stays a literal,
        ///                 a captured variable stays a parameter that rebinds on a cache hit. Collapsing such an
        ///                 array into one frozen value would silently bake the first execution's variables into
        ///                 every later one.
        ///             </description>
        ///         </item>
        ///     </list>
        /// </summary>
        public SqlExpression Values { get; }

        /// <inheritdoc />
        public override SqlExpressionType NodeType => SqlExpressionType.InValues;

        /// <inheritdoc />
        protected internal override SqlExpression Accept(SqlExpressionVisitor sqlExpressionVisitor)
        {
            return sqlExpressionVisitor.VisitInValues(this);
        }

        /// <summary>
        ///     <para>
        ///         Returns this node when the children are unchanged, otherwise an updated copy.
        ///     </para>
        /// </summary>
        public SqlExpression Update(SqlExpression expression, SqlExpression values)
        {
            if (expression == this.Expression && values == this.Values)
            {
                return this;
            }
            return new SqlInValuesExpression(expression, values);
        }
    }
}
