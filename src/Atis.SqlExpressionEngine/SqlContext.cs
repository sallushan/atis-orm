using Atis.SqlExpressionEngine.ExpressionExtensions;

namespace Atis.SqlExpressionEngine
{
    /// <summary>
    ///     <para>
    ///         Names values held by the ambient context of an execution. See
    ///         <see cref="ContextualExpression"/> for the contract, and <see cref="ContextualValueAttribute"/>
    ///         for the property-based spelling.
    ///     </para>
    /// </summary>
    public static class SqlContext
    {
        /// <summary>
        ///     Marker for use inside a query lambda:
        ///     <c>dbc.Invoices.Where(x =&gt; x.CreatedBy == SqlContext.Get&lt;int&gt;("CurrentUserId"))</c>.
        ///     Never runs; the query pipeline replaces the call. <paramref name="key"/> must be a literal.
        /// </summary>
        /// <exception cref="System.NotSupportedException">Always, when called outside a query.</exception>
        public static T Get<T>(string key)
            => throw new System.NotSupportedException("SqlContext.Get<T> is a query marker and can only be used inside a query expression.");

        /// <summary>
        ///     Creates the node directly, for trees built by hand.
        /// </summary>
        public static ContextualExpression Create<T>(string key)
            => new ContextualExpression(key, typeof(T));
    }
}
