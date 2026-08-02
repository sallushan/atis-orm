using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.ExpressionExtensions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    /// <summary>
    ///     Creates the converter for a <see cref="CollectionExpression"/>.
    /// </summary>
    public class CollectionExpressionConverterFactory : LinqToSqlExpressionConverterFactoryBase<CollectionExpression>
    {
        /// <inheritdoc />
        public override bool TryCreate(IConverterDependencies converterDependencies, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is CollectionExpression collectionExpression)
            {
                var d = this.GetConverterDependencies(converterDependencies);
                converter = new CollectionExpressionConverter(d, collectionExpression, converterStack);
                return true;
            }
            converter = null;
            return false;
        }
    }

    /// <summary>
    ///     Converts a <see cref="CollectionExpression"/> to its SQL counterpart,
    ///     <see cref="SqlCollectionExpression"/>.
    /// </summary>
    public class CollectionExpressionConverter : LinqToNonSqlQueryConverterBase<CollectionExpression>
    {
        /// <inheritdoc />
        public CollectionExpressionConverter(LinqToSqlExpressionConverterDependencies context, CollectionExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(context, expression, converterStack)
        {
        }

        /// <inheritdoc />
        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            return new SqlCollectionExpression(convertedChildren);
        }
    }
}
