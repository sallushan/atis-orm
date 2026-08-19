using System.Linq.Expressions;

using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.ExpressionExtensions;
using Atis.SqlExpressionEngine.SqlExpressions;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    /// <summary>
    ///     <para>Creates the converter for <see cref="LikePatternExpression"/>.</para>
    /// </summary>
    public class LikePatternExpressionConverterFactory : LinqToSqlExpressionConverterFactoryBase<LikePatternExpression>
    {
        public LikePatternExpressionConverterFactory() : base() { }

        public override bool TryCreate(IConverterDependencies converterDependencies, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is LikePatternExpression likePattern)
            {
                var dependencies = this.GetConverterDependencies(converterDependencies);
                converter = new LikePatternExpressionConverter(dependencies, likePattern, converterStack);
                return true;
            }

            converter = null;
            return false;
        }
    }

    /// <summary>
    ///     <para>
    ///         Converts <see cref="LikePatternExpression"/> to a <see cref="SqlLikeExpression"/> of kind
    ///         <see cref="SqlExpressionType.LikePattern"/>, i.e. <c>expr LIKE pattern</c> with no decoration.
    ///     </para>
    /// </summary>
    public class LikePatternExpressionConverter : LinqToNonSqlQueryConverterBase<LikePatternExpression>
    {
        public LikePatternExpressionConverter(LinqToSqlExpressionConverterDependencies context, LikePatternExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(context, expression, converterStack)
        {
        }

        /// <inheritdoc/>
        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            return this.SqlFactory.CreateLikePattern(convertedChildren[0], convertedChildren[1]);
        }
    }
}
