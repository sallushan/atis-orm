using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
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
            // A NewArrayBounds node (`new int[3]`) carries no elements - its children are the *bounds*, and
            // the elements are all defaults. Converting it here would turn the bound into a value:
            // `new int[3].Contains(x.Id)` would emit `IN (3)` instead of `IN (0, 0, 0)`. Wrong SQL with no
            // error anywhere, and a correct translation would be useless too (three zeros is just
            // `x.Id == 0`), so the node type is refused outright rather than made to work.
            if (this.Expression.NodeType == ExpressionType.NewArrayBounds)
                throw new InvalidOperationException(
                    $"'{this.Expression}' creates an array by length, so it has no values to translate. " +
                    $"List the values explicitly (new[] {{ a, b }}) or use a collection variable.");

            return new SqlCollectionExpression(convertedChildren);
        }
    }
}
