namespace Atis.SqlExpressionEngine.Abstractions
{
    /// <summary>
    ///     <para>
    ///         Implemented by a custom (<c>ExpressionType.Extension</c>) LINQ node whose identity depends on
    ///         state that is <em>not</em> reachable as a child expression.
    ///     </para>
    ///     <para>
    ///         The compiled-query cache key is a structural hash of the LINQ tree. For an extension node that
    ///         hash can only see the node's runtime type, its <c>Type</c> and whatever <c>VisitChildren</c>
    ///         exposes, so a node carrying a plain field — a name, a kind, a flag — would hash identically to
    ///         a sibling that differs only in that field, and the two would share one cached query. Returning
    ///         that field as <see cref="StructuralKey"/> puts it back into the key.
    ///     </para>
    ///     <para>
    ///         The key must describe the node's <em>shape</em> only. Anything that legitimately varies between
    ///         executions of the same query — most obviously a parameter's value — must be left out, or every
    ///         execution produces a new cache entry.
    ///     </para>
    /// </summary>
    public interface IStructuralKeyExpression
    {
        /// <summary>
        ///     Shape-defining state to fold into the cache key. Must be stable across executions of the same
        ///     query and cheap to hash; <c>null</c> contributes nothing.
        /// </summary>
        object StructuralKey { get; }
    }
}
