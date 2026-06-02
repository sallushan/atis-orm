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
    public class DistinctQueryMethodExpressionConverterFactory : QueryMethodExpressionConverterFactoryBase
    {
        public DistinctQueryMethodExpressionConverterFactory() : base()
        {
        }

        /// <inheritdoc />
        protected override ExpressionConverterBase<Expression, SqlExpression> CreateConverter(IConverterDependencies converterDependencies, MethodCallExpression methodCallExpression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
        {
            var dependencies = this.GetConverterDependencies(converterDependencies);
            return new DistinctQueryMethodExpressionConverter(dependencies, methodCallExpression, converterStack);
        }

        /// <inheritdoc />
        protected override bool IsQueryMethodCall(MethodCallExpression methodCallExpression) 
            => methodCallExpression.Method.Name == nameof(Queryable.Distinct);
    }

    public class DistinctQueryMethodExpressionConverter : QueryMethodExpressionConverterBase
    {
        public DistinctQueryMethodExpressionConverter(LinqToSqlExpressionConverterDependencies dependencies, MethodCallExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack) : base(dependencies, expression, converterStack)
        {
        }

        /// <inheritdoc />
        protected override SqlExpression Convert(SqlSelectExpression sqlQuery, SqlExpression[] arguments)
        {
            sqlQuery.ApplyDistinct();
            return sqlQuery;
        }
    }
}
