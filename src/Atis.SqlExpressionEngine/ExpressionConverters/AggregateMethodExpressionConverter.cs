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
    ///         Factory class for creating converters that handle aggregate method expressions.
    ///     </para>
    /// </summary>
    public class AggregateMethodExpressionConverterFactory : LinqToSqlExpressionConverterFactoryBase<MethodCallExpression>
    {

        /// <inheritdoc />
        public override bool TryCreate(IConverterDependencies converterDependencies, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            var aggregateMethodNames = new[] { nameof(Queryable.Count), nameof(Queryable.LongCount), nameof(Queryable.Average), nameof(Queryable.Max), nameof(Queryable.Min), nameof(Queryable.Sum) };
            // The declaring type matters: a provider function that happens to be called Sum or Count
            // (e.g. SqlServerFunctions.Sum(x)) takes a value, not a query, and must not be claimed here.
            if (expression is MethodCallExpression methodCallExpr &&
                    (methodCallExpr.Method.DeclaringType == typeof(Queryable) || methodCallExpr.Method.DeclaringType == typeof(Enumerable)) &&
                    aggregateMethodNames.Contains(methodCallExpr.Method.Name))
            {
                var dependencies = this.GetConverterDependencies(converterDependencies);
                converter = new AggregateMethodExpressionConverter(dependencies, methodCallExpr, converterStack);
                return true;
            }
            converter = null;
            return false;
        }
    }

    /// <summary>
    ///     <para>
    ///         Converter class for handling aggregate method expressions.
    ///     </para>
    /// </summary>
    public class AggregateMethodExpressionConverter : LinqToNonSqlQueryConverterBase<MethodCallExpression>
    {
        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="AggregateMethodExpressionConverter"/> class.
        ///     </para>
        /// </summary>
        /// <param name="dependencies">The conversion dependencies.</param>
        /// <param name="expression">The method call expression to be converted.</param>
        /// <param name="converterStack">The stack of converters representing the parent chain for context-aware conversion.</param>
        public AggregateMethodExpressionConverter(LinqToSqlExpressionConverterDependencies dependencies, MethodCallExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(dependencies, expression, converterStack)
        {
        }

        private SqlSelectExpression sourceQuery;
        private bool applyProjection;

        /// <inheritdoc />
        public override void OnConversionCompletedByChild(ExpressionConverterBase<Expression, SqlExpression> childConverter, Expression childNode, SqlExpression convertedExpression)
        {
            if (this.Expression.Arguments[0] == childNode)    // if 1st arg was converted
            {
                if (convertedExpression is SqlSelectExpression q)
                {
                    this.sourceQuery = q;
                    applyProjection = false;
                }
                else if (convertedExpression is SqlDerivedTableExpression d)
                {
                    this.sourceQuery = this.SqlFactory.CreateSelectQuery(d);
                    applyProjection = true;
                }
                else
                    throw new InvalidOperationException($"Expected a {nameof(SqlSelectExpression)} or {nameof(SqlDerivedTableExpression)} but got {convertedExpression.GetType().Name}.");

                // mapping given query with next Lambda Parameter (if available)
                if (this.Expression.TryGetArgLambdaParameter(argIndex: 1, paramIndex: 0, out var arg1Param0))
                {
                    this.MapParameter(arg1Param0, () => this.sourceQuery.GetQueryShapeForFieldMapping());
                }
            }
        }

        /// <inheritdoc/>
        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            // skip(1) because 1st argument might be a x
            // e.g.  x.Max(y => y.Field)
            // this will be Max(x, y => y.Field)
            // so we'll skip the 1st argument
            var methodArguments = convertedChildren.Skip(1).ToArray();

            // Count(predicate) must count only the rows that match. A condition used as a value would
            // become CASE WHEN c THEN 1 ELSE 0 END, and COUNT counts every non-NULL value (0 included),
            // so it would count all rows. Count(CASE WHEN c THEN 1 END) has no ELSE, so a row that does
            // not match gives NULL and COUNT skips it.
            if (this.Expression.Method.Name == nameof(Queryable.Count) && methodArguments.Length == 1)
            {
                methodArguments[0] = this.SqlFactory.CreateCondition(methodArguments[0], this.SqlFactory.CreateLiteral(1), ifFalse: null);
            }

            // q.Select(y => y.Age).Sum() has no selector: the value is the single column of the source query.
            // Without this the function was written with no argument at all (Sum()).
            if (methodArguments.Length == 0 && applyProjection &&
                this.Expression.Method.Name != nameof(Queryable.Count) && this.Expression.Method.Name != nameof(Queryable.LongCount))
            {
                var shape = this.sourceQuery.GetQueryShapeForFieldMapping();
                if (shape is SqlQueryShapeExpression queryShape)
                {
                    if (!queryShape.IsScalar)
                        throw new InvalidOperationException($"{this.Expression.Method.Name}() without a selector needs a query that selects a single value, e.g. Select(x => x.Field).{this.Expression.Method.Name}().");
                    shape = queryShape.GetScalarExpression();
                }
                methodArguments = new[] { shape };
            }

            // LINQ's Average of ints returns double, but SQL AVG of an int column is an int (it truncates),
            // and the reader expects a double. Casting the argument to float makes SQL agree with LINQ.
            if (this.Expression.Method.Name == nameof(Queryable.Average) && methodArguments.Length == 1 &&
                (this.Expression.Method.ReturnType == typeof(double) || this.Expression.Method.ReturnType == typeof(double?)))
            {
                var floatType = new SqlDataType(typeof(double), "float", length: null, isNullable: false, isUnicode: false, precision: null, scale: null, useMaxLength: false);
                methodArguments[0] = this.SqlFactory.CreateCast(methodArguments[0], floatType);
            }

            // The provider decides how each function is spelled; the engine only says which one it is.
            SqlAggregateFunction function;
            switch (this.Expression.Method.Name)
            {
                case nameof(Queryable.Count): function = SqlAggregateFunction.Count; break;
                case nameof(Queryable.LongCount): function = SqlAggregateFunction.CountBig; break;
                case nameof(Queryable.Sum): function = SqlAggregateFunction.Sum; break;
                case nameof(Queryable.Min): function = SqlAggregateFunction.Min; break;
                case nameof(Queryable.Max): function = SqlAggregateFunction.Max; break;
                case nameof(Queryable.Average): function = SqlAggregateFunction.Avg; break;
                default: throw new NotSupportedException($"'{this.Expression.Method.Name}' is not a supported aggregate method.");
            }

            SqlExpression result;
            var functionCallExpression = this.SqlFactory.CreateAggregateFunctionCall(function, methodArguments);
            if (applyProjection)
            {
                this.sourceQuery.ApplyProjection(functionCallExpression);
                result = this.SqlFactory.ConvertSelectQueryToDeriveTable(sourceQuery);
            }
            else
            {
                // probably a function on group by
                result = functionCallExpression;
            }
            return result;
        }
    }
}
