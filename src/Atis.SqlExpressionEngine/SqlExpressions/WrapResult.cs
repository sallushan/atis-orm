using Atis.SqlExpressionEngine.Visitors;
using System;
using System.Linq;

namespace Atis.SqlExpressionEngine.SqlExpressions
{
    /// <summary>
    ///     What a <see cref="SqlSelectExpression"/> did when it was asked to wrap itself.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Wrapping moves the current query into a sub-query and gives the outer query a new data source
    ///         (a new alias). Any expression built before the wrap, for example a converted predicate or a
    ///         projection, still points at the old alias. <see cref="Rewrite(SqlExpression)"/> turns such an
    ///         expression into one for the new outer query, for example <c>a_1.Col1 + 53</c> into
    ///         <c>a_2.F1 + 53</c>.
    ///     </para>
    ///     <para>
    ///         The result keeps what it needs from the moment of the wrap, so it can be used later. If several
    ///         wraps happen one after another, apply each result to your expressions in the order the wraps
    ///         happened.
    ///     </para>
    /// </remarks>
    public sealed class WrapResult
    {
        private readonly SelectColumn[] innerProjections;
        private readonly AliasedDataSource outerDataSource;

        /// <summary>
        ///     The result of a call where no wrap was needed. <see cref="Rewrite(SqlExpression)"/> returns its input.
        /// </summary>
        public static WrapResult NotWrapped { get; } = new WrapResult(null, null);

        internal WrapResult(SelectColumn[] innerProjections, AliasedDataSource outerDataSource)
        {
            this.innerProjections = innerProjections;
            this.outerDataSource = outerDataSource;
        }

        /// <summary>
        ///     <c>true</c> if the query was wrapped.
        /// </summary>
        public bool Wrapped => this.outerDataSource != null;

        /// <summary>
        ///     Re-points an expression built before the wrap to the new outer query.
        /// </summary>
        public SqlExpression Rewrite(SqlExpression expression)
        {
            if (expression is null)
                throw new ArgumentNullException(nameof(expression));
            if (!this.Wrapped)
                return expression;
            return SubQueryProjectionReplacementVisitor.FindAndReplace(this.innerProjections, this.outerDataSource, expression);
        }

        /// <summary>
        ///     Re-points several expressions. The given array is not changed; a new array is returned.
        /// </summary>
        public SqlExpression[] RewriteAll(SqlExpression[] expressions)
        {
            if (expressions is null)
                throw new ArgumentNullException(nameof(expressions));
            return expressions.Select(this.Rewrite).ToArray();
        }
    }
}
