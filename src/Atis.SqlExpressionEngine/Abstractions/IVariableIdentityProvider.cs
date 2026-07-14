using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.Abstractions
{
    /// <summary>
    ///     <para>
    ///         Computes a stable, deterministic identity string for a <em>variable</em> (captured local /
    ///         static member) LINQ node, so a translation-time query parameter can be correlated back to the
    ///         value re-extracted from a fresh expression tree on a cache hit.
    ///     </para>
    ///     <para>
    ///         Correlation must not rely on traversal order: the translator walks the (reshaped) SqlExpression
    ///         tree while the value re-extractor walks the original LINQ tree, and reshaping (CTE hoisting,
    ///         subtree copying) can reorder or duplicate parameters relative to LINQ order. Both the
    ///         parameter-creation path and the value re-extraction path compute identity from the same LINQ
    ///         node kind through this service, so a single registered implementation guarantees they agree.
    ///     </para>
    /// </summary>
    public interface IVariableIdentityProvider
    {
        /// <summary>
        ///     Returns a stable identity string for the given variable node. Deterministic across invocations
        ///     of the same compiled query.
        /// </summary>
        string GetIdentity(Expression variableNode);
    }
}
