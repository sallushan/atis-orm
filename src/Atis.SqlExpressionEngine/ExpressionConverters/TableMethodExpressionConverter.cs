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
    ///         Factory class for creating <see cref="TableExpressionConverter"/> instances.
    ///     </para>
    /// </summary>
    public class TableExpressionConverterFactory : LinqToSqlExpressionConverterFactoryBase<MethodCallExpression>
    {
        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="TableExpressionConverterFactory"/> class.
        ///     </para>
        /// </summary>
        /// <param name="context">The conversion context.</param>
        public TableExpressionConverterFactory() : base()
        {
        }

        public override IReadOnlyList<Type> GetConverterDependencyTypes()
        {
            return base.GetConverterDependencyTypes().Concat(new Type[] { typeof(IModel) }).ToArray();
        }

        /// <inheritdoc />
        public override bool TryCreate(IConverterDependencies converterDependencies, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is MethodCallExpression methodCallExpression &&
                    methodCallExpression.Method.IsGenericMethod &&
                    methodCallExpression.Method.GetGenericArguments().Length > 0 &&
                    methodCallExpression.Method.Name == nameof(QueryExtensions.Table) &&
                    methodCallExpression.Method.DeclaringType == typeof(QueryExtensions))
            {
                var d = this.GetConverterDependencies(converterDependencies);
                var model = converterDependencies.GetRequired<IModel>();
                converter = new TableExpressionConverter(model, d, methodCallExpression, converterStack);
                return true;
            }
            converter = null;
            return false;
        }
    }
    /// <summary>
    ///     <para>
    ///         Converter class for converting method call expressions to SQL table expressions.
    ///     </para>
    /// </summary>
    public class TableExpressionConverter : LinqToNonSqlQueryConverterBase<MethodCallExpression>
    {
        private readonly IModel model;

        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="TableExpressionConverter"/> class.
        ///     </para>
        /// </summary>
        /// <param name="model">The model containing entity metadata.</param>
        /// <param name="dependencies">The conversion dependencies.</param>
        /// <param name="expression">The method call expression to be converted.</param>
        /// <param name="converterStack">The stack of converters representing the parent chain for context-aware conversion.</param>
        public TableExpressionConverter(IModel model, LinqToSqlExpressionConverterDependencies dependencies, MethodCallExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(dependencies, expression, converterStack)
        {
            this.model = model;
        }

        /// <inheritdoc />
        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            if (!this.Expression.Method.IsGenericMethod)
                throw new System.InvalidOperationException("Table method must be generic method.");
            var genericArg0 = this.Expression.Method.GetGenericArguments().FirstOrDefault()
                                ??
                                throw new System.InvalidOperationException("Table method must have at least one generic argument.");
            var entity = this.model.GetEntityRequired(genericArg0);
            return this.SqlFactory.CreateTable(entity.Table, entity.SqlColumns);
        }
    }
}
