using System;
using System.Linq.Expressions;

using Atis.Expressions;
using Atis.SqlExpressionEngine.ExpressionExtensions;

namespace Atis.SqlExpressionEngine.Preprocessors
{
    /// <summary>
    ///     <para>
    ///         Rewrites <see cref="WhereBuilder"/> marker calls into
    ///         <see cref="OptionalPredicateExpression"/>, so the term can be dropped per execution.
    ///     </para>
    ///     <para>
    ///         <strong>The rewrite is value-blind, and that is the whole design.</strong> It never evaluates
    ///         the search value; it emits the same shape whether or not a value was supplied, and the guard is
    ///         tested at render time. A preprocessor that branched on the value would violate the
    ///         shape-determinism contract on <see cref="IExpressionPreprocessor"/>: the compiled-query cache is
    ///         keyed on the <em>original</em> expression, so the branch would be invisible to it and the first
    ///         caller's choice would be frozen into the cache entry for every later caller.
    ///     </para>
    ///     <para>
    ///         The value argument's node instance is reused rather than rebuilt, so it keeps the identity that
    ///         lets its value be rebound on a cache hit.
    ///     </para>
    ///     <para>
    ///         This is a preprocessor rather than a converter because the wider family lowers into
    ///         <see cref="InValuesExpression"/>, which is itself produced by a preprocessor - and converters run
    ///         after all preprocessing has finished.
    ///     </para>
    /// </summary>
    public class WhereBuilderRewriterPreprocessor : ExpressionVisitor, IExpressionPreprocessor
    {
        /// <inheritdoc />
        public Expression Preprocess(Expression node) => this.Visit(node);

        /// <inheritdoc />
        public void Initialize()
        {
        }

        /// <inheritdoc />
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var visited = base.VisitMethodCall(node);

            if (!(visited is MethodCallExpression methodCall)
                || methodCall.Method.DeclaringType != typeof(WhereBuilder))
            {
                return visited;
            }

            if (methodCall.Method.Name == nameof(WhereBuilder.Equal) && methodCall.Arguments.Count == 2)
            {
                var column = methodCall.Arguments[0];
                var value = methodCall.Arguments[1];
                // `value` is deliberately the same node instance in both positions: as the guard, and as the
                // right operand. Rebuilding it (e.g. as a fresh Constant) would strip the variable identity and
                // burn the first execution's value into the cached query.
                return new OptionalPredicateExpression(value, Expression.Equal(column, value));
            }

            throw new NotSupportedException(
                $"'{nameof(WhereBuilder)}.{methodCall.Method.Name}' with {methodCall.Arguments.Count} argument(s) " +
                $"is not supported yet. Only '{nameof(WhereBuilder)}.{nameof(WhereBuilder.Equal)}' is implemented.");
        }
    }
}
