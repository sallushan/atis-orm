namespace Atis.Orm.Abstractions
{
    /// <summary>
    ///     <para>
    ///         Supplies the values of contextual nodes (<c>ContextualExpression</c>) — the logged-in user, the
    ///         current tenant — at the moment a query executes.
    ///     </para>
    ///     <para>
    ///         It is asked on every execution, including the first, and its answer is never cached with the
    ///         compiled query. Register it as scoped so each unit of work sees its own user.
    ///     </para>
    /// </summary>
    public interface IQueryContext
    {
        /// <summary>
        ///     Looks up the value for <paramref name="key"/>. Returns <c>false</c> when the context has nothing
        ///     under that key; a <c>null</c> value with <c>true</c> is a legitimate answer.
        /// </summary>
        bool TryGetValue(string key, out object value);
    }
}
