using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using Atis.SqlExpressionEngine.Visitors;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Atis.SqlExpressionEngine.Internal
{
    /// <summary>
    ///     <para>
    ///         A sub-query that was selected in the projection of the final query and has to be loaded as a
    ///         separate query, e.g. <c>Degrees = x.NavDegrees.ToList()</c>.
    ///     </para>
    /// </summary>
    public class ChildQuery
    {
        /// <summary>
        ///     Name of the member of the child query's <see cref="SqlDerivedTableExpression.QueryShape"/> that holds one child item.
        /// </summary>
        public const string ItemMemberName = "Item";

        /// <summary>
        ///     Prefix of the members of the child query's query shape that hold the link key values; the members
        ///     are named <c>Key1</c>, <c>Key2</c>, and so on, in the order of <see cref="ParentKeyColumns"/>.
        /// </summary>
        public const string KeyMemberNamePrefix = "Key";

        /// <summary>
        ///     Initializes a new instance of the <see cref="ChildQuery"/> class.
        /// </summary>
        public ChildQuery(SqlQueryableExpression source, SqlDerivedTableExpression query, IReadOnlyList<SqlExpression> parentKeyColumns)
        {
            this.Source = source ?? throw new ArgumentNullException(nameof(source));
            this.Query = query ?? throw new ArgumentNullException(nameof(query));
            this.ParentKeyColumns = parentKeyColumns ?? throw new ArgumentNullException(nameof(parentKeyColumns));
        }

        /// <summary>
        ///     The sub-query column as it was in the select list of the final query. The query shape of the final
        ///     query refers to this very instance, which is how a member is matched with its child query.
        /// </summary>
        public SqlQueryableExpression Source { get; }

        /// <summary>
        ///     The query to run. Its query shape is a member init with an <see cref="ItemMemberName"/> member (one child
        ///     item) followed by one <c>Key</c> member per <see cref="ParentKeyColumns"/> (the link values of the parent
        ///     the item belongs to).
        /// </summary>
        public SqlDerivedTableExpression Query { get; }

        /// <summary>
        ///     The columns of <see cref="ChildQueryExtractionResult.ParentQuery"/> select list holding the link values, in
        ///     the order of the <c>Key</c> members of <see cref="Query"/>. Empty if the sub-query is not linked to the parent.
        /// </summary>
        public IReadOnlyList<SqlExpression> ParentKeyColumns { get; }
    }

    /// <summary>
    ///     <para>
    ///         Result of <see cref="ChildQueryExtractor.Extract"/>.
    ///     </para>
    /// </summary>
    public class ChildQueryExtractionResult
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ChildQueryExtractionResult"/> class.
        /// </summary>
        public ChildQueryExtractionResult(SqlDerivedTableExpression parentQuery, IReadOnlyList<ChildQuery> childQueries)
        {
            this.ParentQuery = parentQuery ?? throw new ArgumentNullException(nameof(parentQuery));
            this.ChildQueries = childQueries ?? throw new ArgumentNullException(nameof(childQueries));
        }

        /// <summary>
        ///     The query to run for the main rows: the final query without its sub-query columns and with the
        ///     link columns added.
        /// </summary>
        public SqlDerivedTableExpression ParentQuery { get; }

        /// <summary>
        ///     One entry per sub-query column of the final query.
        /// </summary>
        public IReadOnlyList<ChildQuery> ChildQueries { get; }
    }

    /// <summary>
    ///     <para>
    ///         Splits the final query into the query for the main rows and one query per sub-query column.
    ///     </para>
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         By the time a query is final, the engine has already moved every sub-query column to the outermost
    ///         select and re-linked it to that select's own data sources, so the sub-query holds the full parent
    ///         query, filters and <c>Top</c> included, as its correlation.
    ///     </para>
    ///     <para>
    ///         The child query is built as: the parent query (without sub-query columns, reduced to the distinct
    ///         link values) as the first data source, and the sub-query joined to it, with the sub-query's
    ///         references to the parent replaced by the link columns. Rows are matched to their parent by the
    ///         link values, not by parent identity, so parents sharing a link value simply share the children.
    ///     </para>
    /// </remarks>
    public class ChildQueryExtractor
    {
        private readonly ISqlExpressionFactory sqlFactory;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ChildQueryExtractor"/> class.
        /// </summary>
        public ChildQueryExtractor(ISqlExpressionFactory sqlFactory)
        {
            this.sqlFactory = sqlFactory ?? throw new ArgumentNullException(nameof(sqlFactory));
        }

        /// <summary>
        ///     Splits <paramref name="root"/>. When it has no sub-query column the result holds it unchanged and no child query.
        /// </summary>
        public ChildQueryExtractionResult Extract(SqlDerivedTableExpression root)
        {
            if (root is null)
                throw new ArgumentNullException(nameof(root));

            if (root.SelectColumnCollection.SelectColumns.Any(x => x.ColumnExpression is SqlQueryableExpression))
                root = this.MakeRowLimitsDeterministic(root);

            var allColumns = root.SelectColumnCollection.SelectColumns;
            var queryableColumns = allColumns.Where(x => x.ColumnExpression is SqlQueryableExpression).ToArray();
            if (queryableColumns.Length == 0)
                return new ChildQueryExtractionResult(root, Array.Empty<ChildQuery>());

            if (root.GroupByClause.Count > 0)
                throw new NotSupportedException("A sub-query selected in the projection of a grouped query cannot be loaded as a separate query.");

            var ownAliases = new HashSet<Guid>(root.AllDataSources.Select(x => x.Alias));
            var parentColumns = allColumns.Where(x => !(x.ColumnExpression is SqlQueryableExpression)).ToList();

            // 1. which parent columns does each sub-query use to link itself to the parent
            var linkColumnsByQueryable = new List<(SqlQueryableExpression queryable, SqlDataSourceColumnExpression[] linkColumns)>();
            foreach (var column in queryableColumns)
            {
                var queryable = (SqlQueryableExpression)column.ColumnExpression;
                var linkColumns = DataSourceColumnUsageExtractor
                                    .FindDataSources(ownAliases)
                                    .In(queryable)
                                    .ExtractDataSourceColumnExpressions()
                                    .ToArray();
                linkColumnsByQueryable.Add((queryable, linkColumns));
            }

            // 2. the parent query returns those columns again as hidden columns of their own, and they come FIRST in
            //    the select list. The reader may be sequential (columns can only be read in ascending order), and a
            //    parent row needs its link values before the object is constructed, so they cannot sit among or after
            //    the columns that the construction reads. A hidden column is a new instance even when the query
            //    already selects the same column, so the two never share an ordinal.
            var parentColumnOf = new Dictionary<SqlDataSourceColumnExpression, SelectColumn>();
            var hiddenColumns = new List<SelectColumn>();
            foreach (var linkColumn in linkColumnsByQueryable.SelectMany(x => x.linkColumns).Distinct())
            {
                if (root.IsDistinct)
                    throw new NotSupportedException("A sub-query selected in the projection of a DISTINCT query cannot be loaded as a separate query: the columns that link it to the parent would have to be added to the DISTINCT select list.");
                var hidden = new SelectColumn(new SqlDataSourceColumnExpression(linkColumn.DataSourceAlias, linkColumn.ColumnName), "ChildKey" + (hiddenColumns.Count + 1), scalarColumn: false, helperColumn: true);
                hiddenColumns.Add(hidden);
                parentColumnOf[linkColumn] = hidden;
            }
            parentColumns.InsertRange(0, hiddenColumns);

            var parentQuery = root.Update(
                root.CteDataSources,
                root.FromSource,
                root.Joins,
                root.WhereClause,
                root.GroupByClause,
                root.HavingClause,
                root.OrderByClause,
                new SqlSelectListExpression(parentColumns));

            // 3. one child query per sub-query column
            var childQueries = new List<ChildQuery>();
            foreach (var (queryable, linkColumns) in linkColumnsByQueryable)
            {
                var parentKeyColumns = linkColumns.Select(x => parentColumnOf[x].ColumnExpression).ToArray();
                var linkSelectColumns = linkColumns.Select(x => parentColumnOf[x]).ToArray();
                var childQuery = this.BuildChildQuery(parentQuery, queryable, linkColumns, linkSelectColumns);
                childQueries.Add(new ChildQuery(queryable, childQuery, parentKeyColumns));
            }

            return new ChildQueryExtractionResult(parentQuery, childQueries);
        }

        /// <summary>
        ///     <para>
        ///         The child query repeats the parent query, so the parent query has to return the same rows both times.
        ///         A <c>TOP</c> / <c>OFFSET</c> picks its rows by the <c>ORDER BY</c>, and only a unique <c>ORDER BY</c>
        ///         picks the same ones every time. So in every row-limited query of the <c>FROM</c> chain the primary key
        ///         of its table is appended to the <c>ORDER BY</c> (or becomes the <c>ORDER BY</c>).
        ///     </para>
        ///     <para>
        ///         Only a query over a single table with a known primary key can be fixed this way. A row-limited query
        ///         that cannot be fixed and has no <c>ORDER BY</c> at all is refused, because its rows are arbitrary;
        ///         one that has an <c>ORDER BY</c> is left as the developer wrote it.
        ///     </para>
        /// </summary>
        private SqlDerivedTableExpression MakeRowLimitsDeterministic(SqlDerivedTableExpression query)
        {
            // AT-RMKS-01
            // TODO: why we are extracting fromSource and joins like this and then giving back to update the query?
            // Seems like her we are only trying to add OrderBy, if that's the case then does not make sense why
            // extracting fromSource and joins, we can just use query.FromSource and query.Joins directly in the update method.
            var fromSource = query.FromSource.Update(this.MakeRowLimitsDeterministic(query.FromSource.QuerySource));
            var joins = query.Joins.Select(x => x.Update(this.MakeRowLimitsDeterministic(x.QuerySource), x.JoinCondition)).ToArray();
            var orderBy = query.OrderByClause;

            if (query.Top != null || query.RowOffset != null)
            {
                var primaryKeyColumns = this.GetPrimaryKeyColumns(query);
                if (primaryKeyColumns.Count > 0)
                {
                    var orderByColumns = orderBy?.OrderByColumns.ToList() ?? new List<OrderByColumn>();
                    foreach (var primaryKeyColumn in primaryKeyColumns)
                    {
                        if (!orderByColumns.Any(x => x.ColumnExpression.Equals(primaryKeyColumn)))
                            orderByColumns.Add(new OrderByColumn(primaryKeyColumn, SortDirection.Ascending));
                    }
                    orderBy = new SqlOrderByClauseExpression(orderByColumns);
                }
                else if (!(orderBy?.OrderByColumns.Count > 0))
                {
                    throw new NotSupportedException("A sub-query is loaded as a separate query, which repeats the parent query, so the parent query must return the same rows both times. The parent query has Top / Skip without an OrderBy and its rows cannot be ordered by a primary key (it is not a single table with a primary key), so which rows it returns is arbitrary. Add an OrderBy to the parent query.");
                }
            }

            return query.Update(query.CteDataSources, fromSource, joins, query.WhereClause, query.GroupByClause, query.HavingClause, orderBy, query.SelectColumnCollection);
        }

        private SqlQuerySourceExpression MakeRowLimitsDeterministic(SqlQuerySourceExpression querySource)
            => querySource is SqlDerivedTableExpression derivedTable && derivedTable.NodeType == SqlExpressionType.DerivedTable
                ? this.MakeRowLimitsDeterministic(derivedTable)
                : querySource;

        private IReadOnlyList<SqlDataSourceColumnExpression> GetPrimaryKeyColumns(SqlDerivedTableExpression query)
        {
            if (query.Joins.Count > 0 || query.GroupByClause.Count > 0 || query.IsDistinct || query.CteDataSources.Count > 0)
                return Array.Empty<SqlDataSourceColumnExpression>();
            if (!(query.FromSource.QuerySource is SqlTableExpression table))
                return Array.Empty<SqlDataSourceColumnExpression>();
            return table.TableColumns
                        .Where(x => x.IsPrimaryKey)
                        .Select(x => new SqlDataSourceColumnExpression(query.FromSource.Alias, x.DatabaseColumnName))
                        .ToArray();
        }

        private SqlDerivedTableExpression BuildChildQuery(SqlDerivedTableExpression parentQuery, SqlQueryableExpression queryable, SqlDataSourceColumnExpression[] linkColumns, SelectColumn[] linkSelectColumns)
        {
            if (linkColumns.Length == 0)
            {
                // not linked to the parent at all, so every parent gets the same rows and there is nothing to join to
                var standalone = this.sqlFactory.CreateSelectQuery(queryable.Query);
                var itemShape = standalone.GetQueryShapeForDataSourceMapping();
                standalone.ApplyProjection(new SqlMemberInitExpression(new[] { new SqlMemberAssignment(ChildQuery.ItemMemberName, itemShape) }));
                return this.sqlFactory.ConvertSelectQueryToDeriveTable(standalone);
            }

            // The distinct link values of the parent rows that survive the parent's filters and Top. Order by is
            // not allowed in a derived table unless it comes with a row limit, and it does not matter here.
            var keysSource = parentQuery;
            if (parentQuery.OrderByClause != null && parentQuery.Top == null && parentQuery.RowOffset == null)
            {
                keysSource = parentQuery.Update(
                    parentQuery.CteDataSources,
                    parentQuery.FromSource,
                    parentQuery.Joins,
                    parentQuery.WhereClause,
                    parentQuery.GroupByClause,
                    parentQuery.HavingClause,
                    orderByClause: null,
                    parentQuery.SelectColumnCollection);
            }

            var keysSelect = this.sqlFactory.CreateSelectQuery(keysSource);
            var keysSourceAlias = keysSelect.DataSources.First().Alias;
            keysSelect.ApplyProjection(new SqlMemberInitExpression(
                linkSelectColumns.Select(x => new SqlMemberAssignment(x.Alias, new SqlDataSourceColumnExpression(keysSourceAlias, x.Alias))).ToArray()));
            keysSelect.ApplyDistinct();
            var distinctKeys = this.sqlFactory.ConvertSelectQueryToDeriveTable(keysSelect);

            var childSelect = this.sqlFactory.CreateSelectQuery(distinctKeys);
            var distinctKeysAlias = childSelect.DataSources.First().Alias;

            // the sub-query talks to the parent through the parent's own columns, now it has to talk through the link columns
            var replaceWith = linkColumns
                                .Select((column, i) => new SelectColumn(column, linkSelectColumns[i].Alias, scalarColumn: false))
                                .ToArray();
            var rewritten = SubQueryProjectionReplacementVisitor.FindAndReplace(replaceWith, new AliasedDataSource(distinctKeys, distinctKeysAlias), queryable.Query) as SqlDerivedTableExpression
                            ??
                            throw new InvalidOperationException($"Expected '{nameof(SqlDerivedTableExpression)}' after the sub-query was linked to the link columns.");

            var itemQueryShape = childSelect.AddDataSourceWithJoinResolution(rewritten, isDefaultIfEmpty: false);

            var bindings = new List<SqlMemberAssignment> { new SqlMemberAssignment(ChildQuery.ItemMemberName, itemQueryShape) };
            for (var i = 0; i < linkSelectColumns.Length; i++)
                bindings.Add(new SqlMemberAssignment(ChildQuery.KeyMemberNamePrefix + (i + 1), new SqlDataSourceColumnExpression(distinctKeysAlias, linkSelectColumns[i].Alias)));
            childSelect.ApplyProjection(new SqlMemberInitExpression(bindings));

            return this.sqlFactory.ConvertSelectQueryToDeriveTable(childSelect);
        }
    }
}
