using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    public class CastQueryMethodExpressionConverterFactory : QueryMethodExpressionConverterFactoryBase
    {
        public CastQueryMethodExpressionConverterFactory() : base()
        {
        }

        protected override ExpressionConverterBase<Expression, SqlExpression> CreateConverter(IConverterDependencies converterDependencies, MethodCallExpression methodCallExpression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
        {
            var d = this.GetConverterDependencies(converterDependencies);
            return new CastQueryMethodExpressionConverter(d, methodCallExpression, converterStack);
        }

        protected override bool IsQueryMethodCall(MethodCallExpression methodCallExpression)
        {
            return methodCallExpression.Method.Name == nameof(Queryable.Cast) &&
                   (methodCallExpression.Method.DeclaringType == typeof(Queryable) ||
                   methodCallExpression.Method.DeclaringType == typeof(Enumerable));
        }
    }

    public class CastQueryMethodExpressionConverter : QueryMethodExpressionConverterBase
    {
        public CastQueryMethodExpressionConverter(LinqToSqlExpressionConverterDependencies converterDependencies, MethodCallExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack) 
            : base(converterDependencies, expression, converterStack)
        {
        }

        protected override SqlExpression Convert(SqlSelectExpression sqlQuery, SqlExpression[] arguments)
        {
            return sqlQuery;
        }
    }
}
