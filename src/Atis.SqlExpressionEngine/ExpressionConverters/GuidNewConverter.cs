using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    public class GuidNewConverterFactory : LinqToSqlExpressionConverterFactoryBase<MethodCallExpression>
    {
        public GuidNewConverterFactory() : base()
        {
        }

        /// <inheritdoc />
        public override bool TryCreate(IConverterDependencies converterDependencies, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is MethodCallExpression methodCall &&
                methodCall.Method.Name == nameof(Guid.NewGuid) &&
                methodCall.Method.DeclaringType == typeof(Guid))
            {
                var dependencies = this.GetConverterDependencies(converterDependencies);
                converter = new GuidNewConverter(dependencies, methodCall, converterStack);
                return true;
            }
            converter = null;
            return false;
        }
    }

    public class GuidNewConverter : LinqToNonSqlQueryConverterBase<MethodCallExpression>
    {
        public GuidNewConverter(LinqToSqlExpressionConverterDependencies dependencies, MethodCallExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converters) : base(dependencies, expression, converters)
        {
        }

        /// <inheritdoc />
        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            return this.SqlFactory.CreateNewGuid();
        }
    }
}
