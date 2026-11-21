using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.Internal;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Atis.SqlExpressionEngine.SqlExpressions
{
    // Conversion between a SqlSelectExpression (the query being built) and a SqlDerivedTableExpression
    // (the finished, immutable node). The query builds the derived table; the derived table knows nothing
    // about the query. See docs/SqlSelectExpressionRedesign.md, section 8.
    public partial class SqlSelectExpression
    {
        /// <summary>
        ///     Creates a derived table from the current state of this query. This query is not changed.
        /// </summary>
        public SqlDerivedTableExpression ToDerivedTable()
            => this.ToDerivedTable(SqlExpressionType.DerivedTable);

        /// <summary>
        ///     Same as <see cref="ToDerivedTable()"/>, but the derived table is marked so that it can be turned back
        ///     into a query without changing it. The query must not have CTE data sources.
        /// </summary>
        public SqlDerivedTableExpression ToUnwrappableDerivedTable()
            // DO NOT REMOVE UnwrappableDerivedTable enum type, you may NOT find the references used but it's important
            // because we are testing the NodeType == DerivedTable so it's being used in the reverse order
            => this.ToDerivedTable(SqlExpressionType.UnwrappableDerivedTable);

        /// <summary>
        ///     Creates the derived table that a data manipulation (update, delete) is built on. It has no select list.
        /// </summary>
        public SqlDerivedTableExpression ToDataManipulationDerivedTable()
        {
            var tempDerivedTable = this.ToDerivedTable();
            return new SqlDerivedTableExpression(tempDerivedTable.CteDataSources, tempDerivedTable.FromSource, tempDerivedTable.Joins, tempDerivedTable.WhereClause, tempDerivedTable.GroupByClause, tempDerivedTable.HavingClause, tempDerivedTable.OrderByClause, selectColumnCollection: null, tempDerivedTable.IsDistinct, tempDerivedTable.Top, tempDerivedTable.RowOffset, tempDerivedTable.RowsPerPage, tempDerivedTable.AutoProjection, this.Tag, this.GetQueryShapeForFieldMapping(), SqlExpressionType.DataManipulationDerivedTable);
        }

        private SqlDerivedTableExpression ToDerivedTable(SqlExpressionType nodeType)
        {
            // the last operation applied to this query is checked here, because this is where the query is read
            this.AssertValidIfOutermost(nameof(ToDerivedTable));

            var autoProjection = this.selectList.Count == 0;
            // we should NOT be modifying this query
            // previously we were doing sqlQuery.ApplyAutoProjection()
            var cteDataSources = this.cteDataSources
                                        .Select(x => new SqlAliasedCteSourceExpression(x.CteBody, x.CteAlias))
                                        .ToArray();
            if (nodeType == SqlExpressionType.UnwrappableDerivedTable &&
                cteDataSources.Length > 0)
                throw new InvalidOperationException($"nodeType is '{nodeType}' while there are CTE Data Sources being extracted from selectQuery, CTE Data Sources are not allowed in this case. This is because for the UnwrappableDerivedTable, the SqlDerivedTableExpression must not be changed when creating a SqlSelectExpression from it, however, it will change the expression if there are CTE Data Sources present, this is a safety measure to prevent more complex errors.");
            var firstDataSource = this.dataSources.Where(x => !(x is JoinDataSource)).First();
            var fromDataSource = new SqlAliasedFromSourceExpression(firstDataSource.QuerySource, firstDataSource.Alias);
            var joinedDataSources = this.dataSources
                                        .Where(x => x is JoinDataSource)
                                        .Cast<JoinDataSource>()
                                        .Select(x => new SqlAliasedJoinSourceExpression(x.JoinType, x.QuerySource, x.Alias, x.JoinCondition, x.JoinName, x.IsNavigationJoin))
                                        .ToArray();
            SqlFilterClauseExpression whereClause = null;
            if (this.whereClause.Count > 0)
                whereClause = new SqlFilterClauseExpression(this.whereClause.ToArray(), SqlExpressionType.WhereClause);
            SqlExpression[] groupByClause = null;
            SqlExpression groupClause = this.GroupByClause != null ? CreateCopyOfQueryShape(this.GroupByClause, autoProjection) : null;
            if (groupClause != null)
            {
                var groupExpressions = new List<SqlExpression>();
                FlattenSqlExpressions(groupClause, groupExpressions);
                groupByClause = groupExpressions.ToArray();
            }
            SqlFilterClauseExpression havingClause = null;
            if (this.havingClause.Count > 0)
                havingClause = new SqlFilterClauseExpression(this.havingClause.ToArray(), SqlExpressionType.HavingClause);
            SqlOrderByClauseExpression orderByClause = null;
            if (this.orderByClause.Count > 0)
                orderByClause = new SqlOrderByClauseExpression(this.orderByClause.ToArray());
            SqlExpression queryShape;
            var selectQueryShape = this.GetQueryShapeForFieldMapping();
            selectQueryShape = (selectQueryShape as SqlQueryShapeFieldResolverExpression)?.ShapeExpression
                                ??
                                selectQueryShape;
            selectQueryShape = CreateCopyOfQueryShape(selectQueryShape, autoProjection);
            queryShape = selectQueryShape;
            IReadOnlyList<SelectColumn> columns;
            if (this.selectList.Count > 0)
                columns = this.selectList.Select(x => new SelectColumn(x.ColumnExpression, x.Alias, x.ScalarColumn, x.HelperColumn)).ToArray();
            else
            {
                if (groupClause == null)
                    columns = ExtensionMethods.ConvertQueryShapeToSelectList(queryShape, applyAll: false);
                else
                    columns = ExtensionMethods.ConvertQueryShapeToSelectList(groupClause, applyAll: false);
            }
            var selectListExpression = new SqlSelectListExpression(columns);

            return new SqlDerivedTableExpression(
                    cteDataSources,
                    fromDataSource,
                    joinedDataSources,
                    whereClause,
                    groupByClause,
                    havingClause,
                    orderByClause,
                    selectListExpression,
                    this.IsDistinct,
                    this.Top,
                    this.RowOffset,
                    this.RowsPerPage,
                    autoProjection,
                    this.Tag,
                    queryShape,
                    nodeType);
        }

        private static void FlattenSqlExpressions(SqlExpression sqlExpression, List<SqlExpression> sqlExpressions)
        {
            if (sqlExpression is SqlMemberInitExpression queryShape)
            {
                foreach (var binding in queryShape.Bindings)
                {
                    FlattenSqlExpressions(binding.SqlExpression, sqlExpressions);
                }
            }
            else
                sqlExpressions.Add(sqlExpression);
        }

        private static SqlExpression CreateCopyOfQueryShape(SqlExpression queryShape, bool autoProjection)
        {
            if (queryShape is null)
                throw new ArgumentNullException(nameof(queryShape));

            return convert(queryShape);

            SqlExpression convert(SqlExpression sqlExpression)
            {
                if (sqlExpression is SqlMemberInitExpression qs)
                {
                    IEnumerable<SqlMemberAssignment> bindings = qs.Bindings;
                    if (!autoProjection)
                        bindings = bindings.Where(x => x.Projectable);
                    var newList = bindings.Select(x => new SqlMemberAssignment(x.MemberName, convert(x.SqlExpression), x.Projectable)).ToArray();
                    return new SqlMemberInitExpression(newList);
                }
                else if (sqlExpression is SqlDataSourceQueryShapeExpression dsQueryShape)
                {
                    var newShape = convert(dsQueryShape.ShapeExpression);
                    return new SqlDataSourceQueryShapeExpression(newShape, dsQueryShape.DataSourceAlias);
                }
                return sqlExpression;
            }
        }

        /// <summary>
        ///     Creates a query from a derived table. A plain derived table (it only adds a filter and, possibly,
        ///     navigation joins on top of its source) is opened up, so the query continues where the derived table
        ///     stopped and does not wrap it in one more level. Any other derived table becomes the data source of
        ///     the new query.
        /// </summary>
        /// <remarks>
        ///     The state of the new query is set directly. The previous approach replayed the derived table through
        ///     the public methods (<c>AddJoin</c>, <c>ApplyWhere</c>, ...), and each of those could wrap.
        /// </remarks>
        public static SqlSelectExpression FromDerivedTable(SqlDerivedTableExpression derivedTable, ISqlExpressionFactory sqlFactory)
        {
            if (derivedTable is null)
                throw new ArgumentNullException(nameof(derivedTable));
            if (sqlFactory is null)
                throw new ArgumentNullException(nameof(sqlFactory));

            if (derivedTable.NodeType == SqlExpressionType.DerivedTable && IsDerivedTableUnwrappable(derivedTable))
            {
                // IsDerivedTableUnwrappable guarantees: it was not given a projection of its own (auto projection),
                // and has no grouping, having, order by, distinct, top, row offset or rows per page. So only
                // the source, the joins, the filter and the query shape have to be carried over.
                var cteDataSources = derivedTable.CteDataSources
                                                    .Select(x => new CteDataSource(x.CteBody, x.CteAlias))
                                                    .ToArray();
                var fromSource = derivedTable.FromSource;
                var selectQuery = new SqlSelectExpression(cteDataSources, fromSource.QuerySource, sqlFactory);
                // the data sources get new aliases, so the query does not share aliases with the derived table
                var aliasMap = new Dictionary<Guid, Guid>
                {
                    { derivedTable.FromSource.Alias, selectQuery.dataSources.First().Alias }
                };
                foreach (var join in derivedTable.Joins)
                {
                    var updatedQuerySource = ReplaceDataSourceAliasVisitor.FindAndReplace(aliasMap, join.QuerySource) as SqlQuerySourceExpression
                                            ??
                                            throw new InvalidOperationException($"Failed to convert {join.QuerySource} to SqlQuerySourceExpression");
                    var newJoinAlias = Guid.NewGuid();
                    aliasMap.Add(join.Alias, newJoinAlias);
                    SqlExpression joinCondition = null;
                    if (join.JoinCondition != null)
                        joinCondition = ReplaceDataSourceAliasVisitor.FindAndReplace(aliasMap, join.JoinCondition);
                    selectQuery.AddAliasedDataSource(new JoinDataSource(join.JoinType, updatedQuerySource, newJoinAlias, joinCondition, join.JoinName, join.IsNavigationJoin));
                }
                if (!(derivedTable.WhereClause?.FilterConditions.IsNullOrEmpty() ?? true))
                {
                    foreach (var filterCondition in derivedTable.WhereClause.FilterConditions)
                    {
                        var updatedPredicate = ReplaceDataSourceAliasVisitor.FindAndReplace(aliasMap, filterCondition.Predicate);
                        selectQuery.whereClause.Add(new FilterCondition(updatedPredicate, filterCondition.UseOrOperator));
                    }
                }
                selectQuery.QueryShape = ReplaceDataSourceAliasVisitor.FindAndReplace(aliasMap, derivedTable.QueryShape);
                selectQuery.AssertValidIfOutermost(nameof(FromDerivedTable));
                return selectQuery;
            }
            return new SqlSelectExpression(cteDataSources: null, derivedTable, sqlFactory);
        }

        private static bool IsDerivedTableUnwrappable(SqlDerivedTableExpression derivedTable)
        {
            if (derivedTable is null)
                throw new ArgumentNullException(nameof(derivedTable));
            return derivedTable.AutoProjection &&
                    //derivedTable.CteDataSources.Length == 0 &&
                    (derivedTable.Joins.Count == 0 ||     // either there are no joins
                                                          // or all the joins are navigation joins
                    derivedTable.Joins.All(x => x.IsNavigationJoin)) &&
                    !(derivedTable.HavingClause?.FilterConditions.Count > 0) &&
                    derivedTable.GroupByClause.Count == 0 &&
                    !(derivedTable.OrderByClause?.OrderByColumns.Count > 0) &&
                    derivedTable.Top == null &&
                    derivedTable.IsDistinct == false &&
                    derivedTable.RowOffset == null &&
                    derivedTable.RowsPerPage == null;
        }
    }
}
