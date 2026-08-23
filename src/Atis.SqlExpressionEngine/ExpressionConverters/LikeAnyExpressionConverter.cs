using System.Linq.Expressions;

using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.ExpressionExtensions;
using Atis.SqlExpressionEngine.SqlExpressions;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    /// <summary>
    ///     <para>Creates the converter for <see cref="LikeAnyExpression"/>.</para>
    /// </summary>
    public class LikeAnyExpressionConverterFactory : LinqToSqlExpressionConverterFactoryBase<LikeAnyExpression>
    {
        /// <summary>Creates the factory.</summary>
        public LikeAnyExpressionConverterFactory() : base() { }

        /// <inheritdoc />
        public override bool TryCreate(IConverterDependencies converterDependencies, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is LikeAnyExpression likeAny)
            {
                var dependencies = this.GetConverterDependencies(converterDependencies);
                converter = new LikeAnyExpressionConverter(dependencies, likeAny, converterStack);
                return true;
            }

            converter = null;
            return false;
        }
    }

    /// <summary>
    ///     <para>
    ///         Converts <see cref="LikeAnyExpression"/> to <see cref="SqlLikeAnyExpression"/>.
    ///     </para>
    ///     <para>
    ///         The collection converts like any other child, so a captured variable becomes a
    ///         <see cref="SqlParameterExpression"/> carrying its identity - and it is kept whole. Splitting it
    ///         into one expression per element would need the collection's length, which is not knowable here:
    ///         the same compiled query is re-executed with collections of different lengths.
    ///     </para>
    /// </summary>
    public class LikeAnyExpressionConverter : LinqToNonSqlQueryConverterBase<LikeAnyExpression>
    {
        /// <summary>Creates the converter.</summary>
        public LikeAnyExpressionConverter(LinqToSqlExpressionConverterDependencies context, LikeAnyExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(context, expression, converterStack)
        {
        }

        /// <inheritdoc/>
        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            // Children are converted in the order LikeAnyExpression.VisitChildren visits them.
            var stringExpression = convertedChildren[0];
            var values = convertedChildren[1];

            return this.SqlFactory.CreateLikeAny(stringExpression, values, this.Expression.MatchMode);
        }
    }
}
