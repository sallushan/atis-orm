using System.Linq.Expressions;

using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.ExpressionExtensions;
using Atis.SqlExpressionEngine.SqlExpressions;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    /// <summary>
    ///     <para>
    ///         Creates the converter for <see cref="OptionalPredicateExpression"/>.
    ///     </para>
    /// </summary>
    public class OptionalPredicateExpressionConverterFactory : LinqToSqlExpressionConverterFactoryBase<OptionalPredicateExpression>
    {
        public OptionalPredicateExpressionConverterFactory() : base() { }

        public override bool TryCreate(IConverterDependencies converterDependencies, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is OptionalPredicateExpression optionalPredicate)
            {
                var dependencies = this.GetConverterDependencies(converterDependencies);
                converter = new OptionalPredicateExpressionConverter(dependencies, optionalPredicate, converterStack);
                return true;
            }

            converter = null;
            return false;
        }
    }

    /// <summary>
    ///     <para>
    ///         Converts <see cref="OptionalPredicateExpression"/> to
    ///         <see cref="SqlOptionalPredicateExpression"/>.
    ///     </para>
    ///     <para>
    ///         The guard converts like any other child, so a captured variable becomes a
    ///         <see cref="SqlParameterExpression"/> carrying its identity. It is converted separately from the
    ///         copy inside the predicate: the predicate's copy emits the placeholder, while the guard's copy
    ///         exists only so the translator can name the value the renderer tests.
    ///     </para>
    /// </summary>
    public class OptionalPredicateExpressionConverter : LinqToNonSqlQueryConverterBase<OptionalPredicateExpression>
    {
        public OptionalPredicateExpressionConverter(LinqToSqlExpressionConverterDependencies context, OptionalPredicateExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(context, expression, converterStack)
        {
        }

        /// <inheritdoc/>
        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            // Children are converted in the order OptionalPredicateExpression.VisitChildren visits them.
            var guard = convertedChildren[0];
            var predicate = convertedChildren[1];

            return this.SqlFactory.CreateOptionalPredicateExpression(guard, predicate, this.Expression.GuardKind);
        }
    }
}
