using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Atis.Expressions;
using Atis.SqlExpressionEngine.ExpressionExtensions;

namespace Atis.SqlExpressionEngine.Preprocessors
{
    /// <summary>
    ///     <para>
    ///         Rewrites <see cref="WhereBuilder"/> marker calls into
    ///         <see cref="OptionalPredicateExpression"/>, so each term can be dropped per execution.
    ///     </para>
    ///     <para>
    ///         <strong>Every rewrite is value-blind, and that is the whole design.</strong> Nothing here
    ///         evaluates a search value; the same shape is emitted whether or not a value was supplied, and the
    ///         guard is tested at render time. A preprocessor that branched on the value would violate the
    ///         shape-determinism contract on <see cref="IExpressionPreprocessor"/>: the compiled-query cache is
    ///         keyed on the <em>original</em> expression, so the branch would be invisible to it and the first
    ///         caller's choice would be frozen into the cache entry for every later caller. This is the single
    ///         biggest departure from the old <c>Atis.ORM.WhereBuilder</c>, whose every <c>_internal</c> method
    ///         called <c>GetValueFromExpressionCompile</c> and branched on the result.
    ///     </para>
    ///     <para>
    ///         <strong>The value argument's node instance is reused, never rebuilt.</strong> It is what carries
    ///         the variable identity that lets the value be rebound on a cache hit. Wrapping it in any
    ///         transform - even <c>.Value</c> on a nullable - changes that identity to one the original tree
    ///         does not contain, and the parameter then silently freezes to its first value. That is why
    ///         <see cref="WhereBuilder.DateRange"/> shifts its upper bound in SQL rather than in C#.
    ///     </para>
    ///     <para>
    ///         This is a preprocessor rather than a converter because the <c>IN</c> family lowers into
    ///         <see cref="InValuesExpression"/>, which is itself produced by a preprocessor - and converters run
    ///         after all preprocessing has finished.
    ///     </para>
    /// </summary>
    public class WhereBuilderRewriterPreprocessor : ExpressionVisitor, IExpressionPreprocessor
    {
        private static readonly MethodInfo StringContains = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) });
        private static readonly MethodInfo StringStartsWith = typeof(string).GetMethod(nameof(string.StartsWith), new[] { typeof(string) });
        private static readonly MethodInfo StringEndsWith = typeof(string).GetMethod(nameof(string.EndsWith), new[] { typeof(string) });

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

            var arguments = methodCall.Arguments;
            switch (methodCall.Method.Name)
            {
                case nameof(WhereBuilder.Equal):
                    return Optional(arguments[1], Expression.Equal(arguments[0], arguments[1]));

                case nameof(WhereBuilder.Contains):
                    return Optional(arguments[1], Expression.Call(arguments[0], StringContains, arguments[1]));

                case nameof(WhereBuilder.StartsWith):
                    return Optional(arguments[1], Expression.Call(arguments[0], StringStartsWith, arguments[1]));

                case nameof(WhereBuilder.EndsWith):
                    return Optional(arguments[1], Expression.Call(arguments[0], StringEndsWith, arguments[1]));

                case nameof(WhereBuilder.LikePattern):
                    // No BCL spelling means "match this pattern verbatim", so this one needs its own node.
                    return Optional(arguments[1], new LikePatternExpression(arguments[0], arguments[1]));

                case nameof(WhereBuilder.In):
                    return Optional(arguments[1], new InValuesExpression(arguments[0], arguments[1]), OptionalGuardKind.NullOrEmptyCollection);

                case nameof(WhereBuilder.NotIn):
                    return Optional(arguments[1], Expression.Not(new InValuesExpression(arguments[0], arguments[1])), OptionalGuardKind.NullOrEmptyCollection);

                case nameof(WhereBuilder.DateRange):
                    return RewriteDateRange(arguments[0], arguments[1], arguments[2]);

                default:
                    throw new NotSupportedException(
                        $"'{nameof(WhereBuilder)}.{methodCall.Method.Name}' is not supported yet.");
            }
        }

        private static Expression Optional(Expression guard, Expression predicate, OptionalGuardKind guardKind = OptionalGuardKind.NullOnly)
            => new OptionalPredicateExpression(guard, predicate, guardKind);

        /// <summary>
        ///     <para>
        ///         Two independently optional bounds over one column, joined with <c>AND</c> - so supplying
        ///         only one still filters, and supplying neither drops both terms.
        ///     </para>
        ///     <para>
        ///         The upper bound is half-open against the next midnight
        ///         (<c>column &lt; DATEADD(day, 1, CAST(@to AS date))</c>) so that the whole end day is
        ///         included. The old library's code did the same; only its documentation claimed
        ///         <c>&lt;= to</c>.
        ///     </para>
        ///     <para>
        ///         <strong>The shift is emitted as SQL over the raw parameter, not computed in C#.</strong>
        ///         The old library baked <c>.Date.AddDays(1)</c> into a constant at rewrite time, which is
        ///         value-dependent; and even building it as C# expression nodes would not work here, because
        ///         <c>to.Value.Date</c> is a different variable identity from <c>to</c> and the original
        ///         expression contains only the latter - so the bound would freeze to the first execution's
        ///         date on every cache hit.
        ///     </para>
        /// </summary>
        private static Expression RewriteDateRange(Expression column, Expression from, Expression to)
        {
            var lowerBound = Optional(from, Expression.GreaterThanOrEqual(column, from));
            var upperBound = Optional(to, Expression.LessThan(column, new NextDayBoundaryExpression(to)));

            return Expression.AndAlso(lowerBound, upperBound);
        }
    }
}
