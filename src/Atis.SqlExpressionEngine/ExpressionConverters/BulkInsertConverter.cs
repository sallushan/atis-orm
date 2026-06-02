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
    public class BulkInsertConverterFactory : LinqToSqlExpressionConverterFactoryBase<MethodCallExpression>
    {
        public BulkInsertConverterFactory() : base()
        {
        }

        public override IReadOnlyList<Type> GetConverterDependencyTypes()
        {
            return base.GetConverterDependencyTypes().Concat(new[] { typeof(IModel) }).ToArray();
        }

        /// <inheritdoc />
        public override bool TryCreate(IConverterDependencies dependencyContainer, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is MethodCallExpression methodCall &&
                methodCall.Method.Name == nameof(QueryExtensions.BulkInsert) &&
                methodCall.Method.DeclaringType == typeof(QueryExtensions))
            {
                var dependencies = this.GetConverterDependencies(dependencyContainer);
                var model = dependencyContainer.GetRequired<IModel>();
                converter = new BulkInsertConverter(model, dependencies, methodCall, converterStack);
                return true;
            }
            converter = null;
            return false;
        }
    }
    public class BulkInsertConverter : LinqToNonSqlQueryConverterBase<MethodCallExpression>
    {
        private readonly IModel model;

        public BulkInsertConverter(IModel model, LinqToSqlExpressionConverterDependencies dependencies, MethodCallExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converters) : base(dependencies, expression, converters)
        {
            this.model = model;
        }

        /// <inheritdoc />
        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            var derivedTable = convertedChildren[0].CastTo<SqlDerivedTableExpression>("The first child must be a derived table for bulk insert.");
            var tableType = this.ReflectionService.GetElementType(this.Expression.Arguments[0].Type);
            var entity = this.model.GetEntityRequired(tableType);
            var bulkInsertExpression = this.SqlFactory.CreateInsertInto(entity.Table, entity.SqlColumns, derivedTable);
            return bulkInsertExpression;
        }
    }
}
