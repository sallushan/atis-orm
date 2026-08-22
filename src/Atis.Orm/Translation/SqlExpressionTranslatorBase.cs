using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
    ///         Translation builds an ordered list of <see cref="ICommandFragment"/> (see the
    ///         <c>Translate*</c> methods, which return <c>void</c>). Literal text goes through
    ///         <see cref="Append(string)"/>; parameter placeholders are recorded as markers via
    ///         <see cref="EmitParameter"/>, and that marker bookkeeping is owned by the base and cannot
    ///         be altered by derived translators.
    ///     </para>
    ///     <para>
    ///         A fragment whose rendering depends on the values bound at execution time is built by
    ///         capturing its children with <see cref="BeginCapture"/> / <see cref="EndCapture"/> (or the
    ///         <see cref="TranslateFragments"/> shorthand) and handing them to the fragment's own
    ///         constructor, then <see cref="AppendFragment"/>. The capture API knows nothing about
    ///         individual fragment types, so a provider adds one without touching this class.
    ///     </para>
    ///     <para>
    ///         <b>Not thread-safe, and not shareable.</b> <see cref="Translate"/> accumulates state on
    ///         the instance — the parameter list, the fragment buffers, the alias cache, the nesting
    ///         depth — and resets it at the start of each call. Two translations running at once on one
    ///         instance corrupt each other. It is therefore registered <c>Scoped</c>, matching its only
    ///         consumer (<c>IQueryTranslator</c>) and the rest of that chain; registering it as a
    ///         singleton reintroduces the hazard.
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

        /// <summary>
        ///     One level of output being collected: the fragments completed so far and the literal text
        ///     accumulated since the last one. A capture pushes a frame, so a nested capture never disturbs
        ///     the text its caller had half-written.
        /// </summary>
        private sealed class CaptureFrame
        {
            public readonly List<ICommandFragment> Fragments = new List<ICommandFragment>();
            public readonly StringBuilder PendingText = new StringBuilder();
        }

        // Never empty between Translate() calls: the bottom frame is the statement itself.
        private readonly Stack<CaptureFrame> captureFrames = new Stack<CaptureFrame>();
        private Dictionary<Guid, string> aliasCache;
        private int depth;
        // When set, the next derived-table / union query emits without its outer parentheses.
        // Replaces the old string-based RemoveOuterParentheses post-processing (union/update/delete).
        private bool suppressDerivedTableParens;
        // Depth of optional terms currently being translated - see TranslateOptionalPredicate, which rejects
        // anything past the first. Note this is NOT a limit on capture nesting: a null switch inside an
        // optional term's predicate is the ordinary shape and must keep working.
        private int openOptionalPredicates;
        // Guards the public entry point against re-entry - see Translate.
        private bool isTranslating;

        /// <summary>
        ///     <para>
        ///         Translates a SQL expression tree to a SQL string with parameters.
        ///     </para>
        ///     <para>
        ///         The entry point for a whole statement, and <b>only</b> that: it discards all per-statement
        ///         state before it starts. To translate a child node from inside a <c>Translate*</c> method,
        ///         call <see cref="TranslateExpression"/> - or <see cref="TranslateFragments"/> to capture it -
        ///         never this method. Re-entering it is refused rather than allowed to half-work.
        ///     </para>
        /// </summary>
        /// <param name="sqlExpression">The SQL expression to translate.</param>
        /// <returns>A <see cref="SqlTranslationResult"/> containing the SQL string and parameters.</returns>
        public SqlTranslationResult Translate(SqlExpression sqlExpression)
        {
            // Re-entry would clear the parameter list, the alias cache and the fragment buffers of the
            // translation already in progress. The damage is silent - aliases renumber, parameters vanish from
            // the plan, and the statement still renders - so it is refused outright, with a message naming the
            // method that was actually meant.
            if (this.isTranslating)
                throw new InvalidOperationException(
                    $"{nameof(Translate)} was called while a translation was already in progress. It is the " +
                    $"entry point for a complete statement and resets all per-statement state, so calling it " +
                    $"from inside a Translate* method would discard the translation in progress. Use " +
                    $"{nameof(TranslateExpression)} to translate a child node, or {nameof(TranslateFragments)} " +
                    $"to capture one into its own fragment list.");

            this.isTranslating = true;
            try
            {
                return this.TranslateCore(sqlExpression);
            }
            finally
            {
                this.isTranslating = false;
            }
        }

        private SqlTranslationResult TranslateCore(SqlExpression sqlExpression)
        {
            this.Parameters.Clear();
            this.aliasCache = new Dictionary<Guid, string>();
            this.depth = 0;
            this.suppressDerivedTableParens = false;
            this.openOptionalPredicates = 0;
            // A fresh frame stack, so an exception mid-translation cannot leave a half-open capture behind
            // and corrupt the next call. The bottom frame collects the statement itself.
            this.captureFrames.Clear();
            this.captureFrames.Push(new CaptureFrame());

            this.TranslateExpression(sqlExpression);

            if (this.captureFrames.Count != 1)
                throw new InvalidOperationException(
                    $"{this.captureFrames.Count - 1} capture(s) were opened and never closed. Every " +
                    $"{nameof(BeginCapture)} needs a matching {nameof(EndCapture)}.");

            // The frame is handed over whole and replaced on the next call, so the returned list is never
            // touched by a later translation.
            var fragments = this.CloseFrame();

            // Only the top level is inspected: a composite fragment reports its children's requirement
            // itself (see ICommandFragment.RequirePerExecutionRendering).
            var requirePerExecutionRendering = fragments.Any(x => x.RequirePerExecutionRendering);

            // Parameters gets no such treatment: it is one list, created with this translator and cleared at
            // the top of this method. Handing out the live reference means a compiled query keeps pointing at
            // it for the life of its cache entry, and the next translation of anything rewrites its parameter
            // plan underneath it — so it is copied. This translator is registered as a singleton, which makes
            // the aliasing process-wide.
            return new SqlTranslationResult(this.Parameters.ToArray(), fragments, requirePerExecutionRendering);
        }

        #region Output helpers

        /// <summary>
        ///     Appends literal SQL text to the output. Consecutive appends coalesce into a single
        ///     <see cref="TextCommandFragment"/>, which is sealed as soon as a fragment is added beside it.
        /// </summary>
        protected void Append(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;
            this.captureFrames.Peek().PendingText.Append(text);
        }

        /// <summary>Appends a single literal SQL character to the output.</summary>
        protected void Append(char c) => this.captureFrames.Peek().PendingText.Append(c);

        /// <summary>
        ///     <para>
        ///         Seals the literal text written so far and records <paramref name="fragment"/> at this exact
        ///         point in the output.
        ///     </para>
        ///     <para>
        ///         Any <see cref="ICommandFragment"/> is accepted, including a provider's own type - this
        ///         method is what makes the translator a factory for fragments rather than a fixed menu of
        ///         them.
        ///     </para>
        /// </summary>
        protected void AppendFragment(ICommandFragment fragment)
        {
            if (fragment is null)
                throw new ArgumentNullException(nameof(fragment));

            var frame = this.captureFrames.Peek();
            FlushPendingText(frame);
            frame.Fragments.Add(fragment);
        }

        /// <summary>
        ///     <para>
        ///         Starts collecting output separately, so it can be handed to a composite fragment's
        ///         constructor instead of going straight into the statement. Everything written until the
        ///         matching <see cref="EndCapture"/> is collected.
        ///     </para>
        ///     <para>
        ///         Captures nest. That is not a corner case: a null switch sits inside an optional term's
        ///         predicate for every <c>WhereBuilder.Equal</c> over a nullable value, which is the ordinary
        ///         shape of the whole feature.
        ///     </para>
        /// </summary>
        protected void BeginCapture()
        {
            this.captureFrames.Push(new CaptureFrame());
        }

        /// <summary>
        ///     Closes the capture opened by the matching <see cref="BeginCapture"/> and returns everything
        ///     written inside it, with any trailing literal text sealed into a final fragment.
        /// </summary>
        protected IReadOnlyList<ICommandFragment> EndCapture()
        {
            if (this.captureFrames.Count <= 1)
                throw new InvalidOperationException($"{nameof(EndCapture)} was called without a matching {nameof(BeginCapture)}.");

            return this.CloseFrame();
        }

        /// <summary>
        ///     Shorthand for a capture around a single child translation: the common case when building a
        ///     composite fragment's branch.
        /// </summary>
        /// <param name="node">The expression to translate into the capture.</param>
        /// <param name="visitMethod">
        ///     How to translate it - defaults to <see cref="TranslateExpression"/>. Pass
        ///     <see cref="TranslateAsLogicalExpression"/> / <see cref="TranslateAsNonLogicalExpression"/> when
        ///     the position demands one of those forms.
        /// </param>
        protected IReadOnlyList<ICommandFragment> TranslateFragments(SqlExpression node, Action<SqlExpression> visitMethod = null)
        {
            this.BeginCapture();
            (visitMethod ?? this.TranslateExpression)(node);
            return this.EndCapture();
        }

        // Pops the innermost frame and returns its fragments, sealing whatever text it ended with.
        private IReadOnlyList<ICommandFragment> CloseFrame()
        {
            var frame = this.captureFrames.Pop();
            FlushPendingText(frame);
            return frame.Fragments;
        }

        private static void FlushPendingText(CaptureFrame frame)
        {
            if (frame.PendingText.Length == 0)
                return;

            frame.Fragments.Add(new TextCommandFragment(frame.PendingText.ToString()));
            frame.PendingText.Clear();
        }

        #endregion

        #region Parameter and Alias Helpers

        /// <summary>
        ///     <para>
        ///         Creates a query parameter and adds it to the parameters collection.
        ///     </para>
        ///     <para>
        ///         Override this method to provide a custom <see cref="IQueryParameter"/> implementation.
        ///     </para>
        ///     <para>
        ///         The placeholder name is not decided here: it is assigned when the fragments are rendered,
        ///         by <see cref="IDbParameterNameGenerator"/> (see <see cref="ICommandRenderer"/>), so a
        ///         dialect can name positionally (<c>?</c>) or by index without the translator knowing.
        ///     </para>
        /// </summary>
        /// <param name="value">The parameter value.</param>
        /// <param name="isLiteral">Whether this is a literal value.</param>
        /// <param name="sourceExpression">The source SQL expression.</param>
        /// <returns>The created query parameter.</returns>
        protected virtual IQueryParameter CreateQueryParameter(object value, bool isLiteral, SqlExpression sourceExpression)
        {
            // Non-literal parameters carry the source variable's identity so their value can be rebound by
            // lookup (not by traversal position) on a cache hit. Literals keep InitialValue and need none.
            var identity = (sourceExpression as SqlParameterExpression)?.Identity;
            return new QueryParameter(value, isLiteral, sourceExpression, identity);
        }

        /// <summary>
        ///     <para>
        ///         Emits a parameter placeholder: records the query parameter and writes the parameter marker
        ///         at the current output position. The marker's name is assigned later, at render time.
        ///     </para>
        ///     <para>
        ///         Marker recording is owned here and is intentionally non-virtual so derived translators
        ///         cannot bypass or corrupt it. Providers customize the parameter object via
        ///         <see cref="CreateQueryParameter"/>.
        ///     </para>
        /// </summary>
        /// <param name="value">The parameter value.</param>
        /// <param name="isLiteral">Whether this is a literal value.</param>
        /// <param name="source">The source SQL expression (literal or parameter node).</param>
        /// <param name="isExpandable">
        ///     Whether this position accepts a comma-separated list, so a collection value expands into one
        ///     placeholder per element at execution time. Set only by <see cref="TranslateValueList"/>.
        /// </param>
        /// <param name="emptyListTemplate">
        ///     Self-contained SQL emitted in place of the value list when an expandable collection is empty
        ///     (no parameter is bound).
        /// </param>
        protected void EmitParameter(object value, bool isLiteral, SqlExpression source, bool isExpandable = false, string emptyListTemplate = null)
        {
            var queryParameter = this.CreateQueryParameter(value, isLiteral, source);
            this.Parameters.Add(queryParameter);
            this.AppendFragment(isExpandable
                ? (ICommandFragment)new ExpandableParameterCommandFragment(queryParameter, emptyListTemplate)
                : new ParameterCommandFragment(queryParameter));
        }

        /// <summary>
        ///     <para>
        ///         Translates <paramref name="node"/> at a position where the SQL accepts a comma-separated
        ///         list of values (an <c>IN</c> list, a <c>CONCAT_WS</c> value operand, ...).
        ///     </para>
        ///     <para>
        ///         A multi-value parameter is emitted as a single expandable marker, which the renderer turns
        ///         into <c>@p0_0, @p0_1, ...</c> using the collection's length at execution time. Anything
        ///         else is translated normally. Expansion is opted into here, per position, rather than being
        ///         inferred from the value: <c>byte[]</c> is a collection too, and a blob compared with
        ///         <c>=</c> must stay one parameter.
        ///     </para>
        /// </summary>
        /// <param name="node">The expression occupying the list position.</param>
        /// <param name="emptyListTemplate">
        ///     Self-contained SQL to emit when the collection turns out to be empty (no parameter is bound).
        /// </param>
        protected void TranslateValueList(SqlExpression node, string emptyListTemplate)
        {
            if (node is SqlParameterExpression parameter && parameter.MultipleValues)
                this.EmitParameter(parameter.Value, isLiteral: false, source: parameter, isExpandable: true, emptyListTemplate: emptyListTemplate);
            else
                this.TranslateExpression(node);
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
            else if (node is SqlOptionalPredicateExpression optionalPredicate)
                this.TranslateOptionalPredicate(optionalPredicate);
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
            else if (node is SqlInsertExpression insert)
                this.TranslateInsert(insert);
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
            else if (node is SqlOutputColumnExpression outputColumn)
                this.TranslateOutputColumn(outputColumn);
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

            if (node.NodeType == SqlExpressionType.Equal || node.NodeType == SqlExpressionType.NotEqual)
            {
                var isEqual = node.NodeType == SqlExpressionType.Equal;

                // A null written into the query (`x.Col == null`) is frozen by design: the tree itself says
                // null, so no execution can make it say otherwise, and folding it is safe forever.
                if (this.IsNullLiteral(node.Right))
                {
                    this.TranslateNullTest(node.Left, isEqual);
                    return;
                }
                if (this.IsNullLiteral(node.Left))
                {
                    this.TranslateNullTest(node.Right, isEqual);
                    return;
                }

                // A null that arrived in a *value* is not frozen: this compiled query is cached by expression
                // shape and re-executed with whatever the caller supplies next, so the choice between
                // `col = @p` and `col IS NULL` belongs to each execution, not to this translation. Emit both.
                if (node.Right is SqlParameterExpression rightParameter && rightParameter.CanBeNull)
                {
                    this.TranslateNullSwitch(node.Left, rightParameter, isEqual);
                    return;
                }
                if (node.Left is SqlParameterExpression leftParameter && leftParameter.CanBeNull)
                {
                    this.TranslateNullSwitch(node.Right, leftParameter, isEqual);
                    return;
                }
            }

            var op = this.GetBinaryOperator(node.NodeType);
            this.Append("(");
            if (node.NodeType == SqlExpressionType.AndAlso || node.NodeType == SqlExpressionType.OrElse)
            {
                this.TranslateAsLogicalExpression(node.Left);
                this.Append($" {op} ");
                this.TranslateAsLogicalExpression(node.Right);
            }
            else
            {
                this.TranslateAsNonLogicalExpression(node.Left);
                this.Append($" {op} ");
                this.TranslateAsNonLogicalExpression(node.Right);
            }
            this.Append(")");
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
            this.Append("COALESCE(");
            this.TranslateAsNonLogicalExpression(left);
            this.Append(", ");
            this.TranslateAsNonLogicalExpression(right);
            this.Append(")");
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
        ///         Whether <paramref name="node"/> is a null the query itself states, rather than one that
        ///         merely happens to have arrived in a value on this execution.
        ///     </para>
        ///     <para>
        ///         Only a literal qualifies. A literal is part of the expression's shape, so it is part of the
        ///         cache key and cannot change between executions of the compiled query; a parameter's value
        ///         can, and folding on it is what bakes one caller's answer into every later caller's SQL.
        ///     </para>
        /// </summary>
        protected virtual bool IsNullLiteral(SqlExpression node)
            => node is SqlLiteralExpression literal && literal.LiteralValue == null;

        /// <summary>Emits <c>(&lt;operand&gt; IS NULL)</c> / <c>(&lt;operand&gt; IS NOT NULL)</c>.</summary>
        protected virtual void TranslateNullTest(SqlExpression operand, bool isEqual)
        {
            this.Append("(");
            this.TranslateAsNonLogicalExpression(operand);
            this.Append(isEqual ? " IS NULL)" : " IS NOT NULL)");
        }

        /// <summary>
        ///     <para>
        ///         Emits a comparison against a value that could be null, as both of its spellings - the
        ///         ordinary <c>col = @p</c> and the null test <c>col IS NULL</c> - leaving the choice to the
        ///         renderer, which knows the value this execution actually binds.
        ///     </para>
        ///     <para>
        ///         C# <c>==</c> treats two nulls as equal; SQL <c>=</c> never does. So the two languages need
        ///         different SQL for the same expression depending on the value, and the value is not known
        ///         here: a compiled query is cached by expression shape and re-run with whatever comes next.
        ///     </para>
        ///     <para>
        ///         Both spellings are built here, in the translator, using the ordinary translation methods -
        ///         the renderer only picks one. The operand is translated once and its fragments are shared by
        ///         both branches: exactly one branch reaches the output, so a marker inside it is emitted -
        ///         and bound - exactly once, and translating twice would only add a second unbindable copy.
        ///     </para>
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         The deciding parameter is a second <see cref="IQueryParameter"/> over the same source node,
        ///         not the one the has-value branch emits, and is deliberately <em>not</em> added to
        ///         <see cref="Parameters"/>: it writes no placeholder of its own. It carries the same identity,
        ///         so it resolves to the same value on a cache hit - exactly as an optional term's guard does.
        ///         A parameter with no identity has no re-extractable source and falls back to its value at
        ///         translation time, which is the documented behaviour for such parameters everywhere else.
        ///     </para>
        /// </remarks>
        protected virtual void TranslateNullSwitch(SqlExpression operand, SqlParameterExpression parameter, bool isEqual)
        {
            var operandFragments = this.TranslateFragments(operand, this.TranslateAsNonLogicalExpression);
            var parameterFragments = this.TranslateFragments(parameter, this.TranslateAsNonLogicalExpression);
            var op = this.GetBinaryOperator(isEqual ? SqlExpressionType.Equal : SqlExpressionType.NotEqual);

            var whenNotNullFragments = new List<ICommandFragment>();
            whenNotNullFragments.AddRange(operandFragments);
            whenNotNullFragments.Add(new TextCommandFragment($" {op} "));
            whenNotNullFragments.AddRange(parameterFragments);

            var whenNullFragments = new List<ICommandFragment>();
            whenNullFragments.AddRange(operandFragments);
            whenNullFragments.Add(new TextCommandFragment(isEqual ? " IS NULL" : " IS NOT NULL"));

            var decider = this.CreateQueryParameter(parameter.Value, isLiteral: false, parameter);

            // The parentheses wrap the switch rather than sitting inside each branch: both spellings need
            // them, and either branch is a complete predicate on its own.
            this.Append("(");
            this.AppendFragment(new NullSwitchCommandFragment(decider, whenNullFragments, whenNotNullFragments));
            this.Append(")");
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
                   nt == SqlExpressionType.LikePattern ||
                   nt == SqlExpressionType.InValues ||
                   // Emitted as `(1 = 1 [AND ...])`, which is already a boolean group in both states.
                   nt == SqlExpressionType.OptionalPredicate ||
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
                this.Append("(");
                this.TranslateExpression(node);
                this.Append(" = 1)");
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
                this.Append("CASE WHEN ");
                this.TranslateExpression(node);
                this.Append(" THEN 1 ELSE 0 END");
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
        ///         Translates a column of the rows a data-manipulation statement returns.
        ///     </para>
        ///     <para>
        ///         The default is the SQL Server spelling — the <c>inserted</c> and <c>deleted</c>
        ///         pseudo-tables — matching the rest of this base class, whose UPDATE ... FROM is
        ///         already T-SQL shaped. A dialect that returns rows differently overrides this: a
        ///         PostgreSQL translator emitting <c>RETURNING</c> would write the bare column name,
        ///         since there is no pseudo-table to qualify it with.
        ///     </para>
        /// </summary>
        protected virtual void TranslateOutputColumn(SqlOutputColumnExpression node)
        {
            switch (node.Source)
            {
                case SqlOutputSource.Inserted:
                    this.Append("inserted");
                    break;
                case SqlOutputSource.Deleted:
                    this.Append("deleted");
                    break;
                default:
                    throw new NotSupportedException($"Output source '{node.Source}' is not supported by {this.GetType().Name}.");
            }
            this.Append(".");
            this.Append(node.ColumnName);
        }

        /// <summary>
        ///     <para>
        ///         Translates a data source column reference.
        ///     </para>
        /// </summary>
        protected virtual void TranslateDataSourceColumn(SqlDataSourceColumnExpression node)
        {
            this.Append(this.GetAlias(node.DataSourceAlias));
            this.Append(".");
            this.Append(node.ColumnName);
        }

        /// <summary>
        ///     <para>
        ///         Translates a table reference.
        ///     </para>
        /// </summary>
        protected virtual void TranslateTable(SqlTableExpression node)
        {
            this.Append(this.GetQualifiedTableName(node.SqlTable));
        }

        /// <summary>
        ///     <para>
        ///         The name a table is written by. Every site that names a table — this one, the INSERT
        ///         destination, the INSERT ... SELECT destination — goes through here, so a dialect that
        ///         quotes or brackets identifiers overrides once and all of them follow.
        ///     </para>
        /// </summary>
        protected virtual string GetQualifiedTableName(SqlTable table)
        {
            return SqlTableNaming.GetQualifiedName(table);
        }

        /// <summary>
        ///     <para>
        ///         Translates an alias expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateAlias(SqlAliasExpression node)
        {
            this.Append(node.ColumnAlias);
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
                this.Append("(\r\n");

            // Clauses are joined by "\r\n". A clause occupies a "part slot" only when present; the FROM
            // clause always occupies a slot (matching the original unconditional add, even when empty).
            var first = true;
            void Separator()
            {
                if (!first)
                    this.Append("\r\n");
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
                this.Append("\r\n)");
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

            this.Append("SELECT ");
            if (node.IsDistinct)
                this.Append("DISTINCT ");
            if (node.Top > 0)
                this.Append($"TOP ({node.Top}) ");
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
                    this.Append(", ");
                var col = selectColumns[i];
                this.TranslateAsNonLogicalExpression(col.ColumnExpression);
                this.Append(" AS ");
                this.Append(col.Alias);
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
            this.Append("FROM ");
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
            this.Append(" AS ");
            this.Append(this.GetAlias(node.Alias));
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
                    this.Append("\r\n");
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
            this.Append(this.GetJoinTypeKeyword(node.JoinType));
            this.Append(" ");
            this.TranslateExpression(node.QuerySource);
            this.Append(" AS ");
            this.Append(this.GetAlias(node.Alias, node.JoinName));
            if (node.JoinCondition != null)
            {
                this.Append(" ON ");
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

            this.Append(keyword);
            this.Append(" ");
            for (var i = 0; i < filterClause.FilterConditions.Count; i++)
            {
                var condition = filterClause.FilterConditions[i];
                if (i > 0)
                    this.Append(condition.UseOrOperator ? " OR " : " AND ");
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

            this.Append("GROUP BY ");
            for (var i = 0; i < groupByClause.Count; i++)
            {
                if (i > 0)
                    this.Append(", ");
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

            this.Append("ORDER BY ");
            for (var i = 0; i < orderByClause.OrderByColumns.Count; i++)
            {
                if (i > 0)
                    this.Append(", ");
                var o = orderByClause.OrderByColumns[i];
                this.TranslateExpression(o.ColumnExpression);
                this.Append(o.Direction == SortDirection.Ascending ? " ASC" : " DESC");
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

            this.Append($"OFFSET {rowOffset} ROWS FETCH NEXT {rowsPerPage} ROWS ONLY");
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

            this.Append("WITH ");
            for (var i = 0; i < cteDataSources.Count; i++)
            {
                if (i > 0)
                    this.Append(", ");
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
            this.Append(this.GetAlias(node.CteAlias, "cte"));
            this.Append(" AS\r\n");
            this.TranslateExpression(node.CteBody);
        }

        /// <summary>
        ///     <para>
        ///         Translates a CTE reference.
        ///     </para>
        /// </summary>
        protected virtual void TranslateCteReference(SqlCteReferenceExpression node)
        {
            this.Append(this.GetAlias(node.CteAlias, "cte"));
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
                this.Append(node.FunctionName);
                this.Append("(1)");
                return;
            }

            this.Append(node.FunctionName);
            this.Append("(");
            if (hasArgs)
            {
                for (var i = 0; i < arguments.Count; i++)
                {
                    if (i > 0)
                        this.Append(", ");
                    this.TranslateAsNonLogicalExpression(arguments[i]);
                }
            }
            this.Append(")");
        }

        /// <summary>
        ///     <para>
        ///         Translates a string function expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateStringFunction(SqlStringFunctionExpression node)
        {
            this.Append(node.StringFunction.ToString());
            this.Append("(");
            this.TranslateExpression(node.StringExpression);
            if (node.Arguments != null && node.Arguments.Count > 0)
            {
                for (var i = 0; i < node.Arguments.Count; i++)
                {
                    this.Append(", ");
                    this.TranslateExpression(node.Arguments[i]);
                }
            }
            this.Append(")");
        }

        /// <summary>
        ///     <para>
        ///         Translates a DATEADD expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateDateAdd(SqlDateAddExpression node)
        {
            this.Append($"DATEADD({node.DatePart}, ");
            this.TranslateExpression(node.Interval);
            this.Append(", ");
            this.TranslateExpression(node.DateExpression);
            this.Append(")");
        }

        /// <summary>
        ///     <para>
        ///         Translates a DATEPART expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateDatePart(SqlDatePartExpression node)
        {
            this.Append($"DATEPART({node.DatePart}, ");
            this.TranslateExpression(node.DateExpression);
            this.Append(")");
        }

        /// <summary>
        ///     <para>
        ///         Translates a date subtraction expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateDateSubtract(SqlDateSubtractExpression node)
        {
            this.Append($"DATEDIFF({node.DatePart}, ");
            this.TranslateExpression(node.StartDate);
            this.Append(", ");
            this.TranslateExpression(node.EndDate);
            this.Append(")");
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
            this.Append("NEWID()");
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
            this.Append("EXISTS");
            this.TranslateExpression(node.SubQuery);
        }

        /// <summary>
        ///     <para>
        ///         Translates a conditional (CASE WHEN) expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateConditional(SqlConditionalExpression node)
        {
            this.Append("CASE WHEN ");
            this.TranslateAsLogicalExpression(node.Test);
            this.Append(" THEN ");
            this.TranslateAsNonLogicalExpression(node.IfTrue);
            this.Append(" ELSE ");
            this.TranslateAsNonLogicalExpression(node.IfFalse);
            this.Append(" END");
        }

        /// <summary>
        ///     <para>
        ///         Translates a NOT expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateNot(SqlNotExpression node)
        {
            this.Append("NOT ");
            this.TranslateAsLogicalExpression(node.Operand);
        }

        /// <summary>
        ///     <para>
        ///         Translates an optional predicate: a term that disappears from the statement when its guard
        ///         has no value at execution time.
        ///     </para>
        ///     <para>
        ///         Emitted as a self-anchored group - <c>(1 = 1</c>, then a skippable span holding
        ///         <c> AND &lt;predicate&gt;</c>, then <c>)</c>. The anchor is load-bearing, not cosmetic: it
        ///         makes the term valid boolean SQL whether or not it survives, so an inactive term is a pure
        ///         omission and the surrounding predicate composes through the ordinary binary translation
        ///         with no separator bookkeeping and no all-terms-dropped special case.
        ///     </para>
        ///     <para>
        ///         The guard gets a query parameter but no placeholder, and is deliberately <em>not</em> added
        ///         to <see cref="Parameters"/>: it is never written to the output, so it occupies no position
        ///         in the placeholder plan. Its value still resolves through the same resolver as every other
        ///         parameter, so it rebinds by identity on a cache hit.
        ///     </para>
        /// </summary>
        protected virtual void TranslateOptionalPredicate(SqlOptionalPredicateExpression node)
        {
            object guardValue;
            bool guardIsLiteral;
            if (node.Guard is SqlParameterExpression guardParameterExpression)
            {
                guardValue = guardParameterExpression.Value;
                guardIsLiteral = false;
            }
            else if (node.Guard is SqlLiteralExpression guardLiteralExpression)
            {
                // An inline constant rather than a variable: legal, but frozen at translation, so the term is
                // permanently on or off for this compiled query. That is the caller's choice to make.
                guardValue = guardLiteralExpression.LiteralValue;
                guardIsLiteral = true;
            }
            else
            {
                throw new InvalidOperationException(
                    $"An optional predicate's guard must be a value (a captured variable or a constant), but it " +
                    $"translated to '{node.Guard.GetType().Name}'. A column cannot act as a guard: whether a term " +
                    $"appears in the statement is decided once per execution, before any row is read.");
            }

            // One optional term inside another's predicate has no meaning - it would say "apply this filter
            // only when some unrelated value was also supplied" - and no sensible query reaches it: the only
            // way to write one is to pass a WhereBuilder call in a *column* position, which type-checks solely
            // when the outer term is itself over bool. It compiles, so it gets a named error rather than odd
            // SQL. This bars only optional-inside-optional; other spans nest freely.
            if (this.openOptionalPredicates > 0)
                throw new InvalidOperationException(
                    "An optional term cannot contain another optional term. Optional terms are joined with " +
                    "AND and sit beside each other; nesting one inside another's predicate has no meaning. " +
                    "This usually means a WhereBuilder call was passed where a column was expected.");

            var guard = this.CreateQueryParameter(guardValue, guardIsLiteral, node.Guard);

            var anchorFragments = new List<ICommandFragment> { new TextCommandFragment("1 = 1") };

            // The predicate translates exactly as it would anywhere else. A comparison inside it against a
            // nullable value still emits its own null switch, which is redundant here - this term only renders
            // when the guard has a value - but costs nothing at execution and needs no special case. Before
            // the switch existed this call had to suppress null folding, or a value that was null when the
            // query compiled would bake `IS NULL` into it for every later execution.
            var predicateFragments = new List<ICommandFragment> { new TextCommandFragment(" AND ") };
            this.openOptionalPredicates++;
            try
            {
                predicateFragments.AddRange(this.TranslateFragments(node.Predicate, this.TranslateAsLogicalExpression));
            }
            finally
            {
                this.openOptionalPredicates--;
            }

            this.Append("(");
            this.AppendFragment(new OptionalPredicateCommandFragment(guard, node.GuardKind, anchorFragments, predicateFragments));
            this.Append(")");
        }

        /// <summary>
        ///     <para>
        ///         Translates a negate (-) expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateNegate(SqlNegateExpression node)
        {
            this.Append("-");
            this.TranslateExpression(node.Operand);
        }

        /// <summary>
        ///     <para>
        ///         Self-contained SQL emitted in place of an empty collection's value list inside
        ///         <c>IN (...)</c>. No parameter is bound - an empty collection has no values.
        ///     </para>
        ///     <para>
        ///         An empty subquery is used rather than <c>IN (NULL)</c> because it also negates correctly:
        ///         <c>NOT IN</c> over an empty collection matches every row. Override for dialects that
        ///         require a FROM clause (Oracle: <c>SELECT NULL FROM DUAL WHERE 1 = 0</c>).
        ///     </para>
        /// </summary>
        protected virtual string EmptyValueListTemplate => "SELECT NULL WHERE 1 = 0";

        /// <summary>
        ///     <para>
        ///         Translates an IN VALUES expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateInValues(SqlInValuesExpression node)
        {
            this.TranslateExpression(node.Expression);
            this.Append(" IN (");
            var firstValue = true;
            foreach (var value in node.Values)
            {
                if (!firstValue)
                    this.Append(", ");
                firstValue = false;
                // The values of an inline array arrive as separate expressions; a captured collection arrives
                // as one multi-value parameter, which this expands.
                this.TranslateValueList(value, this.EmptyValueListTemplate);
            }
            this.Append(")");
        }

        /// <summary>
        ///     <para>
        ///         Translates a LIKE expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateLike(SqlLikeExpression node)
        {
            this.Append("(");
            this.TranslateExpression(node.Expression);
            switch (node.NodeType)
            {
                case SqlExpressionType.LikeStartsWith:
                    this.Append(" LIKE ");
                    this.TranslateExpression(node.Pattern);
                    this.Append(" + '%')");
                    break;
                case SqlExpressionType.LikeEndsWith:
                    this.Append(" LIKE '%' + ");
                    this.TranslateExpression(node.Pattern);
                    this.Append(")");
                    break;
                case SqlExpressionType.LikePattern:
                    // The caller's pattern is used verbatim, wildcards and all - no decoration.
                    this.Append(" LIKE ");
                    this.TranslateExpression(node.Pattern);
                    this.Append(")");
                    break;
                default: // SqlExpressionType.Like (contains)
                    this.Append(" LIKE '%' + ");
                    this.TranslateExpression(node.Pattern);
                    this.Append(" + '%')");
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
            this.Append("CAST(");
            this.TranslateExpression(node.Expression);
            this.Append(" AS ");
            this.Append(this.TranslateDataType(node.SqlDataType));
            this.Append(")");
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
                    this.Append(", ");
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
                this.Append("(\r\n");

            var first = true;
            void Separator()
            {
                if (!first)
                    this.Append("\r\n");
                first = false;
            }

            for (var i = 0; i < node.Unions.Count; i++)
            {
                var unionItem = node.Unions[i];
                if (i > 0)
                {
                    Separator();
                    this.Append(unionItem.UnionType == SqlUnionType.UnionAll ? "UNION ALL" : "UNION");
                }
                Separator();
                // Each union member is emitted without its outer parentheses.
                this.suppressDerivedTableParens = true;
                this.TranslateExpression(unionItem.DerivedTable);
            }

            if (wrap)
                this.Append("\r\n)");
        }

        /// <summary>
        ///     <para>
        ///         Translates a standalone SELECT expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateStandaloneSelect(SqlStandaloneSelectExpression node)
        {
            this.Append("(SELECT ");
            this.TranslateSelectColumns(node.SelectList);
            this.Append(")");
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
            this.Append("UPDATE ");
            this.Append(alias);
            this.Append("\r\nSET ");

            var count = Math.Min(node.Columns.Count, node.Values.Count);
            for (var i = 0; i < count; i++)
            {
                if (i > 0)
                    this.Append(",\r\n\t");
                this.Append(node.Columns[i]);
                this.Append(" = ");
                this.TranslateExpression(node.Values[i]);
            }

            this.Append("\r\n");

            if (node.Outputs.Count > 0)
            {
                this.Append("OUTPUT ");
                for (var i = 0; i < node.Outputs.Count; i++)
                {
                    if (i > 0)
                        this.Append(", ");
                    this.TranslateExpression(node.Outputs[i].ColumnExpression);
                    this.Append(" AS ");
                    this.Append(node.Outputs[i].Alias);
                }
                this.Append("\r\n");
            }

            // Source query is emitted bare (no outer parentheses).
            this.suppressDerivedTableParens = true;
            this.TranslateExpression(node.Source);
        }

        /// <summary>Translates a single-row INSERT ... VALUES statement.</summary>
        protected virtual void TranslateInsert(SqlInsertExpression node)
        {
            this.Append("INSERT INTO ");
            this.Append(this.GetQualifiedTableName(node.Table));
            this.Append(" (");
            this.Append(string.Join(", ", node.Columns));
            this.Append(")\r\n");

            if (node.Outputs.Count > 0)
            {
                this.Append("OUTPUT ");
                for (var i = 0; i < node.Outputs.Count; i++)
                {
                    if (i > 0)
                        this.Append(", ");
                    this.TranslateExpression(node.Outputs[i].ColumnExpression);
                    this.Append(" AS ");
                    this.Append(node.Outputs[i].Alias);
                }
                this.Append("\r\n");
            }

            this.Append("VALUES (");
            for (var i = 0; i < node.Values.Count; i++)
            {
                if (i > 0)
                    this.Append(", ");
                this.TranslateExpression(node.Values[i]);
            }
            this.Append(")");
        }

        /// <summary>
        ///     <para>
        ///         Translates a DELETE expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateDelete(SqlDeleteExpression node)
        {
            this.Append("DELETE ");
            this.Append(this.GetAlias(node.DataSourceAlias));
            this.Append("\r\n");
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
            this.Append("INSERT INTO ");
            this.Append(this.GetQualifiedTableName(node.SqlTable));
            this.Append("(");
            this.Append(columns);
            this.Append(")\r\n");
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
            this.Append("/*");
            this.Append(node.Comment);
            this.Append("*/");
        }

        /// <summary>
        ///     <para>
        ///         Translates a SQL fragment expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateFragment(SqlFragmentExpression node)
        {
            this.Append(node.Fragment);
        }

        /// <summary>
        ///     <para>
        ///         Translates a queryable expression.
        ///     </para>
        /// </summary>
        protected virtual void TranslateQueryable(SqlQueryableExpression node)
        {
            this.Append("Queryable: {\r\n");
            this.TranslateExpression(node.Query);
            this.Append("\r\n}");
        }

        #endregion
    }
}
