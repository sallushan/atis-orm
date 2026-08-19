namespace Atis.SqlExpressionEngine
{
    /// <summary>
    ///     <para>
    ///         What counts as "no value" for an optional term's guard.
    ///     </para>
    ///     <para>
    ///         Chosen per term while the expression is being shaped, never inferred from the value at render
    ///         time - the same rule collection-parameter expansion follows, and for the same reason: a
    ///         <c>byte[]</c> is an empty-able collection too, and a blob compared with <c>=</c> must not
    ///         silently become "no filter" when it happens to be empty.
    ///     </para>
    /// </summary>
    public enum OptionalGuardKind
    {
        /// <summary>Only <c>null</c> deactivates the term. An empty string is a value.</summary>
        NullOnly = 0,

        /// <summary>
        ///     <c>null</c> or an empty collection deactivates the term. Used by the <c>IN</c> family, where an
        ///     empty list means "no filter" rather than "match nothing".
        /// </summary>
        NullOrEmptyCollection = 1,
    }
}
