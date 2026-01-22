using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Atis.Orm
{
    /// <summary>
    ///     <para>
    ///         Abstract base class for translating SQL expressions to SQL strings.
    ///     </para>
    ///     <para>
    ///         This class provides the core translation logic and can be extended
    ///         to support database-specific SQL syntax.
    ///     </para>
    /// </summary>
    public class SqlExpressionTranslatorBase : ISqlExpressionTranslator
    {
        /// <summary>
        ///     <para>
        ///         Gets the collection of query parameters generated during translation.
        ///     </para>
        /// </summary>
        protected List<IQueryParameter> Parameters { get; } = new List<IQueryParameter>();
        private int parameterCounter;
        private Dictionary<Guid, string> aliasCache;

        /// <summary>
        ///     <para>
        ///         Translates a SQL expression tree to a SQL string with parameters.
        ///     </para>
        /// </summary>
        /// <param name="sqlExpression">The SQL expression to translate.</param>
        /// <returns>A <see cref="TranslationResult"/> containing the SQL string and parameters.</returns>
        public TranslationResult Translate(SqlExpression sqlExpression)
        {
            this.Parameters.Clear();
            this.parameterCounter = 0;
            this.aliasCache = new Dictionary<Guid, string>();

            var sql = this.TranslateExpression(sqlExpression);

            return new TranslationResult(sql, this.Parameters);
        }

        #region Parameter and Alias Helpers

        /// <summary>
        ///     <para>
        ///         Generates the next parameter name.
        ///     </para>
        /// </summary>
        /// <returns>A parameter name like "@p0", "@p1", etc.</returns>
        protected virtual string GenerateParameterName()
        {
            return $"@p{this.parameterCounter++}";
        }

        /// <summary>
        ///     <para>
        ///         Creates a query parameter and adds it to the parameters collection.
        ///     </para>
        ///     <para>
        ///         Override this method to provide a custom <see cref="IQueryParameter"/> implementation.
        ///     </para>
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <param name="value">The parameter value.</param>
        /// <param name="isLiteral">Whether this is a literal value.</param>
        /// <param name="sourceExpression">The source SQL expression.</param>
        /// <returns>The created query parameter.</returns>
        protected virtual IQueryParameter CreateQueryParameter(string name, object value, bool isLiteral, SqlExpression sourceExpression)
        {
            return new QueryParameter(name, value, isLiteral, sourceExpression);
        }

        /// <summary>
        ///     <para>
        ///         Gets or creates a simplified alias for a data source GUID.
        ///     </para>
        /// </summary>
        /// <param name="aliasGuid">The GUID of the data source.</param>
        /// <param name="prefix">Optional prefix for the alias (default is "t").</param>
        /// <returns>A simplified alias like "t1", "t2", etc.</returns>
        protected virtual string GetAlias(Guid aliasGuid, string prefix = null)
        {
            if (!this.aliasCache.TryGetValue(aliasGuid, out var alias))
            {
                alias = $"{prefix ?? "t"}{this.aliasCache.Count + 1}";
                this.aliasCache.Add(aliasGuid, alias);
            }
            return alias;
        }

        #endregion

        #region Main Dispatch

        /// <summary>
        ///     <para>
        ///         Main dispatch method that routes to the appropriate translation method based on expression type.
        ///     </para>
        /// </summary>
        /// <param name="node">The SQL expression to translate.</param>
        /// <returns>The translated SQL string.</returns>
        protected virtual string TranslateExpression(SqlExpression node)
        {
            if (node == null)
                return string.Empty;

            if (node is SqlLiteralExpression literal)
                return this.TranslateLiteral(literal);
            else if (node is SqlParameterExpression parameter)
                return this.TranslateParameter(parameter);
            else if (node is SqlBinaryExpression binary)
                return this.TranslateBinary(binary);
            else if (node is SqlDataSourceColumnExpression column)
                return this.TranslateDataSourceColumn(column);
            else if (node is SqlTableExpression table)
                return this.TranslateTable(table);
            else if (node is SqlDerivedTableExpression derivedTable)
                return this.TranslateDerivedTable(derivedTable);
            else if (node is SqlAliasedFromSourceExpression aliasedFromSource)
                return this.TranslateAliasedFromSource(aliasedFromSource);
            else if (node is SqlAliasedJoinSourceExpression aliasedJoinSource)
                return this.TranslateAliasedJoinSource(aliasedJoinSource);
            else if (node is SqlAliasedCteSourceExpression aliasedCteSource)
                return this.TranslateAliasedCteSource(aliasedCteSource);
            else if (node is SqlCteReferenceExpression cteReference)
                return this.TranslateCteReference(cteReference);
            else if (node is SqlAliasExpression alias)
                return this.TranslateAlias(alias);
            else if (node is SqlFunctionCallExpression functionCall)
                return this.TranslateFunctionCall(functionCall);
            else if (node is SqlExistsExpression exists)
                return this.TranslateExists(exists);
            else if (node is SqlConditionalExpression conditional)
                return this.TranslateConditional(conditional);
            else if (node is SqlNotExpression not)
                return this.TranslateNot(not);
            else if (node is SqlNegateExpression negate)
                return this.TranslateNegate(negate);
            else if (node is SqlInValuesExpression inValues)
                return this.TranslateInValues(inValues);
            else if (node is SqlLikeExpression like)
                return this.TranslateLike(like);
            else if (node is SqlCastExpression cast)
                return this.TranslateCast(cast);
            else if (node is SqlDateAddExpression dateAdd)
                return this.TranslateDateAdd(dateAdd);
            else if (node is SqlDatePartExpression datePart)
                return this.TranslateDatePart(datePart);
            else if (node is SqlDateSubtractExpression dateSubtract)
                return this.TranslateDateSubtract(dateSubtract);
            else if (node is SqlStringFunctionExpression stringFunction)
                return this.TranslateStringFunction(stringFunction);
            else if (node is SqlCollectionExpression collection)
                return this.TranslateCollection(collection);
            else if (node is SqlUnionQueryExpression unionQuery)
                return this.TranslateUnionQuery(unionQuery);
            else if (node is SqlStandaloneSelectExpression standaloneSelect)
                return this.TranslateStandaloneSelect(standaloneSelect);
            else if (node is SqlUpdateExpression update)
                return this.TranslateUpdate(update);
            else if (node is SqlDeleteExpression delete)
                return this.TranslateDelete(delete);
            else if (node is SqlInsertIntoExpression insertInto)
                return this.TranslateInsertInto(insertInto);
            else if (node is SqlNewGuidExpression newGuid)
                return this.TranslateNewGuid(newGuid);
            else if (node is SqlCommentExpression comment)
                return this.TranslateComment(comment);
            else if (node is SqlFragmentExpression fragment)
                return this.TranslateFragment(fragment);
            else if (node is SqlQueryableExpression queryable)
                return this.TranslateQueryable(queryable);
            else
                return this.TranslateUnknown(node);
        }

        /// <summary>
        ///     <para>
        ///         Handles unknown or unsupported expression types.
        ///     </para>
        ///     <para>
        ///         Override this method to provide custom handling for additional expression types.
        ///     </para>
        /// </summary>
        /// <param name="node">The unknown SQL expression.</param>
        /// <returns>The translated SQL string.</returns>
        protected virtual string TranslateUnknown(SqlExpression node)
        {
            throw new NotSupportedException($"SQL expression type '{node?.GetType().Name}' is not supported.");
        }

        #endregion

        #region Literal and Parameter Translation

        /// <summary>
        ///     <para>
        ///         Translates a literal expression to a parameter placeholder.
        ///     </para>
        /// </summary>
        protected virtual string TranslateLiteral(SqlLiteralExpression node)
        {
            var paramName = this.GenerateParameterName();
            var queryParameter = this.CreateQueryParameter(paramName, node.LiteralValue, isLiteral: true, node);
            this.Parameters.Add(queryParameter);
            return paramName;
        }

        /// <summary>
        ///     <para>
        ///         Translates a parameter expression to a parameter placeholder.
        ///     </para>
        /// </summary>
        protected virtual string TranslateParameter(SqlParameterExpression node)
        {
            var paramName = this.GenerateParameterName();
            var queryParameter = this.CreateQueryParameter(paramName, node.Value, isLiteral: false, node);
            this.Parameters.Add(queryParameter);
            return paramName;
        }

        #endregion

        #region Binary Expression Translation

        /// <summary>
        ///     <para>
        ///         Translates a binary expression.
        ///     </para>
        /// </summary>
        protected virtual string TranslateBinary(SqlBinaryExpression node)
        {
            if (node.NodeType == SqlExpressionType.Coalesce)
            {
                return this.TranslateCoalesce(node.Left, node.Right);
            }

            // Handle null comparisons
            if (this.IsNullExpression(node.Right))
            {
                if (node.NodeType == SqlExpressionType.Equal)
                    return $"({this.TranslateExpression(node.Left)} IS NULL)";
                else if (node.NodeType == SqlExpressionType.NotEqual)
                    return $"({this.TranslateExpression(node.Left)} IS NOT NULL)";
            }

            string left;
            string right;

            if (node.NodeType == SqlExpressionType.AndAlso || node.NodeType == SqlExpressionType.OrElse)
            {
                left = this.TranslateAsLogicalExpression(node.Left);
                right = this.TranslateAsLogicalExpression(node.Right);
            }
            else
            {
                left = this.TranslateAsNonLogicalExpression(node.Left);
                right = this.TranslateAsNonLogicalExpression(node.Right);
            }

            var op = this.GetBinaryOperator(node.NodeType);
            return $"({left} {op} {right})";
        }

        /// <summary>
        ///     <para>
        ///         Translates a COALESCE expression.
        ///     </para>
        ///     <para>
        ///         Override this method for database-specific syntax (e.g., ISNULL for SQL Server).
        ///     </para>
        /// </summary>
        protected virtual string TranslateCoalesce(SqlExpression left, SqlExpression right)
        {
            var leftSql = this.TranslateAsNonLogicalExpression(left);
            var rightSql = this.TranslateAsNonLogicalExpression(right);
            return $"COALESCE({leftSql}, {rightSql})";
        }

        /// <summary>
        ///     <para>
        ///         Gets the SQL operator for a binary expression type.
        ///     </para>
        /// </summary>
        protected virtual string GetBinaryOperator(SqlExpressionType nodeType)
        {
            switch (nodeType)
            {
                case SqlExpressionType.Add: return "+";
                case SqlExpressionType.Subtract: return "-";
                case SqlExpressionType.Multiply: return "*";
                case SqlExpressionType.Divide: return "/";
                case SqlExpressionType.Modulus: return "%";
                case SqlExpressionType.Equal: return "=";
                case SqlExpressionType.NotEqual: return "<>";
                case SqlExpressionType.GreaterThan: return ">";
                case SqlExpressionType.GreaterThanOrEqual: return ">=";
                case SqlExpressionType.LessThan: return "<";
                case SqlExpressionType.LessThanOrEqual: return "<=";
                case SqlExpressionType.AndAlso: return "AND";
                case SqlExpressionType.OrElse: return "OR";
                default: return "<unknown_op>";
            }
        }

        /// <summary>
        ///     <para>
        ///         Checks if an expression represents a null value.
        ///     </para>
        /// </summary>
        protected virtual bool IsNullExpression(SqlExpression node)
        {
            if (node is SqlLiteralExpression literal)
                return literal.LiteralValue == null;
            if (node is SqlParameterExpression parameter)
                return parameter.Value == null;
            return false;
        }

        /// <summary>
        ///     <para>
        ///         Checks if an expression is a logical (boolean) expression.
        ///     </para>
        /// </summary>
        protected virtual bool IsLogicalExpression(SqlExpression node)
        {
            var nt = node.NodeType;
            return nt == SqlExpressionType.AndAlso ||
                   nt == SqlExpressionType.OrElse ||
                   nt == SqlExpressionType.GreaterThan ||
                   nt == SqlExpressionType.GreaterThanOrEqual ||
                   nt == SqlExpressionType.LessThan ||
                   nt == SqlExpressionType.LessThanOrEqual ||
                   nt == SqlExpressionType.Equal ||
                   nt == SqlExpressionType.NotEqual ||
                   nt == SqlExpressionType.Like ||
                   nt == SqlExpressionType.LikeStartsWith ||
                   nt == SqlExpressionType.LikeEndsWith ||
                   nt == SqlExpressionType.InValues ||
                   nt == SqlExpressionType.Not ||
                   nt == SqlExpressionType.Exists;
        }

        /// <summary>
        ///     <para>
        ///         Ensures an expression is translated as a logical expression.
        ///     </para>
        ///     <para>
        ///         If the expression is not logical, wraps it as "expression = true".
        ///     </para>
        /// </summary>
        protected virtual string TranslateAsLogicalExpression(SqlExpression node)
        {
            if (!this.IsLogicalExpression(node))
            {
                var expr = this.TranslateExpression(node);
                return $"({expr} = 1)";
            }
            return this.TranslateExpression(node);
        }

        /// <summary>
        ///     <para>
        ///         Ensures an expression is translated as a non-logical (value) expression.
        ///     </para>
        ///     <para>
        ///         If the expression is logical, wraps it in a CASE expression.
        ///     </para>
        /// </summary>
        protected virtual string TranslateAsNonLogicalExpression(SqlExpression node)
        {
            if (this.IsLogicalExpression(node))
            {
                var expr = this.TranslateExpression(node);
                return $"CASE WHEN {expr} THEN 1 ELSE 0 END";
            }
            return this.TranslateExpression(node);
        }

        #endregion

        #region Column and Table Translation

        /// <summary>
        ///     <para>
        ///         Translates a data source column reference.
        ///     </para>
        /// </summary>
        protected virtual string TranslateDataSourceColumn(SqlDataSourceColumnExpression node)
        {
            return $"{this.GetAlias(node.DataSourceAlias)}.{node.ColumnName}";
        }

        /// <summary>
        ///     <para>
        ///         Translates a table reference.
        ///     </para>
        /// </summary>
        protected virtual string TranslateTable(SqlTableExpression node)
        {
            return node.SqlTable.TableName;
        }

        /// <summary>
        ///     <para>
        ///         Translates an alias expression.
        ///     </para>
        /// </summary>
        protected virtual string TranslateAlias(SqlAliasExpression node)
        {
            return node.ColumnAlias;
        }

        #endregion

        #region Derived Table Translation

        /// <summary>
        ///     <para>
        ///         Translates a derived table (subquery).
        ///     </para>
        /// </summary>
        protected virtual string TranslateDerivedTable(SqlDerivedTableExpression node)
        {
            var parts = new List<string>();

            // CTE definitions
            var cteClause = this.TranslateCteDataSources(node.CteDataSources);
            if (!string.IsNullOrEmpty(cteClause))
                parts.Add(cteClause);

            // SELECT clause
            var selectClause = this.TranslateSelectClause(node);
            if (!string.IsNullOrEmpty(selectClause))
                parts.Add(selectClause);

            // FROM clause
            var fromClause = this.TranslateFromClause(node.FromSource);
            parts.Add(fromClause);

            // JOIN clauses
            var joinsClause = this.TranslateJoins(node.Joins);
            if (!string.IsNullOrEmpty(joinsClause))
                parts.Add(joinsClause);

            // WHERE clause
            var whereClause = this.TranslateFilterClause(node.WhereClause, "WHERE");
            if (!string.IsNullOrEmpty(whereClause))
                parts.Add(whereClause);

            // GROUP BY clause
            var groupByClause = this.TranslateGroupByClause(node.GroupByClause);
            if (!string.IsNullOrEmpty(groupByClause))
                parts.Add(groupByClause);

            // HAVING clause
            var havingClause = this.TranslateFilterClause(node.HavingClause, "HAVING");
            if (!string.IsNullOrEmpty(havingClause))
                parts.Add(havingClause);

            // ORDER BY clause
            var orderByClause = this.TranslateOrderByClause(node.OrderByClause);
            if (!string.IsNullOrEmpty(orderByClause))
                parts.Add(orderByClause);

            // Paging (OFFSET/FETCH)
            var pagingClause = this.TranslatePaging(node.RowOffset, node.RowsPerPage);
            if (!string.IsNullOrEmpty(pagingClause))
                parts.Add(pagingClause);

            var query = string.Join("\r\n", parts);
            return $"(\r\n{query}\r\n)";
        }

        /// <summary>
        ///     <para>
        ///         Translates the SELECT clause of a derived table.
        ///     </para>
        /// </summary>
        protected virtual string TranslateSelectClause(SqlDerivedTableExpression node)
        {
            if (node.SelectColumnCollection == null)
                return string.Empty;

            var distinct = node.IsDistinct ? "DISTINCT " : string.Empty;
            var top = node.Top > 0 ? $"TOP ({node.Top}) " : string.Empty;
            var columns = this.TranslateSelectColumns(node.SelectColumnCollection.SelectColumns);

            return $"SELECT {distinct}{top}{columns}";
        }

        /// <summary>
        ///     <para>
        ///         Translates a list of select columns.
        ///     </para>
        /// </summary>
        protected virtual string TranslateSelectColumns(IReadOnlyList<SelectColumn> selectColumns)
        {
            var columnStrings = new List<string>();
            foreach (var col in selectColumns)
            {
                var expr = this.TranslateAsNonLogicalExpression(col.ColumnExpression);
                columnStrings.Add($"{expr} AS {col.Alias}");
            }
            return string.Join(", ", columnStrings);
        }

        /// <summary>
        ///     <para>
        ///         Translates the FROM clause.
        ///     </para>
        /// </summary>
        protected virtual string TranslateFromClause(SqlAliasedFromSourceExpression fromSource)
        {
            if (fromSource == null)
                return string.Empty;
            var source = this.TranslateAliasedFromSource(fromSource);
            return $"FROM {source}";
        }

        /// <summary>
        ///     <para>
        ///         Translates an aliased FROM source.
        ///     </para>
        /// </summary>
        protected virtual string TranslateAliasedFromSource(SqlAliasedFromSourceExpression node)
        {
            var source = this.TranslateExpression(node.QuerySource);
            var alias = this.GetAlias(node.Alias);
            return $"{source} AS {alias}";
        }

        /// <summary>
        ///     <para>
        ///         Translates JOIN clauses.
        ///     </para>
        /// </summary>
        protected virtual string TranslateJoins(IReadOnlyList<SqlAliasedJoinSourceExpression> joins)
        {
            if (joins == null || joins.Count == 0)
                return string.Empty;

            var joinStrings = joins.Select(j => this.TranslateAliasedJoinSource(j));
            return string.Join("\r\n", joinStrings);
        }

        /// <summary>
        ///     <para>
        ///         Translates an aliased JOIN source.
        ///     </para>
        /// </summary>
        protected virtual string TranslateAliasedJoinSource(SqlAliasedJoinSourceExpression node)
        {
            var joinType = this.GetJoinTypeKeyword(node.JoinType);
            var source = this.TranslateExpression(node.QuerySource);
            var alias = this.GetAlias(node.Alias, node.JoinName);
            var onClause = node.JoinCondition != null
                ? $" ON {this.TranslateAsLogicalExpression(node.JoinCondition)}"
                : string.Empty;
            return $"{joinType} {source} AS {alias}{onClause}";
        }

        /// <summary>
        ///     <para>
        ///         Gets the SQL keyword for a join type.
        ///     </para>
        /// </summary>
        protected virtual string GetJoinTypeKeyword(SqlJoinType joinType)
        {
            switch (joinType)
            {
                case SqlJoinType.Inner: return "INNER JOIN";
                case SqlJoinType.Left: return "LEFT JOIN";
                case SqlJoinType.Right: return "RIGHT JOIN";
                case SqlJoinType.Cross: return "CROSS JOIN";
                case SqlJoinType.OuterApply: return "OUTER APPLY";
                case SqlJoinType.CrossApply: return "CROSS APPLY";
                case SqlJoinType.FullOuter: return "FULL OUTER JOIN";
                default: return joinType.ToString();
            }
        }

        /// <summary>
        ///     <para>
        ///         Translates a filter clause (WHERE or HAVING).
        ///     </para>
        /// </summary>
        protected virtual string TranslateFilterClause(SqlFilterClauseExpression filterClause, string keyword)
        {
            if (filterClause == null || filterClause.FilterConditions.Count == 0)
                return string.Empty;

            var conditions = new List<string>();
            for (var i = 0; i < filterClause.FilterConditions.Count; i++)
            {
                var condition = filterClause.FilterConditions[i];
                var translated = this.TranslateAsLogicalExpression(condition.Predicate);

                if (i == 0)
                {
                    conditions.Add(translated);
                }
                else
                {
                    var connector = condition.UseOrOperator ? "OR" : "AND";
                    conditions.Add($"{connector} {translated}");
                }
            }

            return $"{keyword} {string.Join(" ", conditions)}";
        }

        /// <summary>
        ///     <para>
        ///         Translates the GROUP BY clause.
        ///     </para>
        /// </summary>
        protected virtual string TranslateGroupByClause(IReadOnlyList<SqlExpression> groupByClause)
        {
            if (groupByClause == null || groupByClause.Count == 0)
                return string.Empty;

            var columns = groupByClause.Select(g => this.TranslateExpression(g));
            return $"GROUP BY {string.Join(", ", columns)}";
        }

        /// <summary>
        ///     <para>
        ///         Translates the ORDER BY clause.
        ///     </para>
        /// </summary>
        protected virtual string TranslateOrderByClause(SqlOrderByClauseExpression orderByClause)
        {
            if (orderByClause == null || orderByClause.OrderByColumns.Count == 0)
                return string.Empty;

            var columns = orderByClause.OrderByColumns.Select(o =>
            {
                var col = this.TranslateExpression(o.ColumnExpression);
                var dir = o.Direction == SortDirection.Ascending ? "ASC" : "DESC";
                return $"{col} {dir}";
            });
            return $"ORDER BY {string.Join(", ", columns)}";
        }

        /// <summary>
        ///     <para>
        ///         Translates paging (OFFSET/FETCH).
        ///     </para>
        ///     <para>
        ///         Override this method for database-specific paging syntax.
        ///     </para>
        /// </summary>
        protected virtual string TranslatePaging(int? rowOffset, int? rowsPerPage)
        {
            if (rowOffset == null || rowsPerPage == null)
                return string.Empty;

            return $"OFFSET {rowOffset} ROWS FETCH NEXT {rowsPerPage} ROWS ONLY";
        }

        #endregion

        #region CTE Translation

        /// <summary>
        ///     <para>
        ///         Translates CTE (Common Table Expression) definitions.
        ///     </para>
        /// </summary>
        protected virtual string TranslateCteDataSources(IReadOnlyList<SqlAliasedCteSourceExpression> cteDataSources)
        {
            if (cteDataSources == null || cteDataSources.Count == 0)
                return string.Empty;

            var ctes = cteDataSources.Select(c => this.TranslateAliasedCteSource(c));
            return $"WITH {string.Join(", ", ctes)}";
        }

        /// <summary>
        ///     <para>
        ///         Translates an aliased CTE source.
        ///     </para>
        /// </summary>
        protected virtual string TranslateAliasedCteSource(SqlAliasedCteSourceExpression node)
        {
            var alias = this.GetAlias(node.CteAlias, "cte");
            var body = this.TranslateExpression(node.CteBody);
            return $"{alias} AS\r\n{body}";
        }

        /// <summary>
        ///     <para>
        ///         Translates a CTE reference.
        ///     </para>
        /// </summary>
        protected virtual string TranslateCteReference(SqlCteReferenceExpression node)
        {
            return this.GetAlias(node.CteAlias, "cte");
        }

        #endregion

        #region Function Translation

        /// <summary>
        ///     <para>
        ///         Translates a function call expression.
        ///     </para>
        /// </summary>
        protected virtual string TranslateFunctionCall(SqlFunctionCallExpression node)
        {
            var args = node.Arguments != null
                ? string.Join(", ", node.Arguments.Select(a => this.TranslateAsNonLogicalExpression(a)))
                : string.Empty;

            // Special handling for COUNT with no arguments
            if (node.FunctionName.Equals("Count", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(args))
            {
                args = "1";
            }

            return $"{node.FunctionName}({args})";
        }

        /// <summary>
        ///     <para>
        ///         Translates a string function expression.
        ///     </para>
        /// </summary>
        protected virtual string TranslateStringFunction(SqlStringFunctionExpression node)
        {
            var stringExpr = this.TranslateExpression(node.StringExpression);
            var args = node.Arguments?.Count > 0
                ? ", " + string.Join(", ", node.Arguments.Select(a => this.TranslateExpression(a)))
                : string.Empty;
            return $"{node.StringFunction}({stringExpr}{args})";
        }

        /// <summary>
        ///     <para>
        ///         Translates a DATEADD expression.
        ///     </para>
        /// </summary>
        protected virtual string TranslateDateAdd(SqlDateAddExpression node)
        {
            var interval = this.TranslateExpression(node.Interval);
            var dateExpr = this.TranslateExpression(node.DateExpression);
            return $"DATEADD({node.DatePart}, {interval}, {dateExpr})";
        }

        /// <summary>
        ///     <para>
        ///         Translates a DATEPART expression.
        ///     </para>
        /// </summary>
        protected virtual string TranslateDatePart(SqlDatePartExpression node)
        {
            var dateExpr = this.TranslateExpression(node.DateExpression);
            return $"DATEPART({node.DatePart}, {dateExpr})";
        }

        /// <summary>
        ///     <para>
        ///         Translates a date subtraction expression.
        ///     </para>
        /// </summary>
        protected virtual string TranslateDateSubtract(SqlDateSubtractExpression node)
        {
            var startDate = this.TranslateExpression(node.StartDate);
            var endDate = this.TranslateExpression(node.EndDate);
            return $"DATEDIFF({node.DatePart}, {startDate}, {endDate})";
        }

        /// <summary>
        ///     <para>
        ///         Translates a NEWGUID expression.
        ///     </para>
        ///     <para>
        ///         Override this method for database-specific syntax.
        ///     </para>
        /// </summary>
        protected virtual string TranslateNewGuid(SqlNewGuidExpression node)
        {
            return "NEWID()";
        }

        #endregion

        #region Logical Expression Translation

        /// <summary>
        ///     <para>
        ///         Translates an EXISTS expression.
        ///     </para>
        /// </summary>
        protected virtual string TranslateExists(SqlExistsExpression node)
        {
            var subQuery = this.TranslateExpression(node.SubQuery);
            return $"EXISTS{subQuery}";
        }

        /// <summary>
        ///     <para>
        ///         Translates a conditional (CASE WHEN) expression.
        ///     </para>
        /// </summary>
        protected virtual string TranslateConditional(SqlConditionalExpression node)
        {
            var test = this.TranslateAsLogicalExpression(node.Test);
            var ifTrue = this.TranslateAsNonLogicalExpression(node.IfTrue);
            var ifFalse = this.TranslateAsNonLogicalExpression(node.IfFalse);
            return $"CASE WHEN {test} THEN {ifTrue} ELSE {ifFalse} END";
        }

        /// <summary>
        ///     <para>
        ///         Translates a NOT expression.
        ///     </para>
        /// </summary>
        protected virtual string TranslateNot(SqlNotExpression node)
        {
            var operand = this.TranslateAsLogicalExpression(node.Operand);
            return $"NOT {operand}";
        }

        /// <summary>
        ///     <para>
        ///         Translates a negate (-) expression.
        ///     </para>
        /// </summary>
        protected virtual string TranslateNegate(SqlNegateExpression node)
        {
            var operand = this.TranslateExpression(node.Operand);
            return $"-{operand}";
        }

        /// <summary>
        ///     <para>
        ///         Translates an IN VALUES expression.
        ///     </para>
        /// </summary>
        protected virtual string TranslateInValues(SqlInValuesExpression node)
        {
            var expr = this.TranslateExpression(node.Expression);
            var values = string.Join(", ", node.Values.Select(v => this.TranslateExpression(v)));
            return $"{expr} IN ({values})";
        }

        /// <summary>
        ///     <para>
        ///         Translates a LIKE expression.
        ///     </para>
        /// </summary>
        protected virtual string TranslateLike(SqlLikeExpression node)
        {
            var expr = this.TranslateExpression(node.Expression);
            var pattern = this.TranslateExpression(node.Pattern);

            switch (node.NodeType)
            {
                case SqlExpressionType.LikeStartsWith:
                    return $"({expr} LIKE {pattern} + '%')";
                case SqlExpressionType.LikeEndsWith:
                    return $"({expr} LIKE '%' + {pattern})";
                default: // SqlExpressionType.Like (contains)
                    return $"({expr} LIKE '%' + {pattern} + '%')";
            }
        }

        /// <summary>
        ///     <para>
        ///         Translates a CAST expression.
        ///     </para>
        /// </summary>
        protected virtual string TranslateCast(SqlCastExpression node)
        {
            var expr = this.TranslateExpression(node.Expression);
            var dataType = this.TranslateDataType(node.SqlDataType);
            return $"CAST({expr} AS {dataType})";
        }

        /// <summary>
        ///     <para>
        ///         Translates a SQL data type.
        ///     </para>
        /// </summary>
        protected virtual string TranslateDataType(ISqlDataType sqlDataType)
        {
            var length = string.Empty;
            if (sqlDataType.UseMaxLength)
            {
                length = "(MAX)";
            }
            else if (sqlDataType.Length != null)
            {
                length = $"({sqlDataType.Length})";
            }

            var decimalParams = string.Empty;
            if (sqlDataType.Precision != null && sqlDataType.Scale != null)
            {
                decimalParams = $"({sqlDataType.Precision}, {sqlDataType.Scale})";
            }

            return $"{sqlDataType.DbType}{length}{decimalParams}";
        }

        #endregion

        #region Collection and Union Translation

        /// <summary>
        ///     <para>
        ///         Translates a collection expression.
        ///     </para>
        /// </summary>
        protected virtual string TranslateCollection(SqlCollectionExpression node)
        {
            return string.Join(", ", node.SqlExpressions.Select(e => this.TranslateExpression(e)));
        }

        /// <summary>
        ///     <para>
        ///         Translates a UNION query expression.
        ///     </para>
        /// </summary>
        protected virtual string TranslateUnionQuery(SqlUnionQueryExpression node)
        {
            var parts = new List<string>();
            for (var i = 0; i < node.Unions.Count; i++)
            {
                var unionItem = node.Unions[i];
                var query = this.TranslateExpression(unionItem.DerivedTable);
                // Remove outer parentheses if present
                query = this.RemoveOuterParentheses(query);

                if (i > 0)
                {
                    var unionKeyword = unionItem.UnionType == SqlUnionType.UnionAll ? "UNION ALL" : "UNION";
                    parts.Add(unionKeyword);
                }
                parts.Add(query);
            }
            return $"(\r\n{string.Join("\r\n", parts)}\r\n)";
        }

        /// <summary>
        ///     <para>
        ///         Translates a standalone SELECT expression.
        ///     </para>
        /// </summary>
        protected virtual string TranslateStandaloneSelect(SqlStandaloneSelectExpression node)
        {
            var columns = this.TranslateSelectColumns(node.SelectList);
            return $"(SELECT {columns})";
        }

        #endregion

        #region DML Translation (UPDATE, DELETE, INSERT)

        /// <summary>
        ///     <para>
        ///         Translates an UPDATE expression.
        ///     </para>
        /// </summary>
        protected virtual string TranslateUpdate(SqlUpdateExpression node)
        {
            var alias = this.GetAlias(node.DataSource);
            var setClause = string.Join(",\r\n\t", node.Columns.Zip(node.Values, (c, v) => $"{c} = {this.TranslateExpression(v)}"));
            var source = this.TranslateExpression(node.Source);
            source = this.RemoveOuterParentheses(source);
            return $"UPDATE {alias}\r\nSET {setClause}\r\n{source}";
        }

        /// <summary>
        ///     <para>
        ///         Translates a DELETE expression.
        ///     </para>
        /// </summary>
        protected virtual string TranslateDelete(SqlDeleteExpression node)
        {
            var alias = this.GetAlias(node.DataSourceAlias);
            var source = this.TranslateExpression(node.Source);
            source = this.RemoveOuterParentheses(source);
            return $"DELETE {alias}\r\n{source}";
        }

        /// <summary>
        ///     <para>
        ///         Translates an INSERT INTO expression.
        ///     </para>
        /// </summary>
        protected virtual string TranslateInsertInto(SqlInsertIntoExpression node)
        {
            var selectColumns = node.SelectQuery.SelectColumnCollection.SelectColumns.ToList();
            var propertyWithDbColumnMap = (
                from tableCol in node.TableColumns
                join selectCol in selectColumns on tableCol.ModelPropertyName equals selectCol.Alias
                select new { selectCol.Alias, tableCol.DatabaseColumnName }
            ).ToDictionary(x => x.Alias, x => x.DatabaseColumnName);

            var columns = string.Join(", ", selectColumns.Select(c => propertyWithDbColumnMap[c.Alias]));
            var selectQuery = this.TranslateExpression(node.SelectQuery);

            return $"INSERT INTO {node.SqlTable.TableName}({columns})\r\n{selectQuery}";
        }

        #endregion

        #region Miscellaneous Translation

        /// <summary>
        ///     <para>
        ///         Translates a comment expression.
        ///     </para>
        /// </summary>
        protected virtual string TranslateComment(SqlCommentExpression node)
        {
            return $"/*{node.Comment}*/";
        }

        /// <summary>
        ///     <para>
        ///         Translates a SQL fragment expression.
        ///     </para>
        /// </summary>
        protected virtual string TranslateFragment(SqlFragmentExpression node)
        {
            return node.Fragment;
        }

        /// <summary>
        ///     <para>
        ///         Translates a queryable expression.
        ///     </para>
        /// </summary>
        protected virtual string TranslateQueryable(SqlQueryableExpression node)
        {
            var query = this.TranslateExpression(node.Query);
            return $"Queryable: {{\r\n{query}\r\n}}";
        }

        /// <summary>
        ///     <para>
        ///         Removes outer parentheses from a SQL string if present.
        ///     </para>
        /// </summary>
        protected virtual string RemoveOuterParentheses(string sql)
        {
            if (string.IsNullOrEmpty(sql))
                return sql;

            sql = sql.Trim();
            if (sql.StartsWith("(") && sql.EndsWith(")"))
            {
                return sql.Substring(1, sql.Length - 2).Trim();
            }
            return sql;
        }

        #endregion
    }
}
