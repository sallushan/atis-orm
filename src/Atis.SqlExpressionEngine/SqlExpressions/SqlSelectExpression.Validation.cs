using Atis.SqlExpressionEngine.Internal;
using Atis.SqlExpressionEngine.Visitors;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Atis.SqlExpressionEngine.SqlExpressions
{
    // Self-checks of the query state. See docs/SqlSelectExpressionRedesign.md, section 7.
    // Nothing here changes how a query is built. Checking runs only when
    // SqlSelectExpressionDiagnostics.Mode is not Off.
    public partial class SqlSelectExpression
    {
        /// <summary>
        ///     Aliases of data sources that this query used to have and does not have any more because
        ///     the query was wrapped (or re-initialized). An expression of THIS query that still points at one
        ///     of them was built before the wrap and was not re-pointed.
        /// </summary>
        private readonly HashSet<Guid> retiredAliases = new HashSet<Guid>();

        // A public operation can call other public operations. Only the outermost one counts.
        private int operationDepth;
        private int wrapsInOperation;

        private void EnterOperation()
        {
            this.operationDepth++;
        }

        private void LeaveOperation(string operationName)
        {
            if (this.operationDepth > 0)
                this.operationDepth--;
            if (this.operationDepth != 0)
                return;

            var wraps = this.wrapsInOperation;
            this.wrapsInOperation = 0;

            if (SqlSelectExpressionDiagnostics.Mode == SelectValidationMode.Off)
                return;

            if (wraps > 1)
                SqlSelectExpressionDiagnostics.Report("MultiWrap", $"{operationName}: the query was wrapped {wraps} times in one operation.");

            this.AssertValid(operationName);
        }

        /// <summary>
        ///     For the small operations that change the state directly: check only when no bigger operation is running.
        /// </summary>
        private void AssertValidIfOutermost(string operationName)
        {
            if (this.operationDepth == 0 && SqlSelectExpressionDiagnostics.Mode != SelectValidationMode.Off)
                this.AssertValid(operationName);
        }

        /// <summary>
        ///     Every wrap this query did, oldest first. Used to bring an expression that was built before a wrap up to date.
        /// </summary>
        private readonly List<WrapResult> wrapHistory = new List<WrapResult>();

        /// <summary>
        ///     An operation can be given an expression that was built before this query was wrapped, for example a
        ///     predicate converted before a later part of the same lambda caused the wrap. It still points at an alias
        ///     the query retired. This re-points it by replaying the wraps the query did, oldest first, and returns
        ///     the up-to-date expression. If an alias is still retired after that, the expression cannot be fixed and
        ///     this throws, because using it would produce SQL that refers to a data source that no longer exists.
        ///     It runs at the start of every operation that takes an expression, before that operation's own wrap.
        ///     It is always on: it only does work after the query has been wrapped once, and one walk per expression.
        /// </summary>
        private SqlExpression RepointStale(string operationName, string argumentName, SqlExpression expression)
        {
            if (expression is null || this.retiredAliases.Count == 0)
                return expression;

            List<Guid> stale;
            try
            {
                stale = FreeAliasFinder.Find(expression, this.retiredAliases).ToList();
            }
            catch (Exception ex)
            {
                // This is a safety net, not a gate: if the walk cannot handle some node type, let the operation run.
                if (SqlSelectExpressionDiagnostics.Mode != SelectValidationMode.Off)
                    SqlSelectExpressionDiagnostics.Report("CheckerError", $"{operationName}: the {argumentName} expression could not be checked for stale aliases ({ex.GetType().Name}: {ex.Message}).");
                return expression;
            }

            if (stale.Count == 0)
                return expression;

            var staleAliasesBefore = string.Join(", ", stale.Select(x => DebugAliasGenerator.GetAlias(x)));
            foreach (var wrapResult in this.wrapHistory)
            {
                expression = wrapResult.Rewrite(expression);
                stale = FreeAliasFinder.Find(expression, this.retiredAliases).ToList();
                if (stale.Count == 0)
                {
                    if (SqlSelectExpressionDiagnostics.Mode != SelectValidationMode.Off)
                        SqlSelectExpressionDiagnostics.Report("StaleRepointed", $"{operationName}: the {argumentName} expression pointed at retired alias(es) {staleAliasesBefore} and was re-pointed to the wrapped query.");
                    return expression;
                }
            }

            var aliases = string.Join(", ", stale.Select(x => DebugAliasGenerator.GetAlias(x)));
            throw new InvalidOperationException(
                $"{operationName}: the {argumentName} expression points at data source alias(es) {aliases} that this query " +
                $"retired when it was wrapped. The expression was built before the wrap and could not be re-pointed to the new outer query. " +
                $"Call {nameof(WrapIfRequired)} first, build the expression after it, or use {nameof(WrapResult)}.{nameof(WrapResult.Rewrite)} " +
                $"on expressions you already hold.");
        }

        private void RetireDataSources()
        {
            foreach (var dataSource in this.dataSources)
                this.retiredAliases.Add(dataSource.Alias);
        }

        /// <summary>
        ///     Checks the rules of the query state. Broken rules are logged; in Throw mode they are also thrown.
        /// </summary>
        internal void AssertValid(string operationName)
        {
            if (SqlSelectExpressionDiagnostics.Mode == SelectValidationMode.Off)
                return;

            var violations = this.FindViolations();
            if (violations.Count == 0)
                return;

            foreach (var violation in violations)
                SqlSelectExpressionDiagnostics.Report(violation.Rule, $"after {operationName}: {violation.Message}");

            if (SqlSelectExpressionDiagnostics.Mode == SelectValidationMode.Throw)
                throw new InvalidOperationException(
                    $"{nameof(SqlSelectExpression)} is in an invalid state after {operationName}: " +
                    string.Join("; ", violations.Select(x => $"[{x.Rule}] {x.Message}")));
        }

        private List<(string Rule, string Message)> FindViolations()
        {
            var violations = new List<(string Rule, string Message)>();

            // R3: the first data source is the FROM source, every other one is a join, aliases are unique.
            if (this.dataSources.Count > 0)
            {
                if (this.dataSources[0] is JoinDataSource)
                    violations.Add(("R3-DataSourceOrder", "The first data source is a join."));
                for (var i = 1; i < this.dataSources.Count; i++)
                {
                    if (!(this.dataSources[i] is JoinDataSource))
                        violations.Add(("R3-DataSourceOrder", $"Data source #{i} is not a join, only the first one can be the FROM source."));
                }
                var duplicates = this.dataSources.GroupBy(x => x.Alias).Where(g => g.Count() > 1).Select(g => g.Key);
                foreach (var duplicate in duplicates)
                    violations.Add(("R3-DuplicateAlias", $"Alias {DebugAliasGenerator.GetAlias(duplicate)} is used by more than one data source."));
            }

            // R2: once the select list exists, the query shape it was built from exists too.
            if (this.selectList.Count > 0 && this.QueryShape is null)
                violations.Add(("R2-ShapeMissing", "The select list is defined but the query shape is null."));

            // R1: nothing of this query may point at an alias this query has retired.
            // The FROM source is skipped on purpose: after a wrap it is the old query, and it legitimately
            // uses the retired aliases inside.
            if (this.retiredAliases.Count > 0)
            {
                this.CheckNoRetiredAlias(violations, "query shape", this.QueryShape);
                for (var i = 0; i < this.selectList.Count; i++)
                    this.CheckNoRetiredAlias(violations, $"select column '{this.selectList[i].Alias}'", this.selectList[i].ColumnExpression);
                foreach (var where in this.whereClause)
                    this.CheckNoRetiredAlias(violations, "where clause", where.Predicate);
                this.CheckNoRetiredAlias(violations, "group by clause", this.GroupByClause);
                foreach (var having in this.havingClause)
                    this.CheckNoRetiredAlias(violations, "having clause", having.Predicate);
                foreach (var orderBy in this.orderByClause)
                    this.CheckNoRetiredAlias(violations, "order by clause", orderBy.ColumnExpression);
                for (var i = 1; i < this.dataSources.Count; i++)
                {
                    this.CheckNoRetiredAlias(violations, $"join #{i} source", this.dataSources[i].QuerySource);
                    if (this.dataSources[i] is JoinDataSource join)
                        this.CheckNoRetiredAlias(violations, $"join #{i} condition", join.JoinCondition);
                }
            }

            // R4: a join condition may not use a source that is added after it.
            for (var i = 1; i < this.dataSources.Count; i++)
            {
                if (!(this.dataSources[i] is JoinDataSource join) || join.JoinCondition is null)
                    continue;
                var laterAliases = new HashSet<Guid>(this.dataSources.Skip(i + 1).Select(x => x.Alias));
                if (laterAliases.Count == 0)
                    continue;
                this.CheckNoneOf(violations, "R4-ForwardJoinReference", $"join #{i} condition", join.JoinCondition, laterAliases, "uses a data source that comes after it");
            }

            return violations;
        }

        /// <summary>
        ///     Finds the aliases from a given set that an expression uses without defining them itself.
        ///     A sub-query can carry its own copy of a query body, for example when <c>g.First()</c> on a group
        ///     brings the grouped source into a sub-query. That body defines its own aliases, so using them
        ///     inside it is fine even though this query has retired the same alias.
        /// </summary>
        private sealed class FreeAliasFinder : SqlExpressionVisitor
        {
            private readonly HashSet<Guid> watched;
            private readonly Stack<HashSet<Guid>> definedAliases = new Stack<HashSet<Guid>>();
            private readonly HashSet<Guid> found = new HashSet<Guid>();

            private FreeAliasFinder(HashSet<Guid> watched)
            {
                this.watched = watched;
            }

            public static IEnumerable<Guid> Find(SqlExpression expression, HashSet<Guid> watched)
            {
                var finder = new FreeAliasFinder(watched);
                finder.Visit(expression);
                return finder.found;
            }

            protected internal override SqlExpression VisitSqlDerivedTable(SqlDerivedTableExpression node)
            {
                this.definedAliases.Push(new HashSet<Guid>(node.AllDataSources.Select(x => x.Alias)));
                try
                {
                    return base.VisitSqlDerivedTable(node);
                }
                finally
                {
                    this.definedAliases.Pop();
                }
            }

            protected internal override SqlExpression VisitSqlDataSourceColumn(SqlDataSourceColumnExpression node)
            {
                var alias = node.DataSourceAlias;
                if (this.watched.Contains(alias) && !this.definedAliases.Any(x => x.Contains(alias)))
                    this.found.Add(alias);
                return base.VisitSqlDataSourceColumn(node);
            }
        }

        private void CheckNoRetiredAlias(List<(string Rule, string Message)> violations, string where, SqlExpression expression)
        {
            this.CheckNoneOf(violations, "R1-RetiredAlias", where, expression, this.retiredAliases, "points at an alias this query retired when it was wrapped");
        }

        private void CheckNoneOf(List<(string Rule, string Message)> violations, string rule, string where, SqlExpression expression, HashSet<Guid> forbiddenAliases, string explanation)
        {
            if (expression is null)
                return;
            try
            {
                foreach (var alias in FreeAliasFinder.Find(expression, forbiddenAliases))
                    violations.Add((rule, $"{where} {explanation}: {DebugAliasGenerator.GetAlias(alias)}."));
            }
            catch (Exception ex)
            {
                // The checker must not hide the real behavior. Say that it could not check.
                violations.Add(("CheckerError", $"{where}: could not be checked ({ex.GetType().Name}: {ex.Message})."));
            }
        }
    }
}
