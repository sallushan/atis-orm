using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Atis.SqlExpressionEngine.Exceptions;

namespace Atis.SqlExpressionEngine
{
    /// <summary>
    ///     <para>
    ///         Optional WHERE terms for search queries: a term whose search value is <c>null</c> disappears
    ///         from the generated SQL instead of filtering anything out. Lets a search screen be written as
    ///         one declarative predicate rather than a chain of <c>if</c> statements building a query.
    ///     </para>
    ///     <code>
    ///         q.Where(x =&gt; WhereBuilder.Equal(x.StatusCode, p.StatusCode)
    ///                    &amp;&amp; WhereBuilder.Equal(x.CountryId, p.CountryId))
    ///     </code>
    ///     <para>
    ///         Every method here is a <em>marker</em>. It is never executed: a preprocessor rewrites the call
    ///         into an <see cref="ExpressionExtensions.OptionalPredicateExpression"/> while the expression tree
    ///         is being prepared, and the body exists only to throw if someone calls it in memory. See
    ///         <see cref="DirectCallNotSupportedException"/>.
    ///     </para>
    ///     <para>
    ///         <strong>Terms are only ever joined with <c>AND</c>.</strong> That is what makes an inactive term
    ///         a pure omission - there is no operator-dependent neutral element to pick. Joining an optional
    ///         term with <c>OR</c> is a logic error: an inactive term would make the whole disjunction true.
    ///     </para>
    ///     <para>
    ///         <strong>"No value" means <c>null</c>, and only <c>null</c>.</strong> An empty string is a value,
    ///         so <c>Equal(x.Name, "")</c> emits <c>Name = ''</c>. This is deliberate: the alternative (treating
    ///         whitespace as absent) cannot express "find the rows with an empty name".
    ///     </para>
    ///     <para>
    ///         The decision is made per <em>execution</em>, not per compile: the compiled query holds the term
    ///         under a guard and the renderer drops it when the guard's value is null, so one cached query
    ///         serves every combination of supplied and omitted values.
    ///     </para>
    /// </summary>
    public static class WhereBuilder
    {
        /// <summary>
        ///     <para>
        ///         An equality term that is omitted entirely when <paramref name="value"/> is <c>null</c>:
        ///         <c>column = @value</c>, or nothing at all.
        ///     </para>
        ///     <para>
        ///         Because a null value means "no filter", this can never generate <c>column = NULL</c>. Test
        ///         for null explicitly (<c>x.Column == null</c>) when that is what you want.
        ///     </para>
        /// </summary>
        /// <typeparam name="T">Type of both operands.</typeparam>
        /// <param name="column">The column (or any translatable expression) to compare.</param>
        /// <param name="value">The search value. <c>null</c> deactivates the term.</param>
        /// <returns>Never returns; this is a marker method.</returns>
        /// <exception cref="DirectCallNotSupportedException">Always, when called directly.</exception>
        public static bool Equal<T>(T column, T value) => throw new DirectCallNotSupportedException(Name());

        /// <summary>
        ///     <para>
        ///         A "contains" term, omitted when <paramref name="text"/> is <c>null</c>:
        ///         <c>column LIKE '%' + @text + '%'</c>. The wildcards are supplied here, so
        ///         <paramref name="text"/> is plain text, not a pattern.
        ///     </para>
        ///     <para>
        ///         <strong>This was <c>Like</c> in the old library.</strong> It is renamed because that name
        ///         said the opposite of what it did - SQL's <c>LIKE</c> means "match this pattern", while this
        ///         wraps your text in wildcards for you. Use <see cref="LikePattern"/> for the literal SQL
        ///         behaviour. The old name is gone rather than reused, so every call site becomes a compile
        ///         error to be looked at, instead of quietly changing meaning during migration.
        ///     </para>
        /// </summary>
        /// <param name="column">The column (or any translatable string expression) to match.</param>
        /// <param name="text">The text to look for. <c>null</c> deactivates the term.</param>
        /// <exception cref="DirectCallNotSupportedException">Always, when called directly.</exception>
        public static bool Contains(string column, string text) => throw new DirectCallNotSupportedException(Name());

        /// <summary>
        ///     <para>
        ///         A term using <paramref name="pattern"/> <em>verbatim</em>, omitted when it is <c>null</c>:
        ///         <c>column LIKE @pattern</c>. The caller supplies the wildcards, so this is the form to use
        ///         for patterns like <c>"A%B"</c> that the other three cannot express. This is SQL's own
        ///         <c>LIKE</c>, with nothing added.
        ///     </para>
        ///     <para>
        ///         <strong>This was <c>LikeOnly</c> in the old library.</strong> Renamed alongside
        ///         <see cref="Contains"/> so that neither old name survives with a changed meaning.
        ///     </para>
        /// </summary>
        /// <param name="column">The column (or any translatable string expression) to match.</param>
        /// <param name="pattern">The pattern, wildcards included. <c>null</c> deactivates the term.</param>
        /// <exception cref="DirectCallNotSupportedException">Always, when called directly.</exception>
        public static bool LikePattern(string column, string pattern) => throw new DirectCallNotSupportedException(Name());

        /// <summary>
        ///     <para>
        ///         A "starts with" term, omitted when <paramref name="pattern"/> is <c>null</c>:
        ///         <c>column LIKE @pattern + '%'</c>.
        ///     </para>
        /// </summary>
        /// <param name="column">The column (or any translatable string expression) to match.</param>
        /// <param name="pattern">The prefix to look for. <c>null</c> deactivates the term.</param>
        /// <exception cref="DirectCallNotSupportedException">Always, when called directly.</exception>
        public static bool StartsWith(string column, string pattern) => throw new DirectCallNotSupportedException(Name());

        /// <summary>
        ///     <para>
        ///         An "ends with" term, omitted when <paramref name="pattern"/> is <c>null</c>:
        ///         <c>column LIKE '%' + @pattern</c>.
        ///     </para>
        /// </summary>
        /// <param name="column">The column (or any translatable string expression) to match.</param>
        /// <param name="pattern">The suffix to look for. <c>null</c> deactivates the term.</param>
        /// <exception cref="DirectCallNotSupportedException">Always, when called directly.</exception>
        public static bool EndsWith(string column, string pattern) => throw new DirectCallNotSupportedException(Name());

        /// <summary>
        ///     <para>
        ///         An <c>IN</c> term, omitted when <paramref name="values"/> is <c>null</c> <em>or empty</em>:
        ///         <c>column IN (@v1, @v2, ...)</c>. An empty collection means "no filter", not "match
        ///         nothing" - the whole point of an optional term.
        ///     </para>
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="column">The column (or any translatable expression) to test.</param>
        /// <param name="values">The values to match. <c>null</c> or empty deactivates the term.</param>
        /// <exception cref="DirectCallNotSupportedException">Always, when called directly.</exception>
        public static bool In<T>(T column, IEnumerable<T> values) => throw new DirectCallNotSupportedException(Name());

        /// <summary>
        ///     <para>
        ///         A <c>NOT IN</c> term, omitted when <paramref name="values"/> is <c>null</c> or empty.
        ///     </para>
        ///     <para>
        ///         <strong>This is where the old library disagreed with itself.</strong> Its eager
        ///         <c>NotStringIn(x, null)</c> returned <c>false</c> (match nothing) while its expression form
        ///         emitted <c>1 = 1</c> (match everything). Here there is one answer, and it is the one every
        ///         other method on this class gives: no value means no filter.
        ///     </para>
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="column">The column (or any translatable expression) to test.</param>
        /// <param name="values">The values to exclude. <c>null</c> or empty deactivates the term.</param>
        /// <exception cref="DirectCallNotSupportedException">Always, when called directly.</exception>
        public static bool NotIn<T>(T column, IEnumerable<T> values) => throw new DirectCallNotSupportedException(Name());

        /// <summary>
        ///     <para>
        ///         A date-range term. Each bound is independently optional, so supplying only one still
        ///         filters: <c>column &gt;= @from</c> and <c>column &lt; DATEADD(day, 1, CAST(@to AS date))</c>.
        ///         With both <c>null</c> the term disappears entirely.
        ///     </para>
        ///     <para>
        ///         <strong>The upper bound includes the whole end day.</strong> Passing
        ///         <c>to = 2026-08-19</c> matches rows timestamped <c>2026-08-19 17:30</c>. That is why it is
        ///         emitted as a half-open <c>&lt;</c> against the next midnight rather than <c>&lt;=</c> - the
        ///         old library's code did the same thing, though its own documentation said <c>&lt;= to</c>.
        ///     </para>
        ///     <para>
        ///         The day arithmetic happens in <em>SQL</em>, not in C#, so the parameter stays the caller's
        ///         raw value. Computing it client-side would inject a value the original expression never had,
        ///         and it would freeze to the first execution's date on every cache hit.
        ///     </para>
        /// </summary>
        /// <param name="column">The date column (or any translatable date expression) to test.</param>
        /// <param name="from">Inclusive lower bound. <c>null</c> deactivates the lower bound.</param>
        /// <param name="to">Upper bound, inclusive of the whole day. <c>null</c> deactivates the upper bound.</param>
        /// <exception cref="DirectCallNotSupportedException">Always, when called directly.</exception>
        public static bool DateRange(DateTime? column, DateTime? from, DateTime? to) => throw new DirectCallNotSupportedException(Name());

        private static string Name([CallerMemberName] string memberName = null) => $"{nameof(WhereBuilder)}.{memberName}";
    }
}
