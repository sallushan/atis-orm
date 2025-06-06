﻿using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.ExpressionExtensions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    /// <summary>
    ///     <para>
    ///         Factory class for creating <see cref="DefaultIfEmptyExpressionConverter"/> instances.
    ///     </para>
    /// </summary>
    public class DefaultIfEmptyExpressionConverterFactory : LinqToSqlExpressionConverterFactoryBase<MethodCallExpression>
    {
        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="DefaultIfEmptyExpressionConverterFactory"/> class.
        ///     </para>
        /// </summary>
        /// <param name="context">The conversion context.</param>
        public DefaultIfEmptyExpressionConverterFactory(IConversionContext context) : base(context)
        {
        }

        /// <inheritdoc />
        public override bool TryCreate(Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is MethodCallExpression methodCallExpression && 
                methodCallExpression.Method.Name == nameof(Queryable.DefaultIfEmpty))
            {
                converter = new DefaultIfEmptyExpressionConverter(this.Context, methodCallExpression, converterStack);
                return true;
            }
            converter = null;
            return false;
        }
    }

    /// <summary>
    ///     <para>
    ///         Converter class for handling <see cref="Queryable.DefaultIfEmpty{TSource}(IQueryable{TSource})"/> method calls.
    ///     </para>
    /// </summary>
    public class DefaultIfEmptyExpressionConverter : LinqToNonSqlQueryConverterBase<MethodCallExpression>
    {
        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="DefaultIfEmptyExpressionConverter"/> class.
        ///     </para>
        /// </summary>
        /// <param name="context">The conversion context.</param>
        /// <param name="expression">The method call expression to be converted.</param>
        /// <param name="converterStack">The stack of converters representing the parent chain for context-aware conversion.</param>
        public DefaultIfEmptyExpressionConverter(IConversionContext context, MethodCallExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(context, expression, converterStack)
        {
        }

        /// <inheritdoc />
        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            var derivedTable = convertedChildren[0].CastTo<SqlDerivedTableExpression>();
            return this.SqlFactory.CreateDefaultIfEmpty(derivedTable);
        }
    }
}
