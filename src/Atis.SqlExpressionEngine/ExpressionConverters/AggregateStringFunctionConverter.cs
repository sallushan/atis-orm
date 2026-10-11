using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    public class AggregateStringFunctionConverterFactory : LinqToSqlExpressionConverterFactoryBase<MethodCallExpression>
    {
        private readonly IReflectionService reflectionService;

        public AggregateStringFunctionConverterFactory(IReflectionService reflectionService) : base()
        {
            this.reflectionService = reflectionService ?? throw new ArgumentNullException(nameof(reflectionService));
        }

        /// <inheritdoc />
        public override bool TryCreate(IConverterDependencies converterDependencies, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is MethodCallExpression methodCallExpr &&
                (this.IsConcatMethodCall(methodCallExpr) ||
                this.IsJoinMethodCall(methodCallExpr)))
            {
                var dependencies = this.GetConverterDependencies(converterDependencies);
                converter = new AggregateStringFunctionConverter(dependencies, methodCallExpr, converterStack);
                return true;
            }
            converter = null;
            return false;
        }

        private bool IsConcatMethodCall(MethodCallExpression methodCallExpression)
        {
            if (methodCallExpression.Method.DeclaringType != typeof(string))
                return false;
            if (methodCallExpression.Method.Name != nameof(string.Concat))
                return false;

            var groupArgument = methodCallExpression.Arguments[0];
            
            return this.IsSelectOnGroupBy(groupArgument);
        }

        private bool IsJoinMethodCall(MethodCallExpression methodCallExpression)
        {
            if (methodCallExpression.Method.DeclaringType != typeof(string))
                return false;
            if (methodCallExpression.Method.Name != nameof(string.Join))
                return false;
            if (methodCallExpression.Arguments.Count < 2)
                return false;
            
            var groupArgument = methodCallExpression.Arguments[1];

            return this.IsSelectOnGroupBy(groupArgument);
        }

        private bool IsSelectOnGroupBy(Expression groupArgument)
        {
            if (!typeof(IEnumerable<string>).IsAssignableFrom(groupArgument.Type))
                return false;

            // string.Concat( groupQuery.Select(x => x.NonGroupField) )
            // Arguments[0] = groupQuery.Select(x => x.NonGroupField)
            // Arguments[0].Type = typeof(IQueryable<string>) or typeof(IEnumerable<string>)
            // Arguments[0].NodeType = ExpressionType.Call

            // string.Join(", ", groupQuery.OrderBy(x => x.Field).ThenByDescending(x => x.Other).Select(x => x.NonGroupField))
            // the ordering is optional and may be any chain of OrderBy / ThenBy over the group.
            if (groupArgument is MethodCallExpression selectMethodCallExpression &&
                selectMethodCallExpression.Method.Name == nameof(Enumerable.Select) &&
                this.IsGroupOrOrderedGroup(selectMethodCallExpression.Arguments?.FirstOrDefault()))
                return true;

            return false;
        }

        private bool IsGroupOrOrderedGroup(Expression source)
        {
            while (AggregateStringFunctionConverter.IsOrderingMethodCall(source))
                source = ((MethodCallExpression)source).Arguments[0];

            return source is ParameterExpression groupByParameter &&
                    this.reflectionService.IsGroupingType(groupByParameter.Type);
        }
    }

    public class AggregateStringFunctionConverter : LinqToNonSqlQueryConverterBase<MethodCallExpression>
    {
        private readonly LinqToSqlExpressionConverterDependencies dependencies;

        public AggregateStringFunctionConverter(LinqToSqlExpressionConverterDependencies dependencies, MethodCallExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack) 
            : base(dependencies, expression, converterStack)
        {
            this.dependencies = dependencies;
        }

        public override bool TryCreateChildConverter(Expression childNode, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> childConverter)
        {
            if ((this.Expression.Method.Name == nameof(string.Join) && childNode == this.Expression.Arguments[1]) ||
                (this.Expression.Method.Name == nameof(string.Concat) && childNode == this.Expression.Arguments[0]))
            {
                childConverter = new GroupBySelectMethodCallForStringAggregateConverter(this.dependencies, (MethodCallExpression)childNode, converterStack);
                return true;
            }
            return base.TryCreateChildConverter(childNode, converterStack, out childConverter);
        }

        internal static bool IsOrderingMethodCall(Expression expression)
        {
            return expression is MethodCallExpression call &&
                    call.Arguments.Count == 2 &&
                    (call.Method.DeclaringType == typeof(Enumerable) || call.Method.DeclaringType == typeof(Queryable)) &&
                    (call.Method.Name == nameof(Enumerable.OrderBy) ||
                     call.Method.Name == nameof(Enumerable.OrderByDescending) ||
                     call.Method.Name == nameof(Enumerable.ThenBy) ||
                     call.Method.Name == nameof(Enumerable.ThenByDescending));
        }

        /// <summary>
        ///     What the <c>Select</c> (and the <c>OrderBy</c> chain below it) hands to the string-aggregate
        ///     converter: the value being aggregated, if the <c>Select</c> has been reached, and the ordering.
        ///     It only lives between converters and is never part of the finished tree.
        /// </summary>
        private sealed class OrderedGroupOperand : SqlExpression
        {
            public OrderedGroupOperand(SqlSelectExpression groupQuery, SqlExpression value, IReadOnlyList<OrderByColumn> orderBy)
            {
                this.GroupQuery = groupQuery;
                this.Value = value;
                this.OrderBy = orderBy;
            }

            public SqlSelectExpression GroupQuery { get; }
            public SqlExpression Value { get; }
            public IReadOnlyList<OrderByColumn> OrderBy { get; }
        }

        /// <inheritdoc />
        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            if (this.Expression.Method.Name == nameof(string.Join))
            {
                // string.Join(", ", groupQuery.Select(x => x.NonGroupField))
                // convertedChildren[0] = separator
                // convertedChildren[1] = x.NonGroupField
                // That's why we are passing [1] in the 1st argument of CreateStringFunction because that's the
                // argument on which function is applied, rest of the arguments are just helping arguments.
                // When the group was ordered, the ordering follows the separator as a SqlOrderByClauseExpression.
                var value = SplitOrdering(convertedChildren[1], out var orderBy);
                var arguments = orderBy == null
                    ? new[] { convertedChildren[0] }
                    : new[] { convertedChildren[0], orderBy };
                return this.SqlFactory.CreateStringFunction(SqlStringFunction.JoinAggregate, value, arguments);
            }
            else if (this.Expression.Method.Name == nameof(string.Concat))
            {
                var value = SplitOrdering(convertedChildren[0], out var orderBy);
                return this.SqlFactory.CreateStringFunction(SqlStringFunction.ConcatAggregate, value, orderBy == null ? null : new SqlExpression[] { orderBy });
            }

            throw new InvalidOperationException($"Aggregate string function '{this.Expression.Method.Name}' is not supported.");
        }

        private static SqlExpression SplitOrdering(SqlExpression operand, out SqlOrderByClauseExpression orderBy)
        {
            if (operand is OrderedGroupOperand ordered)
            {
                orderBy = ordered.OrderBy?.Count > 0 ? new SqlOrderByClauseExpression(ordered.OrderBy) : null;
                return ordered.Value;
            }
            orderBy = null;
            return operand;
        }


        private class GroupBySelectMethodCallForStringAggregateConverter : LinqToNonSqlQueryConverterBase<MethodCallExpression>
        {
            private readonly LinqToSqlExpressionConverterDependencies dependencies;
            private IReadOnlyList<OrderByColumn> orderBy;

            public GroupBySelectMethodCallForStringAggregateConverter(LinqToSqlExpressionConverterDependencies dependencies, MethodCallExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converters) : base(dependencies, expression, converters)
            {
                this.dependencies = dependencies;
            }

            /// <inheritdoc />
            public override bool TryCreateChildConverter(Expression childNode, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> childConverter)
            {
                // The ordering of the group is carried to the aggregate, not applied to the group query, so
                // the usual OrderBy converter (which would add an ORDER BY to the GROUP BY query) is not used.
                if (childNode == this.Expression.Arguments[0] && IsOrderingMethodCall(childNode))
                {
                    childConverter = new GroupOrderByForStringAggregateConverter(this.dependencies, (MethodCallExpression)childNode, converterStack);
                    return true;
                }
                return base.TryCreateChildConverter(childNode, converterStack, out childConverter);
            }

            /// <inheritdoc />
            public override void OnConversionCompletedByChild(ExpressionConverterBase<Expression, SqlExpression> childConverter, Expression childNode, SqlExpression convertedExpression)
            {
                // Select(x, y => y.NonGroupingField)

                if (childNode == this.Expression.Arguments[0])      // childNode = x
                {
                    SqlSelectExpression sqlQuery;
                    if (convertedExpression is OrderedGroupOperand ordered)
                    {
                        sqlQuery = ordered.GroupQuery;
                        this.orderBy = ordered.OrderBy;
                    }
                    else
                        sqlQuery = convertedExpression.CastTo<SqlSelectExpression>();

                    var arg1Param0 = this.Expression.GetArgLambdaParameterRequired(argIndex: 1, paramIndex: 0);
                    this.MapParameter(arg1Param0, () => sqlQuery.GetQueryShapeForFieldMapping());
                }
            }

            /// <inheritdoc />
            public override SqlExpression Convert(SqlExpression[] convertedChildren)
            {
                if (this.orderBy == null)
                    return convertedChildren[1];
                return new OrderedGroupOperand(null, convertedChildren[1], this.orderBy);
            }
        }

        /// <summary>
        ///     <c>OrderBy</c> / <c>ThenBy</c> (and their descending forms) applied to a group, ahead of the
        ///     <c>Select</c> of a string aggregate. It collects the ordering and leaves the group query as it is.
        /// </summary>
        private class GroupOrderByForStringAggregateConverter : LinqToNonSqlQueryConverterBase<MethodCallExpression>
        {
            private readonly LinqToSqlExpressionConverterDependencies dependencies;
            private SqlSelectExpression groupQuery;
            private IReadOnlyList<OrderByColumn> previous = new OrderByColumn[0];

            public GroupOrderByForStringAggregateConverter(LinqToSqlExpressionConverterDependencies dependencies, MethodCallExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converters) : base(dependencies, expression, converters)
            {
                this.dependencies = dependencies;
            }

            /// <inheritdoc />
            public override bool TryCreateChildConverter(Expression childNode, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> childConverter)
            {
                if (childNode == this.Expression.Arguments[0] && IsOrderingMethodCall(childNode))
                {
                    childConverter = new GroupOrderByForStringAggregateConverter(this.dependencies, (MethodCallExpression)childNode, converterStack);
                    return true;
                }
                return base.TryCreateChildConverter(childNode, converterStack, out childConverter);
            }

            /// <inheritdoc />
            public override void OnConversionCompletedByChild(ExpressionConverterBase<Expression, SqlExpression> childConverter, Expression childNode, SqlExpression convertedExpression)
            {
                if (childNode == this.Expression.Arguments[0])      // childNode = x, or the OrderBy below this one
                {
                    if (convertedExpression is OrderedGroupOperand ordered)
                    {
                        this.groupQuery = ordered.GroupQuery;
                        this.previous = ordered.OrderBy;
                    }
                    else
                        this.groupQuery = convertedExpression.CastTo<SqlSelectExpression>();

                    var arg1Param0 = this.Expression.GetArgLambdaParameterRequired(argIndex: 1, paramIndex: 0);
                    this.MapParameter(arg1Param0, () => this.groupQuery.GetQueryShapeForFieldMapping());
                }
            }

            /// <inheritdoc />
            public override SqlExpression Convert(SqlExpression[] convertedChildren)
            {
                var name = this.Expression.Method.Name;
                var descending = name == nameof(Enumerable.OrderByDescending) || name == nameof(Enumerable.ThenByDescending);
                var column = new OrderByColumn(convertedChildren[1], descending ? SortDirection.Descending : SortDirection.Ascending);

                // As in LINQ, a new OrderBy starts the ordering over; ThenBy adds to it.
                var isThen = name == nameof(Enumerable.ThenBy) || name == nameof(Enumerable.ThenByDescending);
                var columns = new List<OrderByColumn>(isThen ? this.previous : new OrderByColumn[0]) { column };
                return new OrderedGroupOperand(this.groupQuery, null, columns);
            }
        }
    }
}
