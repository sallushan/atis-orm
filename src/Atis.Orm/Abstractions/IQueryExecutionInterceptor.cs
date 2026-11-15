using System.Linq.Expressions;

namespace Atis.Orm.Abstractions
{
    /// <summary>
    ///     <para>
    ///         A hook that sees the LINQ expression of every query just before it runs, so a consumer can
    ///         validate it (refuse a table, require a filter, reject a shape) without knowing anything
    ///         about the database. Throw to stop the query; nothing has reached the database by then.
    ///     </para>
    ///     <para>
    ///         <strong>It runs on every execution, on a cache hit as well as a miss</strong> — that is
    ///         the point of it, and what separates it from an expression preprocessor, which only runs
    ///         when a query is compiled. Because of that, an implementation must be cheap: walk the tree,
    ///         compare a few things, return. Keeping it light is the implementer's responsibility; anything
    ///         expensive should be remembered by the implementation itself.
    ///     </para>
    ///     <para>
    ///         It runs before the compiled-query cache is consulted. A query that is refused is therefore
    ///         never compiled and never cached, and the interceptor sees exactly the same thing the first
    ///         time as the hundredth. That is also why it gets the expression and nothing else: on the
    ///         first run there is no SQL yet.
    ///     </para>
    ///     <para>
    ///         It observes only. To change an expression, use an <c>IExpressionPreprocessor</c> — a change
    ///         made here would not be part of the cache key.
    ///     </para>
    ///     <para>
    ///         Implementations are created once, with the services, and may be shared between contexts
    ///         running at the same time, so they must be thread-safe. To read anything that belongs to one
    ///         context (the current user, say), take it from the expression's captured values or from a
    ///         scoped service; do not keep it in a field.
    ///     </para>
    ///     <para>
    ///         Several interceptors can be registered; they run in registration order, and the first one
    ///         to throw stops the rest. Register with
    ///         <see cref="OrmServiceBuilder.AddQueryExecutionInterceptor{TImplementation}"/>.
    ///     </para>
    /// </summary>
    public interface IQueryExecutionInterceptor
    {
        /// <summary>
        ///     Called with the expression about to be executed, for every way of executing one:
        ///     enumerating, the synchronous and asynchronous forms, and opening a streaming reader.
        ///     This includes the LINQ based insert, update and delete.
        /// </summary>
        /// <param name="expression">The expression exactly as the caller built it.</param>
        void QueryExecuting(Expression expression);
    }
}
