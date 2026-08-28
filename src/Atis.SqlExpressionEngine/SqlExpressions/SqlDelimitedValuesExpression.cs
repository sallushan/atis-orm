using System;
using System.Collections.Generic;

using Atis.SqlExpressionEngine.Visitors;

namespace Atis.SqlExpressionEngine.SqlExpressions
{
    /// <summary>
    ///     <para>
    ///         A collection of values supplied as <em>one delimited string</em> ("HR,IT,Finance") rather than as
    ///         a real collection. SQL-side counterpart of
    ///         <see cref="ExpressionExtensions.DelimitedValuesExpression"/>, produced by
    ///         <see cref="WhereBuilder.Delimited"/>.
    ///     </para>
    ///     <para>
    ///         This node only ever occupies a value-list position - the list inside <c>IN (...)</c>, the values
    ///         of a multi-value <c>LIKE</c>, or the guard of the optional term wrapping either. It has no
    ///         translation of its own, because a delimited string is not a SQL construct: it is one value that
    ///         the renderer reads as many.
    ///     </para>
    ///     <para>
    ///         <strong>The string is split at render time, never earlier.</strong> How many values it holds is a
    ///         property of the value, and a compiled query is cached by expression shape and re-executed with
    ///         whatever the caller supplies next - so splitting during translation would settle the placeholder
    ///         count from whichever string happened to be bound when the query was first compiled. Only the
    ///         <em>delimiter</em> is fixed at translation, because it is written in the source.
    ///     </para>
    /// </summary>
    public class SqlDelimitedValuesExpression : SqlExpression
    {
        /// <summary>Creates a delimited value list.</summary>
        /// <param name="values">The single string value holding the whole list.</param>
        /// <param name="delimiter">The text separating one value from the next.</param>
        /// <exception cref="ArgumentNullException">Either argument is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="delimiter"/> is empty.</exception>
        public SqlDelimitedValuesExpression(SqlExpression values, string delimiter)
        {
            this.Values = values ?? throw new ArgumentNullException(nameof(values));
            this.Delimiter = delimiter ?? throw new ArgumentNullException(nameof(delimiter));
            if (delimiter.Length == 0)
                throw new ArgumentException("The delimiter must not be empty.", nameof(delimiter));
        }

        /// <summary>
        ///     The single string value holding the whole list - a captured variable, or an inline constant.
        ///     It stays one value through translation; only the renderer sees the individual entries.
        /// </summary>
        public SqlExpression Values { get; }

        /// <summary>
        ///     The text separating one value from the next. A string rather than a character, so a line break
        ///     or any other multi-character separator can be used.
        /// </summary>
        public string Delimiter { get; }

        /// <inheritdoc />
        public override SqlExpressionType NodeType => SqlExpressionType.DelimitedValues;

        /// <inheritdoc />
        protected internal override SqlExpression Accept(SqlExpressionVisitor sqlExpressionVisitor)
        {
            return sqlExpressionVisitor.VisitSqlDelimitedValues(this);
        }

        /// <summary>Returns this node, or a new one when the child changed.</summary>
        public SqlExpression Update(SqlExpression values)
        {
            if (values == this.Values)
                return this;
            return new SqlDelimitedValuesExpression(values, this.Delimiter);
        }

        /// <summary>
        ///     <para>
        ///         Splits a delimited string into its values. <strong>This is the one definition of what
        ///         "delimited" means</strong> - the renderer, the optional term's guard test and the unit-test
        ///         translator all call it, so none of them can drift from the others.
        ///     </para>
        ///     <para>
        ///         Entries are trimmed, and entries that are empty after trimming are dropped: a value list
        ///         typed into a search box is full of incidental spaces, and <c>"HR,,IT"</c> or a trailing comma
        ///         means two values, not three. A string with nothing left after that returns no values at all,
        ///         which is what lets an optional term drop instead of emitting <c>IN ()</c> - the bug the old
        ///         library had here, where <c>RemoveEmptyEntries</c> was applied but emptiness was never
        ///         re-checked.
        ///     </para>
        ///     <para>
        ///         Trimming is also what makes a <c>"\n"</c> delimiter read Windows line endings: the carriage
        ///         return left on the end of each entry is whitespace, so it is trimmed away.
        ///     </para>
        /// </summary>
        /// <param name="value">The bound value: the delimited string, or <c>null</c>.</param>
        /// <param name="delimiter">The text separating one value from the next.</param>
        /// <returns>The values, in the order they appear. Empty when there are none.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="value"/> is neither null nor a string.</exception>
        public static IReadOnlyList<string> Split(object value, string delimiter)
        {
            if (value is null || value is DBNull)
                return EmptyValues;

            if (!(value is string text))
                throw new InvalidOperationException(
                    $"A delimited value list must be bound to a string, but a '{value.GetType().Name}' was " +
                    $"supplied. Pass the collection itself rather than wrapping it in " +
                    $"{nameof(WhereBuilder)}.{nameof(WhereBuilder.Delimited)} when you already have one.");

            // The single-string overload of Split is netstandard2.1+, so the array form is used instead.
            var parts = text.Split(new[] { delimiter }, StringSplitOptions.None);
            var values = new List<string>(parts.Length);
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.Length > 0)
                    values.Add(trimmed);
            }
            return values;
        }

        private static readonly string[] EmptyValues = new string[0];

        /// <inheritdoc />
        public override string ToString() => $"{this.NodeType}('{this.Delimiter}', {this.Values})";
    }
}
