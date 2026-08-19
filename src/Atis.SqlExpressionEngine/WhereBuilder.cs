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

        private static string Name([CallerMemberName] string memberName = null) => $"{nameof(WhereBuilder)}.{memberName}";
    }
}
