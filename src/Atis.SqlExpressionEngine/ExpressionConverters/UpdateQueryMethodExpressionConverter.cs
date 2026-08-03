using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    public class UpdateQueryMethodExpressionConverterFactory : LinqToSqlExpressionConverterFactoryBase<MethodCallExpression>
    {
        public UpdateQueryMethodExpressionConverterFactory() : base()
        {
        }

        /// <inheritdoc />
        public override bool TryCreate(IConverterDependencies converterDependencies, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is MethodCallExpression methodCallExpression && IsSupportedUpdateMethod(methodCallExpression.Method))
            {
                var d = this.GetConverterDependencies(converterDependencies);
                converter = new UpdateQueryMethodExpressionConverter(d, methodCallExpression, converterStack);
                return true;
            }
            converter = null;
            return false;
        }

        private static bool IsSupportedUpdateMethod(MethodInfo method)
        {
            if (method.DeclaringType != typeof(QueryExtensions) ||
                method.Name != nameof(QueryExtensions.Update) ||
                !method.IsGenericMethod)
            {
                return false;
            }

            var genericArgumentCount = method.GetGenericArguments().Length;
            var parameterCount = method.GetParameters().Length;
            if (method.ReturnType == typeof(int))
            {
                return (genericArgumentCount == 1 && parameterCount == 3) ||
                       (genericArgumentCount == 2 && parameterCount == 4);
            }

            var outputReturnType = typeof(IReadOnlyList<IReadOnlyDictionary<string, object>>);
            return method.ReturnType == outputReturnType &&
                   ((genericArgumentCount == 1 && parameterCount == 4) ||
                    (genericArgumentCount == 2 && parameterCount == 5));
        }
    }

    public class UpdateQueryMethodExpressionConverter : DataManipulationQueryMethodExpressionConverterBase
    {
        public UpdateQueryMethodExpressionConverter(LinqToSqlExpressionConverterDependencies context, MethodCallExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack) : base(context, expression, converterStack)
        {
        }

        private bool HasOutputs => this.Expression.Method.ReturnType != typeof(int);

        /// <inheritdoc />
        protected override bool HasTableSelection => this.Expression.Method.GetGenericArguments().Length == 2;

        private int SetClauseArgumentIndex => this.HasTableSelection ? 2 : 1;

        /// <inheritdoc />
        protected override int WherePredicateArgumentIndex => this.HasTableSelection ? 3 : 2;

        private int OutputFieldsArgumentIndex => this.HasTableSelection ? 4 : 3;

        /// <inheritdoc />
        protected override SqlExpression CreateDmSqlExpression(SqlDerivedTableExpression sqlQuery, Guid selectedDataSource, IReadOnlyList<SqlExpression> arguments)
        {
            // 'arguments' excludes the source query, while the indexes above address the original
            // MethodCallExpression.
            var setClause = arguments[this.SetClauseArgumentIndex - 1];
            if (!(setClause is SqlMemberInitExpression memberInit))
            {
                throw new InvalidOperationException(
                    $"Argument {this.SetClauseArgumentIndex} of {nameof(QueryExtensions.Update)} must be a member-init expression, for example 'x => new T {{ Property = value }}'.");
            }

            var dataSourceToUpdate = sqlQuery.AllDataSources.FirstOrDefault(x => x.Alias == selectedDataSource)
                                     ?? throw new InvalidOperationException("The data source to update was not found in the query.");
            var tableToUpdate = dataSourceToUpdate.QuerySource.CastTo<SqlTableExpression>();

            var columnNames = memberInit.Bindings.Select(x => tableToUpdate.GetByPropertyName(x.MemberName)).ToArray();
            var values = memberInit.Bindings.Select(x => x.SqlExpression).ToArray();
            var outputs = this.HasOutputs
                ? this.CreateOutputs(arguments[this.OutputFieldsArgumentIndex - 1], selectedDataSource)
                : null;

            return this.SqlFactory.CreateUpdate(sqlQuery, selectedDataSource, columnNames, values, outputs);
        }

        private IReadOnlyList<SelectColumn> CreateOutputs(SqlExpression convertedOutputFields, Guid selectedDataSource)
        {
            var outputCollection = convertedOutputFields.CastTo<SqlCollectionExpression>(
                $"Argument {this.OutputFieldsArgumentIndex} of {nameof(QueryExtensions.Update)} must select an object array of columns.");
            var outputLambda = this.Expression.GetArgLambdaRequired(this.OutputFieldsArgumentIndex);
            var outputArray = outputLambda.Body as NewArrayExpression
                              ?? throw new InvalidOperationException(
                                  $"Argument {this.OutputFieldsArgumentIndex} of {nameof(QueryExtensions.Update)} must have the form 'x => new object[] {{ x.Column1, x.Column2 }}'.");
            var convertedOutputs = outputCollection.SqlExpressions.ToArray();

            if (outputArray.Expressions.Count == 0)
                throw new InvalidOperationException("An update output selector must contain at least one column.");
            if (convertedOutputs.Length != outputArray.Expressions.Count)
            {
                throw new InvalidOperationException(
                    $"The number of converted output fields ({convertedOutputs.Length}) does not match the number selected ({outputArray.Expressions.Count}).");
            }

            var outputs = new List<SelectColumn>(outputArray.Expressions.Count);
            for (var i = 0; i < outputArray.Expressions.Count; i++)
            {
                var selectedColumn = convertedOutputs[i].CastTo<SqlDataSourceColumnExpression>(
                    $"Update output field {i} must select a stored column.");
                if (selectedColumn.DataSourceAlias != selectedDataSource)
                {
                    throw new InvalidOperationException(
                        $"Update output field {i} belongs to a different data source than the table being updated.");
                }

                outputs.Add(new SelectColumn(
                    new SqlOutputColumnExpression(SqlOutputSource.Inserted, selectedColumn.ColumnName),
                    GetOutputColumnAlias(outputArray.Expressions[i], i),
                    scalarColumn: false));
            }
            return outputs;
        }

        private static string GetOutputColumnAlias(Expression outputExpression, int index)
        {
            while (outputExpression is UnaryExpression unary &&
                   (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
            {
                outputExpression = unary.Operand;
            }

            return (outputExpression as MemberExpression)?.Member.Name
                   ?? throw new InvalidOperationException(
                       $"Update output field {index} must select a member, for example 'x => new object[] {{ x.EmployeeId }}', but was '{outputExpression}'.");
        }
    }
}
