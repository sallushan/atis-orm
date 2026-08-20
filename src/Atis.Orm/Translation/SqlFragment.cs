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
    ///         Append-only buffer the translator writes into. Consecutive text appends coalesce into a
    ///         single <see cref="SqlTextFragment"/> run; adding a parameter seals the current run and
    ///         records a <see cref="SqlParameterFragment"/>. Because parameter positions are recorded
    ///         here (never by the derived translators), providers cannot bypass or corrupt them.
    ///     </para>
    ///     <para>
    ///         <see cref="BeginOptional"/> / <see cref="EndOptional"/> collect a run into a
    ///         <see cref="SqlConditionalFragment"/>. Redirecting the buffer is handled here rather than by
    ///         the translator for the same reason parameter positions are: a derived translator cannot leave
    ///         the fragment list half-built.
    ///     </para>
    ///     <para>
    ///         <strong>At most one optional span is open at a time.</strong> Optional terms are joined with
    ///         <c>AND</c>, so they sit beside each other and each opens and closes before the next begins;
    ///         a two-bound <c>DateRange</c> is two sibling spans, not one inside the other.
    ///     </para>
    /// </summary>
    internal sealed class SqlFragmentWriter
    {
        private List<SqlFragment> fragments = new List<SqlFragment>();
        private List<SqlFragment> fragmentsOutsideOptional;
        private IQueryParameter openGuard;
        private OptionalGuardKind openGuardKind;
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
            if (this.openGuard != null)
                throw new InvalidOperationException(
                    "An optional term cannot contain another optional term. Optional terms are joined with " +
                    "AND and sit beside each other; nesting one inside another's predicate has no meaning. " +
                    "This usually means a WhereBuilder call was passed where a column was expected.");

            this.FlushTextRun();
            this.fragmentsOutsideOptional = this.fragments;
            this.openGuard = guard;
            this.openGuardKind = guardKind;
            this.fragments = new List<SqlFragment>();
        }

        /// <summary>Closes the span opened by the matching <see cref="BeginOptional"/>.</summary>
        public void EndOptional()
        {
            if (this.openGuard is null)
                throw new InvalidOperationException($"{nameof(EndOptional)} was called without a matching {nameof(BeginOptional)}.");

            this.FlushTextRun();
            var inner = this.fragments;
            this.fragments = this.fragmentsOutsideOptional;
            this.fragments.Add(new SqlConditionalFragment(this.openGuard, this.openGuardKind, inner));
            this.fragmentsOutsideOptional = null;
            this.openGuard = null;
        }

        /// <summary>
        ///     Returns the fragments buffered so far. <see cref="Reset"/> installs a fresh list, so a
        ///     returned list is never mutated by a later translation.
        /// </summary>
        public IReadOnlyList<SqlFragment> GetFragments()
        {
            if (this.openGuard != null)
                throw new InvalidOperationException("An optional span was opened and never closed.");

            this.FlushTextRun();
            return this.fragments;
        }

        /// <summary>Clears all buffered fragments for reuse across translations.</summary>
        public void Reset()
        {
            this.fragments = new List<SqlFragment>();
            this.fragmentsOutsideOptional = null;
            this.openGuard = null;
            this.currentText.Clear();
            this.hasCurrentText = false;
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
