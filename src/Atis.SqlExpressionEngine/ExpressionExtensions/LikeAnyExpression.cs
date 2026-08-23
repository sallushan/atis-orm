using System;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.ExpressionExtensions
{
    /// <summary>
    ///     <para>
    ///         A <c>LIKE</c> against <em>each</em> of a collection of values, the results joined with <c>OR</c>:
    ///         <c>(col LIKE '%' + v1 + '%' OR col LIKE '%' + v2 + '%' ...)</c>. Produced by
    ///         <see cref="WhereBuilder"/>'s preprocessor for the <c>...Any</c> methods.
    ///     </para>
    ///     <para>
    ///         <strong>Why this cannot reuse the <c>IN</c> machinery.</strong> An <c>IN</c> list is a
    ///         comma-separated list at one position, so a collection parameter expands inside a single marker.
    ///         Here the <em>whole predicate</em> repeats - SQL Server has no <c>LIKE ANY (...)</c> - so the
    ///         translator emits the term once as a template and the renderer repeats it per element.
    ///     </para>
    ///     <para>
    ///         Nothing here reads the collection: how many times the template repeats is settled at render
    ///         time, from the value bound to that execution, so one compiled query serves collections of every
    ///         length.
    ///     </para>
    /// </summary>
    public class LikeAnyExpression : Expression
    {
        /// <summary>Creates a multi-value LIKE.</summary>
        /// <param name="expression">The string expression being matched.</param>
        /// <param name="values">The collection of values to match against.</param>
        /// <param name="matchMode">How each value is decorated with wildcards.</param>
        public LikeAnyExpression(Expression expression, Expression values, LikeMatchMode matchMode)
        {
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
            this.Values = values ?? throw new ArgumentNullException(nameof(values));
            this.MatchMode = matchMode;
        }

        /// <inheritdoc />
        public override ExpressionType NodeType => ExpressionType.Extension;

        /// <inheritdoc />
        public sealed override Type Type => typeof(bool);

        /// <summary>The string expression being matched.</summary>
        public Expression Expression { get; }

        /// <summary>The collection of values, matched one at a time and OR-ed together.</summary>
        public Expression Values { get; }

        /// <summary>How each value is decorated with wildcards.</summary>
        public LikeMatchMode MatchMode { get; }

        /// <inheritdoc />
        /// <remarks>
        ///     <para>
        ///         <see cref="Values"/> must be visited, and must stay the caller's own node: it carries the
        ///         variable identity the collection is rebound by on a cache hit, and it is also the node the
        ///         surrounding optional term uses as its guard.
        ///     </para>
        /// </remarks>
        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var updatedExpression = visitor.Visit(this.Expression);
            var updatedValues = visitor.Visit(this.Values);

            if (updatedExpression == this.Expression && updatedValues == this.Values)
                return this;

            return new LikeAnyExpression(updatedExpression, updatedValues, this.MatchMode);
        }

        /// <inheritdoc />
        public override string ToString() => $"{this.GetType().Name}({this.Expression}, {this.MatchMode}, {this.Values})";
    }
}
