﻿using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
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
        /// <param name="context">The conversion context.</param>
        public DataSetQueryMethodExpressionConverterFactory(IConversionContext context) : base(context)
        {
        }

        /// <inheritdoc />
        public override bool TryCreate(Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is MethodCallExpression methodCallExpression &&
                    methodCallExpression.Method.Name == nameof(QueryExtensions.DataSet) &&
                    methodCallExpression.Method.DeclaringType == typeof(QueryExtensions))
            {
                converter = new DataSetQueryMethodExpressionConverter(this.Context, methodCallExpression, converterStack);
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
        /// <param name="context">The conversion context.</param>
        /// <param name="expression">The method call expression to be converted.</param>
        /// <param name="converterStack">The stack of converters representing the parent chain for context-aware conversion.</param>
        public DataSetQueryMethodExpressionConverter(IConversionContext context, MethodCallExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(context, expression, converterStack)
        {
            this.model = context.GetExtension<IModel>();
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
            var sourceType = this.ReflectionService.GetEntityTypeFromQueryableType(this.Expression.Type);
            var sqlTable = this.model.GetSqlTable(sourceType);
            var tableColumns = this.model.GetTableColumns(sourceType);
            var table = this.SqlFactory.CreateTable(sqlTable, tableColumns);
            var result = this.SqlFactory.CreateSelectQuery(table);
            return result;
        }

        /// <inheritdoc />
        public override bool IsChainedQueryArgument(Expression childNode) => false;
    }
}
