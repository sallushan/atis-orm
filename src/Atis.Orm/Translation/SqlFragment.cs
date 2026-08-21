using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using Atis.Orm.Abstractions;
using Atis.SqlExpressionEngine;

namespace Atis.Orm.Translation
{
    /// <summary>
    ///     <para>
    ///         A single piece of a translated SQL statement. The translator emits an ordered
    ///         sequence of fragments instead of building a flat string, so the exact position of
    ///         every parameter is known (see <see cref="SqlParameterFragment"/>).
    ///     </para>
    ///     <para>
    ///         Fragments carry no values, only positions: <see cref="SqlCommandRenderer"/> walks them
    ///         together with the execution-time parameter values to produce the SQL text and the matching
    ///         parameter bindings. That is what lets a collection parameter expand into a different number
    ///         of placeholders on every execution of the same cached query.
    ///     </para>
    /// </summary>
    public abstract class SqlFragment
    {
    }

    /// <summary>
    ///     <para>A run of literal SQL text (keywords, identifiers, aliases, punctuation).</para>
    /// </summary>
    public sealed class SqlTextFragment : SqlFragment
    {
        internal SqlTextFragment(string text)
        {
            this.Text = text ?? throw new ArgumentNullException(nameof(text));
        }

        /// <summary>The literal SQL text of this run.</summary>
        public string Text { get; }
    }

    /// <summary>
    ///     <para>
    ///         Marks the exact point where a query parameter's placeholder (e.g. <c>@p0</c>) sits in
    ///         the output. This object <em>is</em> the parameter marker; recording it is owned by the
    ///         translator base and closed to providers.
    ///     </para>
    /// </summary>
    public sealed class SqlParameterFragment : SqlFragment
    {
        internal SqlParameterFragment(IQueryParameter parameter, bool isExpandable, string emptyListTemplate)
        {
            this.Parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
            this.IsExpandable = isExpandable;
            this.EmptyListTemplate = emptyListTemplate;
        }

        /// <summary>The parameter this marker stands in for.</summary>
        public IQueryParameter Parameter { get; }

        /// <summary>
        ///     <para>
        ///         Whether this marker sits at a position that accepts a comma-separated list of values
        ///         (an <c>IN</c> list, a <c>CONCAT_WS</c> value operand, ...), so a collection value is
        ///         expanded into one placeholder per element at render time.
        ///     </para>
        ///     <para>
        ///         Expansion is decided by the translator at emit time, never inferred from the value: a
        ///         <c>byte[]</c> compared with <c>=</c> is a collection value too and must stay a single
        ///         parameter.
        ///     </para>
        /// </summary>
        public bool IsExpandable { get; }

        /// <summary>
        ///     <para>
        ///         Self-contained SQL emitted in place of the (expandable) value list when the collection is
        ///         empty; no parameter is bound. <c>null</c> for non-expandable markers.
        ///     </para>
        /// </summary>
        public string EmptyListTemplate { get; }
    }

    /// <summary>
    ///     <para>
    ///         A span of output that is emitted only when <see cref="Guard"/>'s value is not null at
    ///         execution time; otherwise the whole span - text, parameters and all - is skipped.
    ///     </para>
    ///     <para>
    ///         This is what makes an optional WHERE term possible without a cache entry per combination of
    ///         supplied values: one compiled query holds every term, and each execution decides which spans
    ///         survive. It differs from <see cref="SqlParameterFragment.EmptyListTemplate"/>, which only
    ///         substitutes one marker's own output; here the guard suppresses a whole nested run.
    ///     </para>
    ///     <para>
    ///         The guard is never itself written to the output, so it has no placeholder. Its value is read
    ///         through the same resolver as any other parameter, so it rebinds by identity on a cache hit.
    ///     </para>
    /// </summary>
    public sealed class SqlConditionalFragment : SqlFragment
    {
        internal SqlConditionalFragment(IQueryParameter guard, OptionalGuardKind guardKind, IReadOnlyList<SqlFragment> fragments)
        {
            this.Guard = guard ?? throw new ArgumentNullException(nameof(guard));
            this.GuardKind = guardKind;
            this.Fragments = fragments ?? throw new ArgumentNullException(nameof(fragments));
        }

        /// <summary>The parameter whose value decides whether <see cref="Fragments"/> are emitted.</summary>
        public IQueryParameter Guard { get; }

        /// <summary>
        ///     What counts as "no value" here. Decided by the translator from the expression tree, never
        ///     inferred from the value - an empty <c>byte[]</c> is not an absent filter.
        /// </summary>
        public OptionalGuardKind GuardKind { get; }

        /// <summary>Whether <paramref name="guardValue"/> means this span should be dropped.</summary>
        public bool IsAbsent(object guardValue)
        {
            if (guardValue is null || guardValue is DBNull)
                return true;

            return this.GuardKind == OptionalGuardKind.NullOrEmptyCollection
                   && guardValue is IEnumerable collection
                   && !(guardValue is string)
                   && !collection.GetEnumerator().MoveNext();
        }

        /// <summary>
        ///     The fragments emitted when the guard has a value. Flat: optional terms sit beside each other
        ///     in the predicate, never inside one another (see <see cref="SqlFragmentWriter.BeginOptional"/>).
        /// </summary>
        public IReadOnlyList<SqlFragment> Fragments { get; }
    }

    /// <summary>
    ///     <para>
    ///         Two alternative spellings of the same comparison, one for when <see cref="Parameter"/> holds a
    ///         value and one for when it is null. The renderer emits exactly one of them, chosen from the
    ///         value this execution binds.
    ///     </para>
    ///     <para>
    ///         C# and SQL disagree about <c>null</c>: <c>x.Col == v</c> means "both are null" in C# but is
    ///         never true in SQL when <c>v</c> is null. The comparison therefore has to be spelled
    ///         <c>col = @p</c> or <c>col IS NULL</c> depending on the value - and the value is not knowable
    ///         when the query is compiled, because a compiled query is cached by expression shape and
    ///         re-executed with whatever the caller supplies next.
    ///     </para>
    ///     <para>
    ///         Deciding at render time rather than at translation time is what keeps this to <strong>one</strong>
    ///         cache entry per call site. Folding the comparison at translation time bakes one execution's
    ///         answer into the cached SQL for every later one; putting the value's nullness into the cache key
    ///         instead is correct but costs up to 2^n entries for n nullable parameters, in a cache that does
    ///         not evict. Both branches are built by the translator, so no translation logic moves here.
    ///     </para>
    /// </summary>
    public sealed class SqlNullSwitchFragment : SqlFragment
    {
        internal SqlNullSwitchFragment(IQueryParameter parameter, IReadOnlyList<SqlFragment> whenHasValue, IReadOnlyList<SqlFragment> whenNull)
        {
            this.Parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
            this.WhenHasValue = whenHasValue ?? throw new ArgumentNullException(nameof(whenHasValue));
            this.WhenNull = whenNull ?? throw new ArgumentNullException(nameof(whenNull));
        }

        /// <summary>
        ///     <para>The parameter whose value picks the branch.</para>
        ///     <para>
        ///         It is not written to the output itself - <see cref="WhenHasValue"/> contains its own marker
        ///         for that - but it resolves through the same resolver as any other parameter, so it rebinds
        ///         by identity on a cache hit.
        ///     </para>
        /// </summary>
        public IQueryParameter Parameter { get; }

        /// <summary>The comparison as ordinary SQL (<c>col = @p</c>), emitted when the value is not null.</summary>
        public IReadOnlyList<SqlFragment> WhenHasValue { get; }

        /// <summary>
        ///     The null-test form (<c>col IS NULL</c>), emitted when the value is null. Binds no parameter for
        ///     the compared value, so a marker inside it would be wrong - there is nothing to send.
        /// </summary>
        public IReadOnlyList<SqlFragment> WhenNull { get; }

        /// <summary>Picks the branch <paramref name="value"/> calls for.</summary>
        public IReadOnlyList<SqlFragment> SelectBranch(object value)
            => value is null || value is DBNull ? this.WhenNull : this.WhenHasValue;
    }

    /// <summary>
    ///     <para>
    ///         Append-only buffer the translator writes into. Consecutive text appends coalesce into a
    ///         single <see cref="SqlTextFragment"/> run; adding a parameter seals the current run and
    ///         records a <see cref="SqlParameterFragment"/>. Because parameter positions are recorded
    ///         here (never by the derived translators), providers cannot bypass or corrupt them.
    ///     </para>
    ///     <para>
    ///         <see cref="BeginOptional"/> / <see cref="EndOptional"/> collect a run into a
    ///         <see cref="SqlConditionalFragment"/>, and <see cref="BeginNullSwitch"/> /
    ///         <see cref="SwitchToNullBranch"/> / <see cref="EndNullSwitch"/> collect two runs into a
    ///         <see cref="SqlNullSwitchFragment"/>. Redirecting the buffer is handled here rather than by
    ///         the translator for the same reason parameter positions are: a derived translator cannot leave
    ///         the fragment list half-built.
    ///     </para>
    ///     <para>
    ///         Spans nest, because a null switch can sit inside an optional term's predicate - that is the
    ///         ordinary shape of <c>WhereBuilder.Equal</c>. <strong>Only an optional span inside another
    ///         optional span is rejected</strong> (see <see cref="BeginOptional"/>).
    ///     </para>
    /// </summary>
    internal sealed class SqlFragmentWriter
    {
        private enum SpanKind { Optional, NullSwitch }

        /// <summary>One suspended output buffer: what to go back to when the span being built closes.</summary>
        private sealed class OpenSpan
        {
            public SpanKind Kind;
            public List<SqlFragment> Enclosing;
            public IQueryParameter Parameter;
            public OptionalGuardKind GuardKind;
            /// <summary>Null-switch only: the first branch, captured when the second one starts.</summary>
            public IReadOnlyList<SqlFragment> HasValueBranch;
            /// <summary>The run collected when the span closed - see <see cref="PopSpan"/>.</summary>
            public IReadOnlyList<SqlFragment> Collected;
        }

        private List<SqlFragment> fragments = new List<SqlFragment>();
        private readonly Stack<OpenSpan> openSpans = new Stack<OpenSpan>();
        private int openOptionalCount;
        private readonly StringBuilder currentText = new StringBuilder();
        private bool hasCurrentText;

        /// <summary>Appends literal SQL text to the current text run.</summary>
        public void Append(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;
            this.EnsureTextRun().Append(text);
        }

        /// <summary>Appends a single literal SQL character to the current text run.</summary>
        public void Append(char c) => this.EnsureTextRun().Append(c);

        private StringBuilder EnsureTextRun()
        {
            this.hasCurrentText = true;
            return this.currentText;
        }

        /// <summary>Seals the current text run and records a parameter marker at this exact point.</summary>
        public void AddParameter(IQueryParameter parameter, bool isExpandable, string emptyListTemplate)
        {
            this.FlushTextRun();
            this.fragments.Add(new SqlParameterFragment(parameter, isExpandable, emptyListTemplate));
        }

        /// <summary>
        ///     Starts a span that is only emitted when <paramref name="guard"/> has a value at execution
        ///     time. Everything appended until the matching <see cref="EndOptional"/> goes inside it.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Rejects a second, nested span. One optional term inside another's predicate has no
        ///         meaning - it would say "apply this filter only when some unrelated value was also
        ///         supplied" - and no sensible query reaches it: the only way to write one is to pass a
        ///         WhereBuilder call in a <em>column</em> position, which type-checks solely when the outer
        ///         term is itself over <c>bool</c>.
        ///     </para>
        /// </remarks>
        public void BeginOptional(IQueryParameter guard, OptionalGuardKind guardKind)
        {
            if (guard is null)
                throw new ArgumentNullException(nameof(guard));
            if (this.openOptionalCount > 0)
                throw new InvalidOperationException(
                    "An optional term cannot contain another optional term. Optional terms are joined with " +
                    "AND and sit beside each other; nesting one inside another's predicate has no meaning. " +
                    "This usually means a WhereBuilder call was passed where a column was expected.");

            this.PushSpan(new OpenSpan { Kind = SpanKind.Optional, Parameter = guard, GuardKind = guardKind });
            this.openOptionalCount++;
        }

        /// <summary>Closes the span opened by the matching <see cref="BeginOptional"/>.</summary>
        public void EndOptional()
        {
            var span = this.PopSpan(SpanKind.Optional, nameof(EndOptional), nameof(BeginOptional));
            this.openOptionalCount--;
            this.fragments.Add(new SqlConditionalFragment(span.Parameter, span.GuardKind, span.Collected));
        }

        /// <summary>
        ///     Starts the branch emitted when <paramref name="parameter"/> holds a value. Call
        ///     <see cref="SwitchToNullBranch"/> to start the other branch, then <see cref="EndNullSwitch"/>.
        /// </summary>
        public void BeginNullSwitch(IQueryParameter parameter)
        {
            if (parameter is null)
                throw new ArgumentNullException(nameof(parameter));

            this.PushSpan(new OpenSpan { Kind = SpanKind.NullSwitch, Parameter = parameter });
        }

        /// <summary>Ends the has-value branch and starts the null branch of the open null switch.</summary>
        public void SwitchToNullBranch()
        {
            var span = this.PeekSpan(SpanKind.NullSwitch, nameof(SwitchToNullBranch), nameof(BeginNullSwitch));
            if (span.HasValueBranch != null)
                throw new InvalidOperationException($"{nameof(SwitchToNullBranch)} was called twice for the same null switch.");

            this.FlushTextRun();
            span.HasValueBranch = this.fragments;
            this.fragments = new List<SqlFragment>();
        }

        /// <summary>Closes the span opened by the matching <see cref="BeginNullSwitch"/>.</summary>
        public void EndNullSwitch()
        {
            var span = this.PopSpan(SpanKind.NullSwitch, nameof(EndNullSwitch), nameof(BeginNullSwitch));
            if (span.HasValueBranch is null)
                throw new InvalidOperationException(
                    $"A null switch was closed without a {nameof(SwitchToNullBranch)}, so it has only one branch.");

            // PopSpan hands back the run collected since the last transition, which here is the null branch.
            this.fragments.Add(new SqlNullSwitchFragment(span.Parameter, span.HasValueBranch, span.Collected));
        }

        /// <summary>
        ///     Returns the fragments buffered so far. <see cref="Reset"/> installs a fresh list, so a
        ///     returned list is never mutated by a later translation.
        /// </summary>
        public IReadOnlyList<SqlFragment> GetFragments()
        {
            if (this.openSpans.Count > 0)
                throw new InvalidOperationException($"A {this.openSpans.Peek().Kind} span was opened and never closed.");

            this.FlushTextRun();
            return this.fragments;
        }

        /// <summary>Clears all buffered fragments for reuse across translations.</summary>
        public void Reset()
        {
            this.fragments = new List<SqlFragment>();
            this.openSpans.Clear();
            this.openOptionalCount = 0;
            this.currentText.Clear();
            this.hasCurrentText = false;
        }

        private void PushSpan(OpenSpan span)
        {
            this.FlushTextRun();
            span.Enclosing = this.fragments;
            this.openSpans.Push(span);
            this.fragments = new List<SqlFragment>();
        }

        private OpenSpan PeekSpan(SpanKind expected, string caller, string opener)
        {
            if (this.openSpans.Count == 0 || this.openSpans.Peek().Kind != expected)
                throw new InvalidOperationException($"{caller} was called without a matching {opener}.");
            return this.openSpans.Peek();
        }

        /// <summary>
        ///     Closes the innermost span, restores the enclosing buffer, and hands the run collected since the
        ///     span's last transition back on <see cref="OpenSpan.Collected"/>.
        /// </summary>
        private OpenSpan PopSpan(SpanKind expected, string caller, string opener)
        {
            this.PeekSpan(expected, caller, opener);
            this.FlushTextRun();
            var span = this.openSpans.Pop();
            span.Collected = this.fragments;
            this.fragments = span.Enclosing;
            return span;
        }

        private void FlushTextRun()
        {
            if (!this.hasCurrentText)
                return;

            this.fragments.Add(new SqlTextFragment(this.currentText.ToString()));
            this.currentText.Clear();
            this.hasCurrentText = false;
        }
    }
}
