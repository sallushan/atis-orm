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
    public class ToStringConverterFactory : LinqToSqlExpressionConverterFactoryBase<MethodCallExpression>
    {
        public ToStringConverterFactory() : base()
        {
        }

        public override IReadOnlyList<Type> GetConverterDependencyTypes()
        {
            return base.GetConverterDependencyTypes().Concat(new[] { typeof(ISqlDataTypeFactory) }).ToArray();
        }

        /// <inheritdoc/>
        public override bool TryCreate(IConverterDependencies converterDependencies, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is MethodCallExpression methodCall &&
                methodCall.Method.Name == nameof(ToString) &&
                ((methodCall.Arguments.Count == 1 && methodCall.Object == null) || (methodCall.Arguments.Count == 0 && methodCall.Object != null)))
            {
                var d = this.GetConverterDependencies(converterDependencies);
                var sqlDataTypeFactory = converterDependencies.GetRequired<ISqlDataTypeFactory>();
                converter = new ToStringConverter(sqlDataTypeFactory, d, methodCall, converterStack);
                return true;
            }
            converter = null;
            return false;
        }
    }

    public class ToStringConverter : LinqToNonSqlQueryConverterBase<MethodCallExpression>
    {
        private readonly ISqlDataTypeFactory sqlDataTypeFactory;

        public ToStringConverter(ISqlDataTypeFactory sqlDataTypeFactory, LinqToSqlExpressionConverterDependencies dependencies, MethodCallExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converters) : base(dependencies, expression, converters)
        {
            this.sqlDataTypeFactory = sqlDataTypeFactory;
        }

        /// <inheritdoc/>
        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            if (convertedChildren.Length == 0)
            {
                throw new ArgumentException("ToString method requires at least one argument.");
            }
            var sqlExpression = convertedChildren[0];
            // -1 means max length
            return this.SqlFactory.CreateCast(sqlExpression, this.sqlDataTypeFactory.CreateNonUnicodeString(-1));
        }
    }
}
