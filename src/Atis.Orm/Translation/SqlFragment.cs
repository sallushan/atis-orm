using System;
using System.Collections.Generic;
using System.Text;

using Atis.Orm.Abstractions;

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
        ///         SQL emitted in place of the (expandable) placeholder list when the collection is empty;
        ///         <c>{0}</c> stands for the parameter name, which is bound to <c>null</c>. <c>null</c> for
        ///         non-expandable markers.
        ///     </para>
        /// </summary>
        public string EmptyListTemplate { get; }
    }

    /// <summary>
    ///     <para>
    ///         Append-only buffer the translator writes into. Consecutive text appends coalesce into a
    ///         single <see cref="SqlTextFragment"/> run; adding a parameter seals the current run and
    ///         records a <see cref="SqlParameterFragment"/>. Because parameter positions are recorded
    ///         here (never by the derived translators), providers cannot bypass or corrupt them.
    ///     </para>
    /// </summary>
    internal sealed class SqlFragmentWriter
    {
        private List<SqlFragment> fragments = new List<SqlFragment>();
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
        ///     Returns the fragments buffered so far. <see cref="Reset"/> installs a fresh list, so a
        ///     returned list is never mutated by a later translation.
        /// </summary>
        public IReadOnlyList<SqlFragment> GetFragments()
        {
            this.FlushTextRun();
            return this.fragments;
        }

        /// <summary>Clears all buffered fragments for reuse across translations.</summary>
        public void Reset()
        {
            this.fragments = new List<SqlFragment>();
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
