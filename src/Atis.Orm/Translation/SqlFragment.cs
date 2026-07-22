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
    ///         every parameter is known (see <see cref="SqlParameterFragment"/>). This is what a
    ///         later stage walks to expand collection parameters into multiple placeholders.
    ///     </para>
    /// </summary>
    public abstract class SqlFragment
    {
        /// <summary>Appends this fragment's rendered text to <paramref name="sb"/>.</summary>
        public abstract void WriteTo(StringBuilder sb);
    }

    /// <summary>
    ///     <para>A run of literal SQL text (keywords, identifiers, aliases, punctuation).</para>
    /// </summary>
    public sealed class SqlTextFragment : SqlFragment
    {
        private readonly StringBuilder text;

        internal SqlTextFragment(StringBuilder text)
        {
            this.text = text ?? throw new ArgumentNullException(nameof(text));
        }

        /// <summary>The literal SQL text of this run.</summary>
        public string Text => this.text.ToString();

        /// <inheritdoc />
        // netstandard2.0 has no StringBuilder.Append(StringBuilder) overload.
        public override void WriteTo(StringBuilder sb) => sb.Append(this.text.ToString());
    }

    /// <summary>
    ///     <para>
    ///         Marks the exact point where a query parameter's placeholder (e.g. <c>@p0</c>) sits in
    ///         the output. Rendered as <see cref="IQueryParameter.Name"/>. This object <em>is</em> the
    ///         parameter marker; recording it is owned by the translator base and closed to providers.
    ///     </para>
    /// </summary>
    public sealed class SqlParameterFragment : SqlFragment
    {
        internal SqlParameterFragment(IQueryParameter parameter)
        {
            this.Parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
        }

        /// <summary>The parameter this marker stands in for.</summary>
        public IQueryParameter Parameter { get; }

        /// <inheritdoc />
        public override void WriteTo(StringBuilder sb) => sb.Append(this.Parameter.Name);
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
        private StringBuilder currentText;

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
            if (this.currentText == null)
            {
                this.currentText = new StringBuilder();
                this.fragments.Add(new SqlTextFragment(this.currentText));
            }
            return this.currentText;
        }

        /// <summary>Seals the current text run and records a parameter marker at this exact point.</summary>
        public void AddParameter(IQueryParameter parameter)
        {
            this.currentText = null;
            this.fragments.Add(new SqlParameterFragment(parameter));
        }

        /// <summary>Concatenates all fragments into the final SQL string.</summary>
        public string ToSql()
        {
            var sb = new StringBuilder();
            foreach (var fragment in this.fragments)
                fragment.WriteTo(sb);
            return sb.ToString();
        }

        /// <summary>Clears all buffered fragments for reuse across translations.</summary>
        public void Reset()
        {
            this.fragments = new List<SqlFragment>();
            this.currentText = null;
        }
    }
}
