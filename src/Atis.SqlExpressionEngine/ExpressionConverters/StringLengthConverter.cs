﻿using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    /// <summary>
    ///     <para>
    ///         Factory class for creating converters that handle string length expressions.
    ///     </para>
    /// </summary>
    public class StringLengthExpressionConverterFactory : LinqToSqlExpressionConverterFactoryBase<MemberExpression>
    {
        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="StringLengthExpressionConverterFactory"/> class.
        ///     </para>
        /// </summary>
        /// <param name="context">The conversion context.</param>
        public StringLengthExpressionConverterFactory(IConversionContext context) : base(context)
        {
        }

        /// <inheritdoc />
        public override bool TryCreate(Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is MemberExpression memberExpression &&
                        memberExpression.Member.Name == nameof(string.Length) &&
                        memberExpression.Member.DeclaringType == typeof(string))
            {
                converter = new StringLengthConverter(this.Context, memberExpression, converterStack);
                return true;
            }
            converter = null;
            return false;
        }
    }

    /// <summary>
    ///     <para>
    ///         Converter class for handling string length expressions.
    ///     </para>
    /// </summary>
    public class StringLengthConverter : LinqToNonSqlQueryConverterBase<MemberExpression>
    {
        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="StringLengthConverter"/> class.
        ///     </para>
        /// </summary>
        /// <param name="context">The conversion context.</param>
        /// <param name="expression">The source expression to be converted.</param>
        /// <param name="converterStack">The stack of converters representing the parent chain for context-aware conversion.</param>
        public StringLengthConverter(IConversionContext context, MemberExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(context, expression, converterStack)
        {
        }

        /// <inheritdoc />
        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            var sourceExpression = convertedChildren[0];
            return this.SqlFactory.CreateStringFunction(SqlStringFunction.CharLength, sourceExpression, arguments: null);
        }
    }
}
