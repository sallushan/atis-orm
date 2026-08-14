using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.Internal;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    public class InsertQueryMethodExpressionConverterFactory : LinqToSqlExpressionConverterFactoryBase<MethodCallExpression>
    {
        public override bool TryCreate(
            IConverterDependencies converterDependencies,
            Expression expression,
            ExpressionConverterBase<Expression, SqlExpression>[] converterStack,
            out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is MethodCallExpression methodCall && IsSupportedInsertMethod(methodCall.Method))
            {
                converter = new InsertQueryMethodExpressionConverter(
                    this.GetConverterDependencies(converterDependencies), methodCall, converterStack);
                return true;
            }

            converter = null;
            return false;
        }

        private static bool IsSupportedInsertMethod(MethodInfo method)
        {
            if (method.DeclaringType != typeof(QueryExtensions)
                || method.Name != nameof(QueryExtensions.Insert)
                || !method.IsGenericMethod
                || method.GetGenericArguments().Length != 1)
            {
                return false;
            }

            if (method.ReturnType == typeof(int))
                return method.GetParameters().Length == 2;

            return method.ReturnType == typeof(IReadOnlyList<IReadOnlyDictionary<string, object>>)
                && method.GetParameters().Length == 3;
        }
    }

    public class InsertQueryMethodExpressionConverter : QueryMethodExpressionConverterBase
    {
        public InsertQueryMethodExpressionConverter(
            LinqToSqlExpressionConverterDependencies dependencies,
            MethodCallExpression expression,
            ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(dependencies, expression, converterStack)
        {
        }

        private bool HasOutputs => this.Expression.Method.ReturnType != typeof(int);

        protected override SqlExpression Convert(SqlSelectExpression sqlQuery, SqlExpression[] arguments)
        {
            ValidateDestination(sqlQuery);

            var dataSource = sqlQuery.DataSources.Single();
            var table = dataSource.QuerySource.CastTo<SqlTableExpression>(
                "The insert destination must resolve to a mapped table.");
            var insertFields = arguments[0] as SqlMemberInitExpression
                ?? throw new InvalidOperationException(
                    $"Argument 1 of {nameof(QueryExtensions.Insert)} must be a member-init expression, for example '() => new T {{ Property = value }}'.");

            var columns = insertFields.Bindings
                .Select(x => table.GetByPropertyName(x.MemberName))
                .ToArray();
            var values = insertFields.Bindings.Select(x => x.SqlExpression).ToArray();
            var outputs = this.HasOutputs
                ? this.CreateOutputs(arguments[1], dataSource.Alias)
                : null;

            return this.SqlFactory.CreateInsert(table.SqlTable, columns, values, outputs);
        }

        private static void ValidateDestination(SqlSelectExpression sqlQuery)
        {
            var isPlainTable = sqlQuery.DataSources.Count == 1
                && sqlQuery.DataSources.Single().QuerySource is SqlTableExpression
                && sqlQuery.CteDataSources.Count == 0
                && sqlQuery.SelectList.Count == 0
                && sqlQuery.WhereClause.Count == 0
                && sqlQuery.GroupByClause is null
                && sqlQuery.HavingClause.Count == 0
                && sqlQuery.OrderByClause.Count == 0
                && sqlQuery.Top is null
                && !sqlQuery.IsDistinct
                && sqlQuery.RowOffset is null
                && sqlQuery.RowsPerPage is null;

            if (!isPlainTable)
            {
                throw new InvalidOperationException(
                    $"{nameof(QueryExtensions.Insert)} requires an uncomposed query for exactly one mapped table. Use {nameof(QueryExtensions.BulkInsert)} for insert-select operations.");
            }
        }

        private IReadOnlyList<SelectColumn> CreateOutputs(SqlExpression convertedOutputFields, Guid destinationAlias)
        {
            var outputCollection = convertedOutputFields.CastTo<SqlCollectionExpression>(
                "Argument 2 of Insert must select an object array of columns.");
            var outputLambda = this.Expression.GetArgLambdaRequired(2);
            var outputArray = outputLambda.Body as NewArrayExpression
                ?? throw new InvalidOperationException(
                    "Argument 2 of Insert must have the form 'x => new object[] { x.Column1, x.Column2 }'.");
            var convertedOutputs = outputCollection.SqlExpressions.ToArray();

            if (outputArray.Expressions.Count == 0)
                throw new InvalidOperationException("An insert output selector must contain at least one column.");
            if (convertedOutputs.Length != outputArray.Expressions.Count)
                throw new InvalidOperationException("The number of converted output fields does not match the number selected.");

            var outputs = new List<SelectColumn>(outputArray.Expressions.Count);
            for (var i = 0; i < outputArray.Expressions.Count; i++)
            {
                var selectedColumn = convertedOutputs[i].CastTo<SqlDataSourceColumnExpression>(
                    $"Insert output field {i} must select a stored column.");
                if (selectedColumn.DataSourceAlias != destinationAlias)
                    throw new InvalidOperationException($"Insert output field {i} belongs to a different data source than the destination table.");

                outputs.Add(new SelectColumn(
                    new SqlOutputColumnExpression(SqlOutputSource.Inserted, selectedColumn.ColumnName),
                    SelectedMemberAlias.Resolve(outputArray.Expressions[i], i, "Insert output"),
                    scalarColumn: false));
            }

            return outputs;
        }
    }
}
