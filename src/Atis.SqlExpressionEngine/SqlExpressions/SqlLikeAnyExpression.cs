using System;

using Atis.SqlExpressionEngine.Visitors;

namespace Atis.SqlExpressionEngine.SqlExpressions
{
    /// <summary>
    ///     <para>
    ///         SQL-side counterpart of <see cref="ExpressionExtensions.LikeAnyExpression"/>: one <c>LIKE</c>
    ///         per element of <see cref="Values"/>, OR-ed together.
    ///     </para>
    ///     <para>
    ///         The element count is not known here and must not be: the translator emits the term once as a
    ///         template with the collection's marker where an element goes, and the renderer repeats that
    ///         template per element at execution time. That is what keeps one compiled query serving
    ///         collections of every length, the same property the <c>IN</c> expansion has.
    ///     </para>
    /// </summary>
    public class SqlLikeAnyExpression : SqlExpression
    {
        /// <summary>Creates a multi-value LIKE node.</summary>
        /// <param name="expression">The string expression being matched.</param>
        /// <param name="values">The collection of values, as a single expression.</param>
        /// <param name="matchMode">How each value is decorated with wildcards.</param>
        public SqlLikeAnyExpression(SqlExpression expression, SqlExpression values, LikeMatchMode matchMode)
        {
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
            this.Values = values ?? throw new ArgumentNullException(nameof(values));
            this.MatchMode = matchMode;
        }

        /// <summary>The string expression being matched.</summary>
        public SqlExpression Expression { get; }

        /// <summary>
        ///     The collection of values, kept whole rather than split into one expression per element - the
        ///     elements are only reachable once a value is bound.
        /// </summary>
        public SqlExpression Values { get; }

        /// <summary>How each value is decorated with wildcards.</summary>
        public LikeMatchMode MatchMode { get; }

        /// <inheritdoc />
        public override SqlExpressionType NodeType => SqlExpressionType.LikeAny;

        /// <inheritdoc />
        protected internal override SqlExpression Accept(SqlExpressionVisitor sqlExpressionVisitor)
        {
            return sqlExpressionVisitor.VisitSqlLikeAny(this);
        }

        /// <summary>Returns this node, or a new one when a child changed.</summary>
        public SqlExpression Update(SqlExpression expression, SqlExpression values)
        {
            if (expression == this.Expression && values == this.Values)
                return this;
            return new SqlLikeAnyExpression(expression, values, this.MatchMode);
        }

        /// <inheritdoc />
        public override string ToString() => $"{this.Expression} {this.NodeType}({this.MatchMode}) {this.Values}";
    }
}
