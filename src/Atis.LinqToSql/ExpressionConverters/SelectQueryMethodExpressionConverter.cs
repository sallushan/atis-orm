﻿using Atis.Expressions;
using Atis.LinqToSql.ContextExtensions;
using Atis.LinqToSql.Internal;
using Atis.LinqToSql.SqlExpressions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Atis.LinqToSql.ExpressionConverters
{
    /// <summary>
    ///     <para>
    ///         Factory class for creating converters for Select query method expressions.
    ///     </para>
    /// </summary>
    public class SelectQueryMethodExpressionConverterFactory : QueryMethodExpressionConverterFactoryBase
    {
        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="SelectQueryMethodExpressionConverterFactory"/> class.
        ///     </para>
        /// </summary>
        /// <param name="context">The conversion context.</param>
        public SelectQueryMethodExpressionConverterFactory(IConversionContext context) : base(context)
        {
        }

        /// <inheritdoc />
        protected override bool IsQueryMethodCall(MethodCallExpression methodCallExpression)
        {
            return methodCallExpression.Method.Name == nameof(Queryable.Select);
        }

        /// <inheritdoc />
        protected override ExpressionConverterBase<Expression, SqlExpression> CreateConverter(MethodCallExpression methodCallExpression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
        {
            return new SelectQueryMethodExpressionConverter(this.Context, methodCallExpression, converterStack);
        }
    }

    /// <summary>
    ///     <para>
    ///         Converter class for Select query method expressions.
    ///     </para>
    /// </summary>
    public class SelectQueryMethodExpressionConverter : QueryMethodExpressionConverterBase
    {
        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="SelectQueryMethodExpressionConverter"/> class.
        ///     </para>
        /// </summary>
        /// <param name="context">The conversion context.</param>
        /// <param name="expression">The method call expression to be converted.</param>
        /// <param name="converterStack">The stack of converters representing the parent chain for context-aware conversion.</param>
        public SelectQueryMethodExpressionConverter(IConversionContext context, MethodCallExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(context, expression, converterStack)
        {
        }

        /// <inheritdoc />
        protected override SqlExpression Convert(SqlQueryExpression sqlQuery, SqlExpression[] arguments)
        {
            var selector = arguments[0];
            if (selector is SqlCollectionExpression sqlCollection && !sqlCollection.SqlExpressions.Any(x => x is SqlColumnExpression))
            {
                var projectionCreator = new ProjectionCreator();
                var sqlColumns = projectionCreator.Create(sqlCollection);
                selector = new SqlCollectionExpression(sqlColumns);
            }
            sqlQuery.ApplyProjection(selector);
            return sqlQuery;
        }
    }
}
