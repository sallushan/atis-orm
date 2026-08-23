namespace Atis.SqlExpressionEngine
{
    /// <summary>
    ///     <para>
    ///         How a <c>LIKE</c> term decorates the value it is matching against.
    ///     </para>
    ///     <para>
    ///         The single-value forms carry this in their node type
    ///         (<see cref="SqlExpressions.SqlExpressionType.Like"/> and friends). The multi-value forms need it
    ///         as a value instead, because one node stands for a whole repeated group and its node type is
    ///         already spent saying so - see <see cref="SqlExpressions.SqlLikeAnyExpression"/>.
    ///     </para>
    /// </summary>
    public enum LikeMatchMode
    {
        /// <summary>Anywhere in the column: <c>LIKE '%' + value + '%'</c>.</summary>
        Contains = 0,

        /// <summary>At the start of the column: <c>LIKE value + '%'</c>.</summary>
        StartsWith = 1,

        /// <summary>At the end of the column: <c>LIKE '%' + value</c>.</summary>
        EndsWith = 2,

        /// <summary>
        ///     The value used verbatim: <c>LIKE value</c>. The caller supplies the wildcards, so this is SQL's
        ///     own <c>LIKE</c> with nothing added.
        /// </summary>
        Pattern = 3,
    }
}
