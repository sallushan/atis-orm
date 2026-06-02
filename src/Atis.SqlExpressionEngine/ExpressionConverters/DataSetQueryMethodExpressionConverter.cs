using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    /// <summary>
    ///     <para>
    ///         Factory class for creating converters for DataSet query method expressions.
    ///     </para>
    /// </summary>
    public class DataSetQueryMethodExpressionConverterFactory : LinqToSqlExpressionConverterFactoryBase<MethodCallExpression>
    {
        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="DataSetQueryMethodExpressionConverterFactory"/> class.
        ///     </para>
        /// </summary>
        public DataSetQueryMethodExpressionConverterFactory() : base()
        {
        }

        public override IReadOnlyList<Type> GetConverterDependencyTypes()
        {
            return base.GetConverterDependencyTypes().Concat(new[] { typeof(IModel) }).ToArray();
        }

        /// <inheritdoc />
        public override bool TryCreate(IConverterDependencies converterDependencies, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is MethodCallExpression methodCallExpression &&
                    methodCallExpression.Method.Name == nameof(QueryExtensions.DataSet) &&
                    methodCallExpression.Method.DeclaringType == typeof(QueryExtensions))
            {
                var model = converterDependencies.GetRequired<IModel>();
                var d = this.GetConverterDependencies(converterDependencies);
                converter = new DataSetQueryMethodExpressionConverter(model, d, methodCallExpression, converterStack);
                return true;
            }
            converter = null;
            return false;
        }
    }

    /// <summary>
    ///     <para>
    ///         Converter class for converting DataSet query method expressions to SQL expressions.
    ///     </para>
    /// </summary>
    public class DataSetQueryMethodExpressionConverter : LinqToSqlQueryConverterBase<MethodCallExpression>
    {
        private readonly IModel model;

        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="DataSetQueryMethodExpressionConverter"/> class.
        ///     </para>
        /// </summary>
        /// <param name="dependencies">The conversion context.</param>
        /// <param name="expression">The method call expression to be converted.</param>
        /// <param name="converterStack">The stack of converters representing the parent chain for context-aware conversion.</param>
        public DataSetQueryMethodExpressionConverter(IModel model, LinqToSqlExpressionConverterDependencies dependencies, MethodCallExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(dependencies, expression, converterStack)
        {
            this.model = model;
        }

        /// <inheritdoc />
        public override bool TryOverrideChildConversion(Expression sourceExpression, out SqlExpression convertedExpression)
        {
            if (this.Expression.Arguments.FirstOrDefault() == sourceExpression)
            {
                convertedExpression = this.SqlFactory.CreateLiteral("dummy");
                return true;
            }
            convertedExpression = null;
            return false;
        }

        /// <inheritdoc />
        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            var sourceType = this.ReflectionService.GetElementType(this.Expression.Type);
            var entity = this.model.GetEntityRequired(sourceType);
            var table = this.SqlFactory.CreateTable(entity.Table, entity.SqlColumns);
            var result = this.SqlFactory.CreateSelectQuery(table);
            return result;
        }

        /// <inheritdoc />
        public override bool IsChainedQueryArgument(Expression childNode) => false;
    }
}
