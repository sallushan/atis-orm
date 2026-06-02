using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System.Linq;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    /// <summary>
    ///     <para>
    ///         Factory class for creating converters for GroupBy query method expressions.
    ///     </para>
    /// </summary>
    public class GroupByQueryMethodExpressionConverterFactory : QueryMethodExpressionConverterFactoryBase
    {
        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="GroupByQueryMethodExpressionConverterFactory"/> class.
        ///     </para>
        /// </summary>
        public GroupByQueryMethodExpressionConverterFactory() : base()
        {
        }

        /// <inheritdoc />
        protected override bool IsQueryMethodCall(MethodCallExpression methodCallExpression)
        {
            return methodCallExpression.Method.Name == nameof(Queryable.GroupBy);
        }

        /// <inheritdoc />
        protected override ExpressionConverterBase<Expression, SqlExpression> CreateConverter(IConverterDependencies converterDependencies, MethodCallExpression methodCallExpression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
        {
            var dependencies = this.GetConverterDependencies(converterDependencies);
            return new GroupByQueryMethodExpressionConverter(dependencies, methodCallExpression, converterStack);
        }
    }

    /// <summary>
    ///     <para>
    ///         Converter class for converting GroupBy query method expressions to SQL expressions.
    ///     </para>
    /// </summary>
    public class GroupByQueryMethodExpressionConverter : QueryMethodExpressionConverterBase
    {
        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="GroupByQueryMethodExpressionConverter"/> class.
        ///     </para>
        /// </summary>
        /// <param name="dependencies">The converter dependencies.</param>
        /// <param name="expression">The method call expression to be converted.</param>
        /// <param name="converterStack">The stack of converters representing the parent chain for context-aware conversion.</param>
        public GroupByQueryMethodExpressionConverter(LinqToSqlExpressionConverterDependencies dependencies, MethodCallExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(dependencies, expression, converterStack)
        {
        }

        /// <inheritdoc />
        protected override void OnArgumentConverted(ExpressionConverterBase<Expression, SqlExpression> childConverter, Expression argument, SqlExpression converterArgument)
        {
            if (argument == this.Expression.Arguments[1])   // group by selector done
            {
                if (this.Expression.Arguments.Count > 2)
                {
                    var arg2Param0 = this.Expression.GetArgLambdaParameterRequired(argIndex: 2, paramIndex: 0);
                    this.MapParameter(arg2Param0, () => this.SourceQuery.GetQueryShapeForDataSourceMapping());
                }
            }
            base.OnArgumentConverted(childConverter, argument, converterArgument);
        }

        /// <inheritdoc />
        protected override SqlExpression Convert(SqlSelectExpression sqlQuery, SqlExpression[] arguments)
        {
            var groupBy = arguments[0];
            sqlQuery.ApplyGroupBy(groupBy);
            if (arguments.Length > 1)
            {
                sqlQuery.UpdateModelBinding(arguments[1]);
            }
            return sqlQuery;
        }
    }
}
