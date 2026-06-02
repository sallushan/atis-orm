using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System.Linq;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    /// <summary>
    ///     <para>
    ///         Factory class for creating converters for FirstOrDefault query method expressions.
    ///     </para>
    /// </summary>
    public class FirstOrDefaultQueryMethodExpressionConverterFactory : QueryMethodExpressionConverterFactoryBase
    {
        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="FirstOrDefaultQueryMethodExpressionConverterFactory"/> class.
        ///     </para>
        /// </summary>
        public FirstOrDefaultQueryMethodExpressionConverterFactory() : base()
        {
        }

        /// <inheritdoc />
        protected override bool IsQueryMethodCall(MethodCallExpression methodCallExpression)
        {
            return methodCallExpression.Method.Name == nameof(Queryable.FirstOrDefault) ||
                    methodCallExpression.Method.Name == nameof(Queryable.First)
                        ;
        }

        /// <inheritdoc />
        protected override ExpressionConverterBase<Expression, SqlExpression> CreateConverter(IConverterDependencies converterDependencies, MethodCallExpression methodCallExpression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
        {
            var dependencies = this.GetConverterDependencies(converterDependencies);
            return new FirstOrDefaultQueryMethodExpressionConverter(dependencies, methodCallExpression, converterStack);
        }
    }

    /// <summary>
    ///     <para>
    ///         Converter class for converting FirstOrDefault query method expressions to SQL expressions.
    ///     </para>
    /// </summary>
    public class FirstOrDefaultQueryMethodExpressionConverter : QueryMethodExpressionConverterBase
    {
        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="FirstOrDefaultQueryMethodExpressionConverter"/> class.
        ///     </para>
        /// </summary>
        /// <param name="converterDependencies">The conversion context.</param>
        /// <param name="expression">The method call expression to be converted.</param>
        /// <param name="converterStack">The stack of converters representing the parent chain for context-aware conversion.</param>
        public FirstOrDefaultQueryMethodExpressionConverter(LinqToSqlExpressionConverterDependencies converterDependencies, MethodCallExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(converterDependencies, expression, converterStack)
        {
        }

        /// <inheritdoc />
        protected override SqlExpression Convert(SqlSelectExpression sqlQuery, SqlExpression[] arguments)
        {
            if (arguments.Length > 0)
            {
                var whereCondition = arguments[0];
                sqlQuery.ApplyWhere(whereCondition, useOrOperator: false);
            }
            sqlQuery.ApplyTop(1);
            return sqlQuery;
        }
    }
}
