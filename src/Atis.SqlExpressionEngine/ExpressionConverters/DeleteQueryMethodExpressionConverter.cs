using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    public class DeleteQueryMethodExpressionConverterFactory : QueryMethodExpressionConverterFactoryBase
    {
        public DeleteQueryMethodExpressionConverterFactory() : base()
        {
        }

        /// <inheritdoc />
        protected override ExpressionConverterBase<Expression, SqlExpression> CreateConverter(IConverterDependencies converterDependencies, MethodCallExpression methodCallExpression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
        {
            var dependencies = this.GetConverterDependencies(converterDependencies);
            return new DeleteQueryMethodExpressionConverter(dependencies, methodCallExpression, converterStack);
        }

        /// <inheritdoc />
        protected override bool IsQueryMethodCall(MethodCallExpression methodCallExpression)
        {
            return methodCallExpression.Method.Name == nameof(QueryExtensions.Delete) &&
                     methodCallExpression.Method.DeclaringType == typeof(QueryExtensions);
        }
    }
    public class DeleteQueryMethodExpressionConverter : DataManipulationQueryMethodExpressionConverterBase
    {
        public DeleteQueryMethodExpressionConverter(LinqToSqlExpressionConverterDependencies dependencies, MethodCallExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack) : base(dependencies, expression, converterStack)
        {
        }

        /// <inheritdoc />
        protected override bool HasTableSelection => this.Expression.Arguments.Count == 3;

        /// <inheritdoc />
        protected override int WherePredicateArgumentIndex => this.Expression.Arguments.Count == 3 ? 2 : 1;

        /// <inheritdoc />
        protected override SqlExpression CreateDmSqlExpression(SqlDerivedTableExpression source, Guid selectedDataSource, IReadOnlyList<SqlExpression> arguments)
        {
            var deleteSqlExpression = this.SqlFactory.CreateDelete(source, selectedDataSource);
            return deleteSqlExpression;
        }
    }
}
