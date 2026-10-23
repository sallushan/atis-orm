using System;
using System.Collections.Generic;
using System.Linq;

using Atis.Orm.Functions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.Internal;
using Atis.SqlExpressionEngine.Services;
using Atis.SqlExpressionEngine.SqlExpressions;

namespace Atis.Orm.Translation
{
    /// <summary>
    ///     <para>
    ///         Rewrites <c>OUTER APPLY</c> / <c>CROSS APPLY</c> of a correlated subquery into a
    ///         <c>LEFT JOIN</c> / <c>INNER JOIN</c> of a derived table.
    ///     </para>
    ///     <para>
    ///         This changes the shape of the SQL the LINQ asked for, so a translator only uses it when the
    ///         provider switched <c>RewriteApplyAsJoin</c> on. A subquery with <c>TOP n</c> becomes
    ///         <c>ROW_NUMBER() OVER (PARTITION BY correlation keys ORDER BY ...) &lt;= n</c> inside the
    ///         joined table. The rewrite is skipped, and the APPLY kept, whenever it cannot be done
    ///         without changing the result.
    ///     </para>
    /// </summary>
    public class ApplyJoinRewriter
    {
        private const string RowNumberAlias = "auto_row_num";
        private readonly ISqlExpressionFactory sqlFactory;

        /// <summary>Creates a rewriter that builds new nodes through <paramref name="sqlFactory"/>.</summary>
        public ApplyJoinRewriter(ISqlExpressionFactory sqlFactory)
        {
            this.sqlFactory = sqlFactory ?? throw new ArgumentNullException(nameof(sqlFactory));
        }

        /// <summary>Creates a rewriter that uses the engine's default node factory.</summary>
        public ApplyJoinRewriter()
            : this(new SqlExpressionFactory())
        {
        }

        /// <summary>
        ///     Returns <paramref name="joins"/> with every APPLY that can be rewritten replaced by its join.
        ///     The same list is returned when nothing changes.
        /// </summary>
        public IReadOnlyList<SqlAliasedJoinSourceExpression> Rewrite(SqlDerivedTableExpression parentTable, IReadOnlyList<SqlAliasedJoinSourceExpression> joins)
        {
            if (parentTable is null)
                throw new ArgumentNullException(nameof(parentTable));
            if (joins is null || joins.Count == 0)
                return joins;

            List<SqlAliasedJoinSourceExpression> result = null;
            for (var i = 0; i < joins.Count; i++)
            {
                var rewritten = this.Rewrite(parentTable, joins[i]);
                if (!ReferenceEquals(rewritten, joins[i]) && result is null)
                    result = joins.Take(i).ToList();
                result?.Add(rewritten);
            }
            return (IReadOnlyList<SqlAliasedJoinSourceExpression>)result ?? joins;
        }

        /// <summary>Rewrites one join, or returns it as it is when it cannot be rewritten.</summary>
        public SqlAliasedJoinSourceExpression Rewrite(SqlDerivedTableExpression parentTable, SqlAliasedJoinSourceExpression node)
        {
            if (node.JoinType != SqlJoinType.OuterApply && node.JoinType != SqlJoinType.CrossApply)
                return node;
            if (!(node.QuerySource is SqlDerivedTableExpression derivedTable))
                return node;

            // Only a plain "select ... from one thing where correlation and filters" can be joined.
            if (!(derivedTable.WhereClause?.FilterConditions.Count > 0)
                || derivedTable.GroupByClause?.Count > 0
                || derivedTable.HavingClause?.FilterConditions.Count > 0
                || !(derivedTable.QueryShape is SqlQueryShapeExpression shape)
                || shape.IsScalar)
                return node;

            // ROW_NUMBER needs an ORDER BY to be valid, and "TOP n" without one picks arbitrary rows anyway,
            // so a TOP without ordering is left as an APPLY.
            if (derivedTable.Top != null && !(derivedTable.OrderByClause?.OrderByColumns.Count > 0))
                return node;

            var parentAliases = new HashSet<Guid>(parentTable.AllDataSources.Select(x => x.Alias));
            var builder = new JoinableQueryBuilder(derivedTable, parentAliases, this.sqlFactory, node.Alias);
            if (!builder.TryBuildUnsafe(out var built) || built.JoinCondition is null)
                return node;

            var joinType = node.JoinType == SqlJoinType.OuterApply ? SqlJoinType.Left : SqlJoinType.Inner;
            var table = built.NormalizedDerivedTable;
            if (table.Top != null)
                table = this.LimitPerKeyWithRowNumber(table, built.OtherTablePredicateSides);

            return new SqlAliasedJoinSourceExpression(joinType, table.ConvertToTableIfPossible(), node.Alias, built.JoinCondition, node.JoinName, node.IsNavigationJoin);
        }

        /// <summary>
        ///     <c>select cols from t where .. order by o top n</c> becomes
        ///     <c>select cols from (select cols, row_number() over (partition by keys order by o) as auto_row_num from t where ..) where auto_row_num &lt;= n</c>.
        /// </summary>
        private SqlDerivedTableExpression LimitPerKeyWithRowNumber(SqlDerivedTableExpression table, IReadOnlyCollection<SqlExpression> partitionKeys)
        {
            var top = table.Top;
            var over = new SqlOverClauseExpression(partitionKeys.ToList(), table.OrderByClause.OrderByColumns);
            var rowNumber = new SqlWindowFunctionExpression(SqlWindowFunction.RowNumber, Array.Empty<SqlExpression>(), over);

            var columns = table.SelectColumnCollection.SelectColumns.ToList();
            // A scalar select list has one column; with the row number added it is no longer scalar.
            if (columns[0].ScalarColumn)
                columns[0] = new SelectColumn(columns[0].ColumnExpression, columns[0].Alias, scalarColumn: false);
            var rowNumberColumn = new SelectColumn(rowNumber, RowNumberAlias, scalarColumn: false, helperColumn: true);
            columns.Add(rowNumberColumn);

            // The ordering moved into the OVER clause and TOP moved into the filter below.
            var inner = new SqlDerivedTableExpression(table.CteDataSources, table.FromSource, table.Joins, table.WhereClause, table.GroupByClause, table.HavingClause,
                null, new SqlSelectListExpression(columns.ToArray()), false, null, null, null, false, table.Tag, table.QueryShape, SqlExpressionType.DerivedTable);

            var source = new SqlAliasedFromSourceExpression(inner, Guid.NewGuid());
            var rowNumberRef = new SqlDataSourceColumnExpression(source.Alias, RowNumberAlias);
            var filter = new SqlFilterClauseExpression(
                new[] { new FilterCondition(new SqlBinaryExpression(rowNumberRef, new SqlLiteralExpression(top), SqlExpressionType.LessThanOrEqual), false) },
                SqlExpressionType.WhereClause);
            var projection = columns.Where(x => x != rowNumberColumn)
                                    .Select(x => new SelectColumn(new SqlDataSourceColumnExpression(source.Alias, x.Alias), x.Alias, x.ScalarColumn, x.HelperColumn))
                                    .ToArray();

            return new SqlDerivedTableExpression(null, source, null, filter, null, null, null, new SqlSelectListExpression(projection),
                false, null, null, null, false, null, table.QueryShape, SqlExpressionType.DerivedTable);
        }
    }
}
