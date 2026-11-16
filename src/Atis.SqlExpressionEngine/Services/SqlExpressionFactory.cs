using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.Internal;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Atis.SqlExpressionEngine.Services
{
    public class SqlExpressionFactory : ISqlExpressionFactory
    {
        // The conversion itself lives in SqlSelectExpression (the query builds its derived table). These methods
        // stay because ISqlExpressionFactory is a public interface.
        public SqlDerivedTableExpression ConvertSelectQueryToDeriveTable(SqlSelectExpression selectQuery)
            => (selectQuery ?? throw new ArgumentNullException(nameof(selectQuery))).ToDerivedTable();

        public SqlDerivedTableExpression ConvertSelectQueryToUnwrappableDeriveTable(SqlSelectExpression selectQuery)
            => (selectQuery ?? throw new ArgumentNullException(nameof(selectQuery))).ToUnwrappableDerivedTable();

        public SqlAliasExpression CreateAlias(string alias)
        {
            return new SqlAliasExpression(alias);
        }

        public SqlBinaryExpression CreateBinary(SqlExpression left, SqlExpression right, SqlExpressionType sqlExpressionType)
        {
            return new SqlBinaryExpression(left, right, sqlExpressionType);
        }

        public SqlLiteralExpression CreateLiteral(object value)
        {
            return new SqlLiteralExpression(value);
        }

        public SqlStringFunctionExpression CreateStringFunction(SqlStringFunction stringFunction, SqlExpression stringExpression, IReadOnlyList<SqlExpression> arguments)
        {
            return new SqlStringFunctionExpression(stringFunction, stringExpression, arguments);
        }

        public SqlTableExpression CreateTable(SqlTable sqlTable, IReadOnlyList<TableColumn> tableColumns)
        {
            return new SqlTableExpression(sqlTable, tableColumns);
        }

        public virtual SqlFunctionExpression CreateFunctionCall(string functionName, SqlExpression[] arguments)
        {
            return new SqlFunctionExpression(functionName, arguments);
        }

        /// <inheritdoc />
        public virtual SqlMathFunctionExpression CreateMathFunction(
            SqlMathFunction function,
            IReadOnlyList<Type> argumentTypes,
            SqlExpression[] arguments,
            MidpointRounding? roundingMode = null)
        {
            return new SqlMathFunctionExpression(function, argumentTypes, arguments, roundingMode);
        }

        /// <inheritdoc />
        public virtual SqlNewDateTimeExpression CreateNewDateTime(SqlExpression year, SqlExpression month, SqlExpression day, SqlExpression hour = null, SqlExpression minute = null, SqlExpression second = null, SqlExpression millisecond = null)
        {
            return new SqlNewDateTimeExpression(year, month, day, hour, minute, second, millisecond);
        }

        public virtual SqlAggregateFunctionExpression CreateAggregateFunctionCall(SqlAggregateFunction function, SqlExpression[] arguments)
        {
            return new SqlAggregateFunctionExpression(function, arguments);
        }

        public SqlSelectExpression CreateSelectQueryFromStandaloneSelect(SqlStandaloneSelectExpression standaloneSelect)
        {
            if (standaloneSelect is null)
                throw new ArgumentNullException(nameof(standaloneSelect));

            var selectSqlQuery = new SqlSelectExpression(cteDataSources: null, standaloneSelect, sqlFactory: this);
            return selectSqlQuery;
        }

        public SqlConditionalExpression CreateCondition(SqlExpression test, SqlExpression ifTrue, SqlExpression ifFalse)
        {
            return new SqlConditionalExpression(test, ifTrue, ifFalse);
        }

        protected virtual SqlSelectExpression CreateUnwrappedSelectQueryFromDerivedTable(SqlDerivedTableExpression derivedTable)
            => SqlSelectExpression.FromDerivedTable(derivedTable, this);

        public SqlSelectExpression CreateSelectQuery(SqlExpression queryShape)
        {
            if (queryShape is null)
                throw new ArgumentNullException(nameof(queryShape));

            if (queryShape.NodeType == SqlExpressionType.DerivedTable && queryShape is SqlDerivedTableExpression derivedTable)
            {
                return this.CreateUnwrappedSelectQueryFromDerivedTable(derivedTable);
            }
            else
            {
                return new SqlSelectExpression(cteDataSources: null, queryShape, sqlFactory: this);
            }
        }

        public SqlDefaultIfEmptyExpression CreateDefaultIfEmpty(SqlDerivedTableExpression derivedTable)
        {
            return new SqlDefaultIfEmptyExpression(derivedTable);
        }

        public SqlUnionQueryExpression CreateUnionQuery(UnionItem[] unionItems)
        {
            return new SqlUnionQueryExpression(unionItems);
        }

        public SqlExistsExpression CreateExists(SqlDerivedTableExpression subQuery)
        {
            return new SqlExistsExpression(subQuery);
        }

        public SqlLikeExpression CreateLike(SqlExpression stringExpression, SqlExpression pattern)
        {
            return new SqlLikeExpression(stringExpression, pattern, SqlExpressionType.Like);
        }

        public SqlLikeExpression CreateLikeStartsWith(SqlExpression stringExpression, SqlExpression pattern)
        {
            return new SqlLikeExpression(stringExpression, pattern, SqlExpressionType.LikeStartsWith);
        }

        public SqlLikeExpression CreateLikePattern(SqlExpression stringExpression, SqlExpression pattern)
        {
            return new SqlLikeExpression(stringExpression, pattern, SqlExpressionType.LikePattern);
        }

        public SqlLikeExpression CreateLikeEndsWith(SqlExpression stringExpression, SqlExpression pattern)
        {
            return new SqlLikeExpression(stringExpression, pattern, SqlExpressionType.LikeEndsWith);
        }

        /// <inheritdoc />
        public SqlLikeAnyExpression CreateLikeAny(SqlExpression stringExpression, SqlExpression values, LikeMatchMode matchMode)
        {
            return new SqlLikeAnyExpression(stringExpression, values, matchMode);
        }

        public SqlDelimitedValuesExpression CreateDelimitedValues(SqlExpression values, string delimiter)
        {
            return new SqlDelimitedValuesExpression(values, delimiter);
        }

        public SqlDateAddExpression CreateDateAdd(SqlDatePart datePart, SqlExpression interval, SqlExpression dateExpression)
        {
            return new SqlDateAddExpression(datePart, interval, dateExpression);
        }

        public SqlDateSubtractExpression CreateDateSubtract(SqlDatePart datePart, SqlExpression startDate, SqlExpression endDate)
        {
            return new SqlDateSubtractExpression(datePart, startDate, endDate);
        }

        public SqlCollectionExpression CreateCollection(IEnumerable<SqlExpression> sqlExpressions)
        {
            return new SqlCollectionExpression(sqlExpressions);
        }

        public SqlCastExpression CreateCast(SqlExpression expression, ISqlDataType sqlDataType)
        {
            return new SqlCastExpression(expression, sqlDataType);
        }

        public SqlDatePartExpression CreateDatePart(SqlDatePart datePart, SqlExpression dateExpr)
        {
            return new SqlDatePartExpression(datePart, dateExpr);
        }

        public SqlParameterExpression CreateParameter(object value, string identity = null, Type valueType = null, string contextKey = null)
        {
            return new SqlParameterExpression(value, identity, valueType, contextKey);
        }

        public SqlInValuesExpression CreateInValuesExpression(SqlExpression expression, SqlExpression values)
        {
            return new SqlInValuesExpression(expression, values);
        }

        public SqlOptionalPredicateExpression CreateOptionalPredicateExpression(SqlExpression guard, SqlExpression predicate, OptionalGuardKind guardKind = OptionalGuardKind.NullOnly)
        {
            return new SqlOptionalPredicateExpression(guard, predicate, guardKind);
        }

        public SqlNegateExpression CreateNegate(SqlExpression operand)
        {
            return new SqlNegateExpression(operand);
        }

        public SqlNotExpression CreateNot(SqlExpression sqlExpression)
        {
            return new SqlNotExpression(sqlExpression);
        }

        public SqlUpdateExpression CreateUpdate(SqlDerivedTableExpression source, Guid dataSourceToUpdate, IReadOnlyList<string> columns, IReadOnlyList<SqlExpression> values, IReadOnlyList<SelectColumn> outputs = null)
        {
            return new SqlUpdateExpression(source, dataSourceToUpdate, columns, values, outputs);
        }

        public SqlInsertExpression CreateInsert(SqlTable table, IReadOnlyList<string> columns, IReadOnlyList<SqlExpression> values, IReadOnlyList<SelectColumn> outputs = null)
        {
            return new SqlInsertExpression(table, columns, values, outputs);
        }

        public SqlDerivedTableExpression ConvertSelectQueryToDataManipulationDerivedTable(SqlSelectExpression selectQuery)
            => (selectQuery ?? throw new ArgumentNullException(nameof(selectQuery))).ToDataManipulationDerivedTable();

        public SqlDeleteExpression CreateDelete(SqlDerivedTableExpression source, Guid dataSourceAlias)
        {
            return new SqlDeleteExpression(source, dataSourceAlias);
        }

        public SqlExpression CreateJoinCondition(SqlExpression leftSide, SqlExpression rightSide)
        {
            SqlBinaryExpression joinPredicate = null;

            var leftExpressions = (leftSide as SqlQueryShapeExpression)?.FlattenQueryShape()
                                    ??
                                    new[] { leftSide };
            var rightExpressions = (rightSide as SqlQueryShapeExpression)?.FlattenQueryShape()
                                    ??
                                    new[] { rightSide };


            if (leftExpressions.Count != rightExpressions.Count)
                throw new InvalidOperationException($"Source columns count {leftExpressions.Count} does not match other columns count {rightExpressions.Count}.");

            for (var i = 0; i < leftExpressions.Count; i++)
            {
                var leftSideExpression = leftExpressions[i];
                var rightSideExpression = rightExpressions[i];
                var condition = this.CreateBinary(leftSideExpression, rightSideExpression, SqlExpressionType.Equal);
                joinPredicate = joinPredicate == null ? condition : this.CreateBinary(joinPredicate, condition, SqlExpressionType.AndAlso);
            }

            return joinPredicate;
        }

        public SqlInsertIntoExpression CreateInsertInto(SqlTable sqlTable, IReadOnlyList<TableColumn> tableColumns, SqlDerivedTableExpression derivedTable)
        {
            return new SqlInsertIntoExpression(sqlTable, tableColumns, derivedTable);
        }

        public SqlNewGuidExpression CreateNewGuid()
        {
            return new SqlNewGuidExpression();
        }
    }
}
