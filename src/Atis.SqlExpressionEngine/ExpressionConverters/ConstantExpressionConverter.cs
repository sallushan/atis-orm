﻿using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    /// <summary>
    ///     <para>
    ///         Factory for creating converters that handle constant expressions.
    ///     </para>
    /// </summary>
    public class ConstantExpressionConverterFactory : LinqToSqlExpressionConverterFactoryBase<ConstantExpression>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConstantExpressionConverterFactory"/> class.
        /// </summary>
        /// <param name="context">The conversion context.</param>
        public ConstantExpressionConverterFactory(IConversionContext context) : base(context)
        {
        }

        /// <inheritdoc />
        public override bool TryCreate(Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is ConstantExpression constExpression)
            {
                converter = new ConstantExpressionConverter(this.Context, constExpression, converterStack);
                return true;
            }
            converter = null;
            return false;
        }
    }

    /// <summary>
    ///     <para>
    ///         Converter for transforming constant expressions to SQL parameter expressions.
    ///     </para>
    /// </summary>
    public class ConstantExpressionConverter : LinqToNonSqlQueryConverterBase<ConstantExpression>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConstantExpressionConverter"/> class.
        /// </summary>
        /// <param name="context">The conversion context.</param>
        /// <param name="constantExpression">The constant expression to be converted.</param>
        /// <param name="converterStack">The stack of converters representing the parent chain for context-aware conversion.</param>
        public ConstantExpressionConverter(IConversionContext context, ConstantExpression constantExpression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(context, constantExpression, converterStack)
        {
        }

        /// <inheritdoc />
        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            return this.SqlFactory.CreateLiteral(this.Expression.Value);
        }
    }
}
