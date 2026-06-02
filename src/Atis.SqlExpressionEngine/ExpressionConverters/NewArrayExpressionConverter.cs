using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    public class NewArrayExpressionConverterFactory : LinqToSqlExpressionConverterFactoryBase<NewArrayExpression>
    {
        public NewArrayExpressionConverterFactory() : base() { }

        public override bool TryCreate(IConverterDependencies converterDependencies, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is NewArrayExpression newArrayExpr)
            {
                var d = this.GetConverterDependencies(converterDependencies);
                converter = new NewArrayExpressionConverter(d, newArrayExpr, converterStack);
                return true;
            }

            converter = null;
            return false;
        }
    }

    public class NewArrayExpressionConverter : LinqToNonSqlQueryConverterBase<NewArrayExpression>
    {
        public NewArrayExpressionConverter(LinqToSqlExpressionConverterDependencies dependencies, NewArrayExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(dependencies, expression, converterStack)
        {
        }

        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            return new SqlCollectionExpression(convertedChildren);
        }
    }
}
