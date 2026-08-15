using System;

using Atis.SqlExpressionEngine.ExpressionExtensions;

namespace Atis.SqlExpressionEngine
{
    /// <summary>
    ///     <para>
    ///         Creates the value nodes for expression trees built by hand at runtime.
    ///     </para>
    ///     <para>
    ///         Use this instead of <c>Expression.Constant</c> for anything that is a <em>value</em> rather
    ///         than a genuine constant. A constant is folded into the compiled-query cache key and frozen at
    ///         translation time, so each distinct value caches its own query; a named parameter keeps one
    ///         cached query per shape and binds its value afresh on every execution. See
    ///         <see cref="NamedParameterExpression"/> for the full contract.
    ///     </para>
    /// </summary>
    public static class SqlParam
    {
        /// <summary>
        ///     <para>
        ///         Creates a named parameter node holding <paramref name="value"/>, typed as
        ///         <typeparamref name="T"/>.
        ///     </para>
        ///     <para>
        ///         The type comes from <typeparamref name="T"/> rather than from the value, so a null value
        ///         still carries a type — which matters because the declared type is part of the cache key.
        ///         State it explicitly when it cannot be inferred: <c>SqlParam.Create&lt;string&gt;("dept", null)</c>.
        ///     </para>
        /// </summary>
        /// <typeparam name="T">Declared type of the value.</typeparam>
        /// <param name="name">
        ///     Name identifying this parameter within the query. It is the parameter's identity, which is what
        ///     lets its value be rebound on a cache hit; reusing one name for two different values in the same
        ///     query is rejected when the values are re-extracted.
        /// </param>
        /// <param name="value">The value to bind. May be <c>null</c>.</param>
        /// <remarks>
        ///     A caller that already holds a <see cref="Type"/> and cannot reach this generic overload — a
        ///     builder driven by reflection over entity properties, say — can use the
        ///     <see cref="NamedParameterExpression(string, object, Type)"/> constructor directly.
        /// </remarks>
        public static NamedParameterExpression Create<T>(string name, T value)
            => new NamedParameterExpression(name, value, typeof(T));
    }
}
