using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.ExpressionExtensions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    public class AggregateListExpressionConverterFactory : LinqToSqlExpressionConverterFactoryBase<AggregatedListExpression>
    {
        public AggregateListExpressionConverterFactory() : base()
        {
        }
        /// <inheritdoc />
        public override bool TryCreate(IConverterDependencies converterDependencies, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is AggregatedListExpression aggregatedListExpression)
            {
                var d = this.GetConverterDependencies(converterDependencies);
                converter = new AggregateListExpressionConverter(d, aggregatedListExpression, converterStack);
                return true;
            }
            converter = null;
            return false;
        }
    }

    public class AggregateListExpressionConverter : LinqToNonSqlQueryConverterBase<AggregatedListExpression>
    {
        public AggregateListExpressionConverter(LinqToSqlExpressionConverterDependencies context, AggregatedListExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack) : base(context, expression, converterStack)
        {
        }
        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            return new SqlCollectionExpression(convertedChildren);
        }
    }


    public class UpdateEntityExpressionConverterFactory : LinqToSqlExpressionConverterFactoryBase<MethodCallExpression>
    {
        public UpdateEntityExpressionConverterFactory() : base()
        {
        }

        /// <inheritdoc />
        public override bool TryCreate(IConverterDependencies converterDependencies, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is UpdateEntityExpression updateEntityExpression)
            {
                var d = this.GetConverterDependencies(converterDependencies);
                converter = new UpdateEntityExpressionConverter(d, updateEntityExpression, converterStack);
                return true;
            }
            converter = null;
            return false;
        }
    }
    public class UpdateEntityExpressionConverter : LinqToSqlQueryConverterBase<UpdateEntityExpression>
    {
        private SqlCollectionExpression convertedSetters;
        private SqlCollectionExpression convertedKeys;
        private SqlCollectionExpression outputFields;

        public UpdateEntityExpressionConverter(LinqToSqlExpressionConverterDependencies context, UpdateEntityExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack) : base(context, expression, converterStack)
        {
        }

        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            if(this.convertedSetters is null)
                throw new InvalidOperationException($"{nameof(this.convertedSetters)} is null. The {nameof(this.Expression.Setters)} child expression must be converted before calling Convert.");
            if (this.convertedKeys is null)
                throw new InvalidOperationException($"{nameof(this.convertedKeys)} is null. The {nameof(this.Expression.Keys)} child expression must be converted before calling Convert.");

            var sourceQuery = convertedChildren[0].CastTo<SqlSelectExpression>();
            var keyExpressions = this.convertedKeys.SqlExpressions;
            foreach (var keyExpression in keyExpressions)
            {
                var equalExpression = keyExpression.CastTo<SqlBinaryExpression>();
                sourceQuery.ApplyWhere(equalExpression, useOrOperator: false);
            }

            var derivedTable = this.SqlFactory.ConvertSelectQueryToDataManipulationDerivedTable(sourceQuery);
            var dataSourceToUpdate = sourceQuery.DataSources.First().Alias;
            var equalExpressions = this.convertedSetters.SqlExpressions;
            var columnNames = new List<string>();
            var values = new List<SqlExpression>();
            foreach (var setter in equalExpressions)
            {
                var equalExpression = setter.CastTo<SqlBinaryExpression>();
                var columnName = equalExpression.Left.CastTo<SqlDataSourceColumnExpression>().ColumnName;
                var value = equalExpression.Right;
                columnNames.Add(columnName);
                values.Add(value);
            }
            List<SelectColumn> outputs = null;
            if (this.outputFields?.SqlExpressions?.Any() == true)
            {
                var convertedOutputDsColumns = this.outputFields.SqlExpressions.ToArray();
                if(convertedOutputDsColumns.Length != this.Expression.OutputFields.Expressions.Count)
                    throw new InvalidOperationException($"Total number of output fields ({this.Expression.OutputFields.Expressions.Count}) does not match the number of converted output fields ({convertedOutputDsColumns.Length}).");
                outputs = new List<SelectColumn>();
                for (var i = 0; i < this.Expression.OutputFields.Expressions.Count; i++) {
                    var columnSelection = convertedOutputDsColumns[i].CastTo<SqlDataSourceColumnExpression>();
                    var columnName = columnSelection.ColumnName;
                    var outputColumnSelectionLambda = this.Expression.OutputFields.Expressions[i] as LambdaExpression
                                                        ??
                                                        throw new InvalidOperationException($"Output field expression at index {i} is not a LambdaExpression.");
                    var alias = GetOutputColumnAlias(outputColumnSelectionLambda, i);
                    // The post-update image: an update's output reports the row as it now stands.
                    var outputColumn = new SqlOutputColumnExpression(SqlOutputSource.Inserted, columnName);
                    var selectColumn = new SelectColumn(outputColumn, alias, scalarColumn: false);
                    outputs.Add(selectColumn);
                }
            }
            var updateExpression = this.SqlFactory.CreateUpdate(derivedTable, dataSourceToUpdate, columnNames, values, outputs: outputs);
            return updateExpression;
        }

        /// <summary>
        ///     <para>
        ///         Gets the alias to emit for an output column, taken from the member the selector
        ///         names.
        ///     </para>
        ///     <para>
        ///         A selector whose column type does not match the lambda's declared return type
        ///         arrives wrapped in a <see cref="ExpressionType.Convert"/> — a value-type column
        ///         selected as <c>object</c>, for instance — so the conversion is unwrapped before
        ///         the member is read.
        ///     </para>
        /// </summary>
        private static string GetOutputColumnAlias(LambdaExpression outputSelector, int index)
        {
            var body = outputSelector.Body;
            while (body is UnaryExpression unary &&
                   (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
            {
                body = unary.Operand;
            }

            return (body as MemberExpression)?.Member.Name
                    ??
                    throw new InvalidOperationException($"Output field expression at index {index} must select a member, for example 'x => x.EmployeeId', but was '{outputSelector.Body}'.");
        }

        public override bool IsChainedQueryArgument(Expression childNode) => childNode == this.Expression.Query;

        public override void OnConversionCompletedByChild(ExpressionConverterBase<Expression, SqlExpression> childConverter, Expression childNode, SqlExpression convertedExpression)
        {
            if (childNode == this.Expression.Query)       // source query
            {
                var sourceQuery = convertedExpression.CastTo<SqlSelectExpression>();
                this.AddMapping(sourceQuery, this.Expression.Setters);
                this.AddMapping(sourceQuery, this.Expression.Keys);
                this.AddMapping(sourceQuery, this.Expression.OutputFields);
            }
            else if (childNode == this.Expression.Setters)
            {
                this.convertedSetters = convertedExpression.CastTo<SqlCollectionExpression>();
            }
            else if (childNode == this.Expression.Keys)
            {
                this.convertedKeys = convertedExpression.CastTo<SqlCollectionExpression>();
            }
            else if (childNode == this.Expression.OutputFields)
            {
                this.outputFields = convertedExpression.CastTo<SqlCollectionExpression>();
            }
            base.OnConversionCompletedByChild(childConverter, childNode, convertedExpression);
        }

        private void AddMapping(SqlSelectExpression sourceQuery, AggregatedListExpression fieldValuePairs)
        {
            for (var i = 0; i < fieldValuePairs.Expressions.Count; i++)
            {
                if (fieldValuePairs.Expressions[i] is LambdaExpression lambda)
                {
                    var fieldSelectorArg = lambda.Parameters[0];
                    this.MapParameter(fieldSelectorArg, () => sourceQuery.GetQueryShapeForFieldMapping());
                }
            }
        }
    }
}
