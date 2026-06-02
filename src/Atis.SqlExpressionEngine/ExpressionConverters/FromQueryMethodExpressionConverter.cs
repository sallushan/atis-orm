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
    ///         Factory for creating converters for the From query method.
    ///     </para>
    /// </summary>
    public class FromQueryMethodExpressionConverterFactory : LinqToSqlExpressionConverterFactoryBase<MethodCallExpression>
    {
        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="FromQueryMethodExpressionConverterFactory"/> class.
        ///     </para>
        /// </summary>
        public FromQueryMethodExpressionConverterFactory() : base()
        {
        }

        /// <inheritdoc />
        public override bool TryCreate(IConverterDependencies converterDependencies, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is MethodCallExpression methodCallExpression &&
                    methodCallExpression.Method.Name == nameof(QueryExtensions.From) &&
                    methodCallExpression.Method.DeclaringType == typeof(QueryExtensions))
            {
                var dependencies = this.GetConverterDependencies(converterDependencies);
                converter = new FromQueryMethodExpressionConverter(dependencies, methodCallExpression, converterStack);
                return true;
            }
            converter = null;
            return false;
        }
    }

    /// <summary>
    ///     <para>
    ///         Converter for the From query method.
    ///     </para>
    /// </summary>
    public class FromQueryMethodExpressionConverter : LinqToSqlQueryConverterBase<MethodCallExpression>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FromQueryMethodExpressionConverter"/> class.
        /// </summary>
        /// <param name="dependencies">The conversion dependencies.</param>
        /// <param name="expression">The method call expression to be converted.</param>
        /// <param name="converterStack">The stack of converters representing the parent chain for context-aware conversion.</param>
        public FromQueryMethodExpressionConverter(LinqToSqlExpressionConverterDependencies dependencies, MethodCallExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(dependencies, expression, converterStack)
        {
        }

        /// <inheritdoc />
        public override bool TryOverrideChildConversion(Expression sourceExpression, out SqlExpression convertedExpression)
        {
            if (this.Expression.Arguments.FirstOrDefault() == sourceExpression)
            {
                // 1st parameter will be IQueryProvider so we'll simply return dummy
                convertedExpression = this.SqlFactory.CreateLiteral("dummy");
                return true;
            }
            convertedExpression = null;
            return false;
        }

        /// <inheritdoc />
        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            // convertedChildren[0] will be dummy (for IQueryProvider)

            SqlExpression queryShape = (convertedChildren[1] as SqlQueryShapeExpression)
                                        ??
                                        (convertedChildren[1] as SqlQuerySourceExpression) as SqlExpression
                                        ??
                                        throw new InvalidOperationException($"convertedChildren[1] is not a SqlQueryShapeExpression or SqlQuerySourceExpression");


            var selectQuery = this.SqlFactory.CreateSelectQuery(queryShape);
            return selectQuery;
        }

        /// <inheritdoc />
        public override bool IsChainedQueryArgument(Expression childNode) => false;
    }
}
