using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq;

using Atis.Orm.Abstractions;
using Atis.Orm.Querying;
namespace Atis.Orm.Translation
{
    /// <summary>
    ///     <para>
    ///         Abstract base class for translating SQL expressions to SQL strings.
    ///     </para>
    ///     <para>
    ///         This class provides the core translation logic and can be extended
    ///         to support database-specific SQL syntax.
    ///     </para>
    ///     <para>
    ///         Translation is performed by appending fragments into a shared
    ///         <see cref="SqlFragmentWriter"/> (see the <c>Translate*</c> methods, which return
    ///         <c>void</c>). Parameter placeholders are recorded as
    ///         <see cref="SqlParameterFragment"/> markers via <see cref="EmitParameter"/>; this
    ///         marker bookkeeping is owned by the base and cannot be altered by derived translators.
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
        private readonly SqlFragmentWriter writer = new SqlFragmentWriter();
        private int parameterCounter;
        private Dictionary<Guid, string> aliasCache;
        private int depth;
        // When set, the next derived-table / union query emits without its outer parentheses.
        // Replaces the old string-based RemoveOuterParentheses post-processing (union/update/delete).
        private bool suppressDerivedTableParens;

        /// <summary>
        ///     <para>
        ///         Translates a SQL expression tree to a SQL string with parameters.
        ///     </para>
        /// </summary>
        /// <param name="sqlExpression">The SQL expression to translate.</param>
        /// <returns>A <see cref="SqlTranslationResult"/> containing the SQL string and parameters.</returns>
        public SqlTranslationResult Translate(SqlExpression sqlExpression)
        {
            this.Parameters.Clear();
            this.parameterCounter = 0;
            this.aliasCache = new Dictionary<Guid, string>();
            this.depth = 0;
            this.suppressDerivedTableParens = false;
            this.writer.Reset();

            this.TranslateExpression(sqlExpression);

            return new SqlTranslationResult(this.writer.ToSql(), this.Parameters);
        }

        #region Output helpers

        /// <summary>Appends literal SQL text to the output.</summary>
        protected void Append(string text) => this.writer.Append(text);

        /// <summary>Appends a single literal SQL character to the output.</summary>
        protected void Append(char c) => this.writer.Append(c);

        #endregion

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
            // Non-literal parameters carry the source variable's identity so their value can be rebound by
            // lookup (not by traversal position) on a cache hit. Literals keep InitialValue and need none.
            var identity = (sourceExpression as SqlParameterExpression)?.Identity;
            return new QueryParameter(name, value, isLiteral, sourceExpression, identity);
        }

        /// <summary>
        ///     <para>
        ///         Emits a parameter placeholder: generates its name, records the query parameter, and
        ///         writes the parameter marker at the current output position.
        ///     </para>
        ///     <para>
        ///         Marker recording is owned here and is intentionally non-virtual so derived translators
        ///         cannot bypass or corrupt it. Providers customize naming via <see cref="GenerateParameterName"/>
        ///         and the parameter object via <see cref="CreateQueryParameter"/>.
        ///     </para>
        /// </summary>
        /// <param name="value">The parameter value.</param>
        /// <param name="isLiteral">Whether this is a literal value.</param>
        /// <param name="source">The source SQL expression (literal or parameter node).</param>
        protected void EmitParameter(object value, bool isLiteral, SqlExpression source)
        {
            var name = this.GenerateParameterName();
            var queryParameter = this.CreateQueryParameter(name, value, isLiteral, source);
            this.Parameters.Add(queryParameter);
            this.writer.AddParameter(queryParameter);
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

        // Reads and clears the one-shot "emit next query without outer parentheses" flag.
        private bool ConsumeSuppressParens()
        {
            var value = this.suppressDerivedTableParens;
            this.suppressDerivedTableParens = false;
            return value;
        }

        #endregion

        #region Main Dispatch

        /// <summary>
        ///     <para>
        ///         Main dispatch method that routes to the appropriate translation method based on expression type.
        ///     </para>
        /// </summary>
        /// <param name="node">The SQL expression to translate.</param>
        protected virtual void TranslateExpression(SqlExpression node)
        {
            if (node == null)
                return;

            // Every recursion routes through here, so incrementing on entry gives each node its
            // query-nesting depth: the root query is reached at depth 1, every subquery at depth >= 2.
            // TranslateDerivedTable/TranslateUnionQuery use this to wrap subqueries in parentheses
            // while emitting the outermost query as a bare statement (a parenthesized top-level
            // query expression is invalid T-SQL once it carries an ORDER BY).
            this.depth++;
            try
            {
                this.DispatchExpression(node);
            }
            finally
            {
                this.depth--;
            }
        }

        /// <summary>
        ///     <para>
        ///         Routes to the appropriate translation method based on expression type.
        ///     </para>
        /// </summary>
        /// <param name="node">The SQL expression to translate.</param>
        protected virtual void DispatchExpression(SqlExpression node)
        {
            if (node is SqlLiteralExpression literal)
                this.TranslateLiteral(literal);
            else if (node is SqlParameterExpression parameter)
                this.TranslateParameter(parameter);
            else if (node is SqlBinaryExpression binary)
                this.TranslateBinary(binary);
            else if (node is SqlDataSourceColumnExpression column)
                this.TranslateDataSourceColumn(column);
            else if (node is SqlTableExpression table)
                this.TranslateTable(table);
            else if (node is SqlDerivedTableExpression derivedTable)
                this.TranslateDerivedTable(derivedTable);
            else if (node is SqlAliasedFromSourceExpression aliasedFromSource)
                this.TranslateAliasedFromSource(aliasedFromSource);
            else if (node is SqlAliasedJoinSourceExpression aliasedJoinSource)
                this.TranslateAliasedJoinSource(aliasedJoinSource);
            else if (node is SqlAliasedCteSourceExpression aliasedCteSource)
                this.TranslateAliasedCteSource(aliasedCteSource);
            else if (node is SqlCteReferenceExpression cteReference)
                this.TranslateCteReference(cteReference);
            else if (node is SqlAliasExpression alias)
                this.TranslateAlias(alias);
            else if (node is SqlFunctionCallExpression functionCall)
                this.TranslateFunctionCall(functionCall);
            else if (node is SqlExistsExpression exists)
                this.TranslateExists(exists);
            else if (node is SqlConditionalExpression conditional)
                this.TranslateConditional(conditional);
            else if (node is SqlNotExpression not)
                this.TranslateNot(not);
            else if (node is SqlNegateExpression negate)
                this.TranslateNegate(negate);
            else if (node is SqlInValuesExpression inValues)
                this.TranslateInValues(inValues);
            else if (node is SqlLikeExpression like)
                this.TranslateLike(like);
            else if (node is SqlCastExpression cast)
                this.TranslateCast(cast);
            else if (node is SqlDateAddExpression dateAdd)
                this.TranslateDateAdd(dateAdd);
            else if (node is SqlDatePartExpression datePart)
                this.TranslateDatePart(datePart);
            else if (node is SqlDateSubtractExpression dateSubtract)
                this.TranslateDateSubtract(dateSubtract);
            else if (node is SqlStringFunctionExpression stringFunction)
                this.TranslateStringFunction(stringFunction);
            else if (node is SqlCollectionExpression collection)
                this.TranslateCollection(collection);
            else if (node is SqlUnionQueryExpression unionQuery)
                this.TranslateUnionQuery(unionQuery);
            else if (node is SqlStandaloneSelectExpression standaloneSelect)
                this.TranslateStandaloneSelect(standaloneSelect);
            else if (node is SqlUpdateExpression update)
                this.TranslateUpdate(update);
            else if (node is SqlDeleteExpression delete)
                this.TranslateDelete(delete);
            else if (node is SqlInsertIntoExpression insertInto)
                this.TranslateInsertInto(insertInto);
            else if (node is SqlNewGuidExpression newGuid)
                this.TranslateNewGuid(newGuid);
            else if (node is SqlCommentExpression comment)
                this.TranslateComment(comment);
            else if (node is SqlFragmentExpression fragment)
                this.TranslateFragment(fragment);
            else if (node is SqlQueryableExpression queryable)
                this.TranslateQueryable(queryable);
            else
                this.TranslateUnknown(node);
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
        protected virtual void TranslateUnknown(SqlExpression node)
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
        protected virtual void TranslateLiteral(SqlLiteralExpression node)
        {
            this.EmitParameter(node.LiteralValue, isLiteral: true, node);
        }

        /// <summary>
        ///     <para>
        ///         Translates a parameter expression to a parameter placeholder.
        ///     </para>
        /// </summary>
        protected virtual void TranslateParameter(SqlParameterExpression node)
        {
            this.EmitParameter(node.Value, isLiteral: false, node);
        }

        #endregion

        #region Binary Expression Translation

        /// <summary>
        ///     <para>
        ///         Translates a binary expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateBinary(SqlBinaryExpression node)
        {
            if (node.NodeType == SqlExpressionType.Coalesce)
            {
                this.TranslateCoalesce(node.Left, node.Right);
                return;
            }

            // Handle null comparisons
            if (this.IsNullExpression(node.Right))
            {
                if (node.NodeType == SqlExpressionType.Equal)
                {
                    this.writer.Append("(");
                    this.TranslateExpression(node.Left);
                    this.writer.Append(" IS NULL)");
                    return;
                }
                else if (node.NodeType == SqlExpressionType.NotEqual)
                {
                    this.writer.Append("(");
                    this.TranslateExpression(node.Left);
                    this.writer.Append(" IS NOT NULL)");
                    return;
                }
            }

            var op = this.GetBinaryOperator(node.NodeType);
            this.writer.Append("(");
            if (node.NodeType == SqlExpressionType.AndAlso || node.NodeType == SqlExpressionType.OrElse)
            {
                this.TranslateAsLogicalExpression(node.Left);
                this.writer.Append($" {op} ");
                this.TranslateAsLogicalExpression(node.Right);
            }
            else
            {
                this.TranslateAsNonLogicalExpression(node.Left);
                this.writer.Append($" {op} ");
                this.TranslateAsNonLogicalExpression(node.Right);
            }
            this.writer.Append(")");
        }

        /// <summary>
        ///     <para>
        ///         Translates a COALESCE expression.
        ///     </para>
        ///     <para>
        ///         Override this method for database-specific syntax (e.g., ISNULL for SQL Server).
        ///     </para>
        /// </summary>
        protected virtual void TranslateCoalesce(SqlExpression left, SqlExpression right)
        {
            this.writer.Append("COALESCE(");
            this.TranslateAsNonLogicalExpression(left);
            this.writer.Append(", ");
            this.TranslateAsNonLogicalExpression(right);
            this.writer.Append(")");
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
        protected virtual void TranslateAsLogicalExpression(SqlExpression node)
        {
            if (!this.IsLogicalExpression(node))
            {
                this.writer.Append("(");
                this.TranslateExpression(node);
                this.writer.Append(" = 1)");
            }
            else
            {
                this.TranslateExpression(node);
            }
        }

        /// <summary>
        ///     <para>
        ///         Ensures an expression is translated as a non-logical (value) expression.
        ///     </para>
        ///     <para>
        ///         If the expression is logical, wraps it in a CASE expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateAsNonLogicalExpression(SqlExpression node)
        {
            if (this.IsLogicalExpression(node))
            {
                this.writer.Append("CASE WHEN ");
                this.TranslateExpression(node);
                this.writer.Append(" THEN 1 ELSE 0 END");
            }
            else
            {
                this.TranslateExpression(node);
            }
        }

        #endregion

        #region Column and Table Translation

        /// <summary>
        ///     <para>
        ///         Translates a data source column reference.
        ///     </para>
        /// </summary>
        protected virtual void TranslateDataSourceColumn(SqlDataSourceColumnExpression node)
        {
            this.writer.Append(this.GetAlias(node.DataSourceAlias));
            this.writer.Append(".");
            this.writer.Append(node.ColumnName);
        }

        /// <summary>
        ///     <para>
        ///         Translates a table reference.
        ///     </para>
        /// </summary>
        protected virtual void TranslateTable(SqlTableExpression node)
        {
            var t = node.SqlTable;
            var tableParts = new[] { t.Server, t.Database, t.Schema, t.TableName };
            this.writer.Append(string.Join(".", tableParts.Where(x => !string.IsNullOrEmpty(x))));
        }

        /// <summary>
        ///     <para>
        ///         Translates an alias expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateAlias(SqlAliasExpression node)
        {
            this.writer.Append(node.ColumnAlias);
        }

        #endregion

        #region Derived Table Translation

        /// <summary>
        ///     <para>
        ///         Translates a derived table (subquery).
        ///     </para>
        /// </summary>
        protected virtual void TranslateDerivedTable(SqlDerivedTableExpression node)
        {
            // Top-level query (depth 1) is a complete statement; only nest subqueries in parentheses.
            // A caller (union/update/delete) can suppress wrapping to emit a bare query.
            var wrap = this.depth > 1 && !this.ConsumeSuppressParens();
            if (wrap)
                this.writer.Append("(\r\n");

            // Clauses are joined by "\r\n". A clause occupies a "part slot" only when present; the FROM
            // clause always occupies a slot (matching the original unconditional add, even when empty).
            var first = true;
            void Separator()
            {
                if (!first)
                    this.writer.Append("\r\n");
                first = false;
            }

            if (node.CteDataSources != null && node.CteDataSources.Count > 0)
            {
                Separator();
                this.TranslateCteDataSources(node.CteDataSources);
            }

            if (node.SelectColumnCollection != null)
            {
                Separator();
                this.TranslateSelectClause(node);
            }

            // FROM clause always occupies a slot.
            Separator();
            this.TranslateFromClause(node.FromSource);

            if (node.Joins != null && node.Joins.Count > 0)
            {
                Separator();
                this.TranslateJoins(node.Joins);
            }

            if (node.WhereClause != null && node.WhereClause.FilterConditions.Count > 0)
            {
                Separator();
                this.TranslateFilterClause(node.WhereClause, "WHERE");
            }

            if (node.GroupByClause != null && node.GroupByClause.Count > 0)
            {
                Separator();
                this.TranslateGroupByClause(node.GroupByClause);
            }

            if (node.HavingClause != null && node.HavingClause.FilterConditions.Count > 0)
            {
                Separator();
                this.TranslateFilterClause(node.HavingClause, "HAVING");
            }

            if (node.OrderByClause != null && node.OrderByClause.OrderByColumns.Count > 0)
            {
                Separator();
                this.TranslateOrderByClause(node.OrderByClause);
            }

            if (node.RowOffset != null && node.RowsPerPage != null)
            {
                Separator();
                this.TranslatePaging(node.RowOffset, node.RowsPerPage);
            }

            if (wrap)
                this.writer.Append("\r\n)");
        }

        /// <summary>
        ///     <para>
        ///         Translates the SELECT clause of a derived table.
        ///     </para>
        /// </summary>
        protected virtual void TranslateSelectClause(SqlDerivedTableExpression node)
        {
            if (node.SelectColumnCollection == null)
                return;

            this.writer.Append("SELECT ");
            if (node.IsDistinct)
                this.writer.Append("DISTINCT ");
            if (node.Top > 0)
                this.writer.Append($"TOP ({node.Top}) ");
            this.TranslateSelectColumns(node.SelectColumnCollection.SelectColumns);
        }

        /// <summary>
        ///     <para>
        ///         Translates a list of select columns.
        ///     </para>
        /// </summary>
        protected virtual void TranslateSelectColumns(IReadOnlyList<SelectColumn> selectColumns)
        {
            for (var i = 0; i < selectColumns.Count; i++)
            {
                if (i > 0)
                    this.writer.Append(", ");
                var col = selectColumns[i];
                this.TranslateAsNonLogicalExpression(col.ColumnExpression);
                this.writer.Append(" AS ");
                this.writer.Append(col.Alias);
            }
        }

        /// <summary>
        ///     <para>
        ///         Translates the FROM clause.
        ///     </para>
        /// </summary>
        protected virtual void TranslateFromClause(SqlAliasedFromSourceExpression fromSource)
        {
            if (fromSource == null)
                return;
            this.writer.Append("FROM ");
            this.TranslateAliasedFromSource(fromSource);
        }

        /// <summary>
        ///     <para>
        ///         Translates an aliased FROM source.
        ///     </para>
        /// </summary>
        protected virtual void TranslateAliasedFromSource(SqlAliasedFromSourceExpression node)
        {
            this.TranslateExpression(node.QuerySource);
            this.writer.Append(" AS ");
            this.writer.Append(this.GetAlias(node.Alias));
        }

        /// <summary>
        ///     <para>
        ///         Translates JOIN clauses.
        ///     </para>
        /// </summary>
        protected virtual void TranslateJoins(IReadOnlyList<SqlAliasedJoinSourceExpression> joins)
        {
            if (joins == null || joins.Count == 0)
                return;

            for (var i = 0; i < joins.Count; i++)
            {
                if (i > 0)
                    this.writer.Append("\r\n");
                this.TranslateAliasedJoinSource(joins[i]);
            }
        }

        /// <summary>
        ///     <para>
        ///         Translates an aliased JOIN source.
        ///     </para>
        /// </summary>
        protected virtual void TranslateAliasedJoinSource(SqlAliasedJoinSourceExpression node)
        {
            this.writer.Append(this.GetJoinTypeKeyword(node.JoinType));
            this.writer.Append(" ");
            this.TranslateExpression(node.QuerySource);
            this.writer.Append(" AS ");
            this.writer.Append(this.GetAlias(node.Alias, node.JoinName));
            if (node.JoinCondition != null)
            {
                this.writer.Append(" ON ");
                this.TranslateAsLogicalExpression(node.JoinCondition);
            }
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
        protected virtual void TranslateFilterClause(SqlFilterClauseExpression filterClause, string keyword)
        {
            if (filterClause == null || filterClause.FilterConditions.Count == 0)
                return;

            this.writer.Append(keyword);
            this.writer.Append(" ");
            for (var i = 0; i < filterClause.FilterConditions.Count; i++)
            {
                var condition = filterClause.FilterConditions[i];
                if (i > 0)
                    this.writer.Append(condition.UseOrOperator ? " OR " : " AND ");
                this.TranslateAsLogicalExpression(condition.Predicate);
            }
        }

        /// <summary>
        ///     <para>
        ///         Translates the GROUP BY clause.
        ///     </para>
        /// </summary>
        protected virtual void TranslateGroupByClause(IReadOnlyList<SqlExpression> groupByClause)
        {
            if (groupByClause == null || groupByClause.Count == 0)
                return;

            this.writer.Append("GROUP BY ");
            for (var i = 0; i < groupByClause.Count; i++)
            {
                if (i > 0)
                    this.writer.Append(", ");
                this.TranslateExpression(groupByClause[i]);
            }
        }

        /// <summary>
        ///     <para>
        ///         Translates the ORDER BY clause.
        ///     </para>
        /// </summary>
        protected virtual void TranslateOrderByClause(SqlOrderByClauseExpression orderByClause)
        {
            if (orderByClause == null || orderByClause.OrderByColumns.Count == 0)
                return;

            this.writer.Append("ORDER BY ");
            for (var i = 0; i < orderByClause.OrderByColumns.Count; i++)
            {
                if (i > 0)
                    this.writer.Append(", ");
                var o = orderByClause.OrderByColumns[i];
                this.TranslateExpression(o.ColumnExpression);
                this.writer.Append(o.Direction == SortDirection.Ascending ? " ASC" : " DESC");
            }
        }

        /// <summary>
        ///     <para>
        ///         Translates paging (OFFSET/FETCH).
        ///     </para>
        ///     <para>
        ///         Override this method for database-specific paging syntax.
        ///     </para>
        /// </summary>
        protected virtual void TranslatePaging(int? rowOffset, int? rowsPerPage)
        {
            if (rowOffset == null || rowsPerPage == null)
                return;

            this.writer.Append($"OFFSET {rowOffset} ROWS FETCH NEXT {rowsPerPage} ROWS ONLY");
        }

        #endregion

        #region CTE Translation

        /// <summary>
        ///     <para>
        ///         Translates CTE (Common Table Expression) definitions.
        ///     </para>
        /// </summary>
        protected virtual void TranslateCteDataSources(IReadOnlyList<SqlAliasedCteSourceExpression> cteDataSources)
        {
            if (cteDataSources == null || cteDataSources.Count == 0)
                return;

            this.writer.Append("WITH ");
            for (var i = 0; i < cteDataSources.Count; i++)
            {
                if (i > 0)
                    this.writer.Append(", ");
                this.TranslateAliasedCteSource(cteDataSources[i]);
            }
        }

        /// <summary>
        ///     <para>
        ///         Translates an aliased CTE source.
        ///     </para>
        /// </summary>
        protected virtual void TranslateAliasedCteSource(SqlAliasedCteSourceExpression node)
        {
            this.writer.Append(this.GetAlias(node.CteAlias, "cte"));
            this.writer.Append(" AS\r\n");
            this.TranslateExpression(node.CteBody);
        }

        /// <summary>
        ///     <para>
        ///         Translates a CTE reference.
        ///     </para>
        /// </summary>
        protected virtual void TranslateCteReference(SqlCteReferenceExpression node)
        {
            this.writer.Append(this.GetAlias(node.CteAlias, "cte"));
        }

        #endregion

        #region Function Translation

        /// <summary>
        ///     <para>
        ///         Translates a function call expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateFunctionCall(SqlFunctionCallExpression node)
        {
            var arguments = node.Arguments?.ToList();
            var hasArgs = arguments != null && arguments.Count > 0;

            // Special handling for COUNT with no arguments -> COUNT(1)
            if (node.FunctionName.Equals("Count", StringComparison.OrdinalIgnoreCase) && !hasArgs)
            {
                this.writer.Append(node.FunctionName);
                this.writer.Append("(1)");
                return;
            }

            this.writer.Append(node.FunctionName);
            this.writer.Append("(");
            if (hasArgs)
            {
                for (var i = 0; i < arguments.Count; i++)
                {
                    if (i > 0)
                        this.writer.Append(", ");
                    this.TranslateAsNonLogicalExpression(arguments[i]);
                }
            }
            this.writer.Append(")");
        }

        /// <summary>
        ///     <para>
        ///         Translates a string function expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateStringFunction(SqlStringFunctionExpression node)
        {
            this.writer.Append(node.StringFunction.ToString());
            this.writer.Append("(");
            this.TranslateExpression(node.StringExpression);
            if (node.Arguments != null && node.Arguments.Count > 0)
            {
                for (var i = 0; i < node.Arguments.Count; i++)
                {
                    this.writer.Append(", ");
                    this.TranslateExpression(node.Arguments[i]);
                }
            }
            this.writer.Append(")");
        }

        /// <summary>
        ///     <para>
        ///         Translates a DATEADD expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateDateAdd(SqlDateAddExpression node)
        {
            this.writer.Append($"DATEADD({node.DatePart}, ");
            this.TranslateExpression(node.Interval);
            this.writer.Append(", ");
            this.TranslateExpression(node.DateExpression);
            this.writer.Append(")");
        }

        /// <summary>
        ///     <para>
        ///         Translates a DATEPART expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateDatePart(SqlDatePartExpression node)
        {
            this.writer.Append($"DATEPART({node.DatePart}, ");
            this.TranslateExpression(node.DateExpression);
            this.writer.Append(")");
        }

        /// <summary>
        ///     <para>
        ///         Translates a date subtraction expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateDateSubtract(SqlDateSubtractExpression node)
        {
            this.writer.Append($"DATEDIFF({node.DatePart}, ");
            this.TranslateExpression(node.StartDate);
            this.writer.Append(", ");
            this.TranslateExpression(node.EndDate);
            this.writer.Append(")");
        }

        /// <summary>
        ///     <para>
        ///         Translates a NEWGUID expression.
        ///     </para>
        ///     <para>
        ///         Override this method for database-specific syntax.
        ///     </para>
        /// </summary>
        protected virtual void TranslateNewGuid(SqlNewGuidExpression node)
        {
            this.writer.Append("NEWID()");
        }

        #endregion

        #region Logical Expression Translation

        /// <summary>
        ///     <para>
        ///         Translates an EXISTS expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateExists(SqlExistsExpression node)
        {
            this.writer.Append("EXISTS");
            this.TranslateExpression(node.SubQuery);
        }

        /// <summary>
        ///     <para>
        ///         Translates a conditional (CASE WHEN) expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateConditional(SqlConditionalExpression node)
        {
            this.writer.Append("CASE WHEN ");
            this.TranslateAsLogicalExpression(node.Test);
            this.writer.Append(" THEN ");
            this.TranslateAsNonLogicalExpression(node.IfTrue);
            this.writer.Append(" ELSE ");
            this.TranslateAsNonLogicalExpression(node.IfFalse);
            this.writer.Append(" END");
        }

        /// <summary>
        ///     <para>
        ///         Translates a NOT expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateNot(SqlNotExpression node)
        {
            this.writer.Append("NOT ");
            this.TranslateAsLogicalExpression(node.Operand);
        }

        /// <summary>
        ///     <para>
        ///         Translates a negate (-) expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateNegate(SqlNegateExpression node)
        {
            this.writer.Append("-");
            this.TranslateExpression(node.Operand);
        }

        /// <summary>
        ///     <para>
        ///         Translates an IN VALUES expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateInValues(SqlInValuesExpression node)
        {
            this.TranslateExpression(node.Expression);
            this.writer.Append(" IN (");
            var firstValue = true;
            foreach (var value in node.Values)
            {
                if (!firstValue)
                    this.writer.Append(", ");
                firstValue = false;
                this.TranslateExpression(value);
            }
            this.writer.Append(")");
        }

        /// <summary>
        ///     <para>
        ///         Translates a LIKE expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateLike(SqlLikeExpression node)
        {
            this.writer.Append("(");
            this.TranslateExpression(node.Expression);
            switch (node.NodeType)
            {
                case SqlExpressionType.LikeStartsWith:
                    this.writer.Append(" LIKE ");
                    this.TranslateExpression(node.Pattern);
                    this.writer.Append(" + '%')");
                    break;
                case SqlExpressionType.LikeEndsWith:
                    this.writer.Append(" LIKE '%' + ");
                    this.TranslateExpression(node.Pattern);
                    this.writer.Append(")");
                    break;
                default: // SqlExpressionType.Like (contains)
                    this.writer.Append(" LIKE '%' + ");
                    this.TranslateExpression(node.Pattern);
                    this.writer.Append(" + '%')");
                    break;
            }
        }

        /// <summary>
        ///     <para>
        ///         Translates a CAST expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateCast(SqlCastExpression node)
        {
            this.writer.Append("CAST(");
            this.TranslateExpression(node.Expression);
            this.writer.Append(" AS ");
            this.writer.Append(this.TranslateDataType(node.SqlDataType));
            this.writer.Append(")");
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
        protected virtual void TranslateCollection(SqlCollectionExpression node)
        {
            var firstItem = true;
            foreach (var e in node.SqlExpressions)
            {
                if (!firstItem)
                    this.writer.Append(", ");
                firstItem = false;
                this.TranslateExpression(e);
            }
        }

        /// <summary>
        ///     <para>
        ///         Translates a UNION query expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateUnionQuery(SqlUnionQueryExpression node)
        {
            // Top-level union (depth 1) is a complete statement; only nest subqueries in parentheses.
            var wrap = this.depth > 1 && !this.ConsumeSuppressParens();
            if (wrap)
                this.writer.Append("(\r\n");

            var first = true;
            void Separator()
            {
                if (!first)
                    this.writer.Append("\r\n");
                first = false;
            }

            for (var i = 0; i < node.Unions.Count; i++)
            {
                var unionItem = node.Unions[i];
                if (i > 0)
                {
                    Separator();
                    this.writer.Append(unionItem.UnionType == SqlUnionType.UnionAll ? "UNION ALL" : "UNION");
                }
                Separator();
                // Each union member is emitted without its outer parentheses.
                this.suppressDerivedTableParens = true;
                this.TranslateExpression(unionItem.DerivedTable);
            }

            if (wrap)
                this.writer.Append("\r\n)");
        }

        /// <summary>
        ///     <para>
        ///         Translates a standalone SELECT expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateStandaloneSelect(SqlStandaloneSelectExpression node)
        {
            this.writer.Append("(SELECT ");
            this.TranslateSelectColumns(node.SelectList);
            this.writer.Append(")");
        }

        #endregion

        #region DML Translation (UPDATE, DELETE, INSERT)

        /// <summary>
        ///     <para>
        ///         Translates an UPDATE expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateUpdate(SqlUpdateExpression node)
        {
            var alias = this.GetAlias(node.DataSource);
            this.writer.Append("UPDATE ");
            this.writer.Append(alias);
            this.writer.Append("\r\nSET ");

            var count = Math.Min(node.Columns.Count, node.Values.Count);
            for (var i = 0; i < count; i++)
            {
                if (i > 0)
                    this.writer.Append(",\r\n\t");
                this.writer.Append(node.Columns[i]);
                this.writer.Append(" = ");
                this.TranslateExpression(node.Values[i]);
            }

            this.writer.Append("\r\n");
            // Source query is emitted bare (no outer parentheses).
            this.suppressDerivedTableParens = true;
            this.TranslateExpression(node.Source);
        }

        /// <summary>
        ///     <para>
        ///         Translates a DELETE expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateDelete(SqlDeleteExpression node)
        {
            this.writer.Append("DELETE ");
            this.writer.Append(this.GetAlias(node.DataSourceAlias));
            this.writer.Append("\r\n");
            // Source query is emitted bare (no outer parentheses).
            this.suppressDerivedTableParens = true;
            this.TranslateExpression(node.Source);
        }

        /// <summary>
        ///     <para>
        ///         Translates an INSERT INTO expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateInsertInto(SqlInsertIntoExpression node)
        {
            var selectColumns = node.SelectQuery.SelectColumnCollection.SelectColumns.ToList();
            var propertyWithDbColumnMap = (
                from tableCol in node.TableColumns
                join selectCol in selectColumns on tableCol.ModelPropertyName equals selectCol.Alias
                select new { selectCol.Alias, tableCol.DatabaseColumnName }
            ).ToDictionary(x => x.Alias, x => x.DatabaseColumnName);

            var columns = string.Join(", ", selectColumns.Select(c => propertyWithDbColumnMap[c.Alias]));
            this.writer.Append("INSERT INTO ");
            this.writer.Append(node.SqlTable.TableName);
            this.writer.Append("(");
            this.writer.Append(columns);
            this.writer.Append(")\r\n");
            this.TranslateExpression(node.SelectQuery);
        }

        #endregion

        #region Miscellaneous Translation

        /// <summary>
        ///     <para>
        ///         Translates a comment expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateComment(SqlCommentExpression node)
        {
            this.writer.Append("/*");
            this.writer.Append(node.Comment);
            this.writer.Append("*/");
        }

        /// <summary>
        ///     <para>
        ///         Translates a SQL fragment expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateFragment(SqlFragmentExpression node)
        {
            this.writer.Append(node.Fragment);
        }

        /// <summary>
        ///     <para>
        ///         Translates a queryable expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateQueryable(SqlQueryableExpression node)
        {
            this.writer.Append("Queryable: {\r\n");
            this.TranslateExpression(node.Query);
            this.writer.Append("\r\n}");
        }

        #endregion
    }
}
