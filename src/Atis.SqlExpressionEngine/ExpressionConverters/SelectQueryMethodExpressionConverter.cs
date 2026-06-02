using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.Internal;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.ExpressionConverters
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
        public SelectQueryMethodExpressionConverterFactory() : base()
        {
        }

        /// <inheritdoc />
        protected override bool IsQueryMethodCall(MethodCallExpression methodCallExpression)
        {
            return methodCallExpression.Method.Name == nameof(Queryable.Select)
                    &&
                    (methodCallExpression.Method.DeclaringType == typeof(Queryable) ||
                        methodCallExpression.Method.DeclaringType == typeof(Enumerable));
        }

        /// <inheritdoc />
        protected override ExpressionConverterBase<Expression, SqlExpression> CreateConverter(IConverterDependencies converterDependencies, MethodCallExpression methodCallExpression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
        {
            var d = this.GetConverterDependencies(converterDependencies);
            return new SelectQueryMethodExpressionConverter(d, methodCallExpression, converterStack);
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
        /// <param name="dependencies">The conversion dependencies.</param>
        /// <param name="expression">The method call expression to be converted.</param>
        /// <param name="converterStack">The stack of converters representing the parent chain for context-aware conversion.</param>
        public SelectQueryMethodExpressionConverter(LinqToSqlExpressionConverterDependencies dependencies, MethodCallExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(dependencies, expression, converterStack)
        {
        }

        /// <inheritdoc />
        protected override SqlExpression Convert(SqlSelectExpression sqlQuery, SqlExpression[] arguments)
        {
            sqlQuery.ApplyProjection(arguments[0]);
            return sqlQuery;
        }
    }
}
