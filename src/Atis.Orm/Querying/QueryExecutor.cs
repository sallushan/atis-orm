using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Atis.Orm.Abstractions;
using Atis.Orm.DataAccess;
namespace Atis.Orm.Querying
{
    public class QueryExecutor : IQueryExecutor
    {
        private readonly IDatabaseAdapter dbAdapter;
        private readonly ICompiledQueryCacheProvider queryCacheProvider;
        private readonly IQueryCompiler queryCompiler;
        private readonly IExpressionVariableValuesExtractor expressionVariableValuesExtractor;
        private readonly INavigationInitializer navigationInitializer;
        private readonly IQueryContext queryContext;

        public QueryExecutor(IDatabaseAdapter dbAdapter, ICompiledQueryCacheProvider queryCacheProvider, IQueryCompiler queryCompiler, IExpressionVariableValuesExtractor expressionVariableValuesExtractor, INavigationInitializer navigationInitializer, IQueryContext queryContext)
        {
            this.queryContext = queryContext ?? throw new ArgumentNullException(nameof(queryContext));
            this.dbAdapter = dbAdapter ?? throw new ArgumentNullException(nameof(dbAdapter));
            this.queryCacheProvider = queryCacheProvider ?? throw new ArgumentNullException(nameof(queryCacheProvider));
            this.queryCompiler = queryCompiler ?? throw new ArgumentNullException(nameof(queryCompiler));
            this.expressionVariableValuesExtractor = expressionVariableValuesExtractor ?? throw new ArgumentNullException(nameof(expressionVariableValuesExtractor));
            this.navigationInitializer = navigationInitializer ?? throw new ArgumentNullException(nameof(navigationInitializer));
        }

        public virtual TResult Execute<TResult>(Expression expression)
        {
            IExecutionContext executionContext = this.GetExecutionContext(expression);
            if (executionContext.IsNonQuery)
            {
                var result = this.dbAdapter.ExecuteNonQuery(executionContext.Sql, executionContext.DbParameters);
                return (TResult)(object)result;
            }
            else if (executionContext is IChildAwareExecutionContext childAware)
            {
                // The child queries are read completely before the main query is opened, so no two readers are ever
                // open at once, and each main row finds its collections ready when its object is constructed.
                var childCollections = this.LoadChildCollections(childAware);
                return this.dbAdapter.Execute<TResult>(childAware.Sql, childAware.DbParameters, this.WrapWithNavigationInitializer(dr => childAware.ChildAwareElementFactory(dr, childCollections)));
            }
            else
            {
                return this.dbAdapter.Execute<TResult>(executionContext.Sql, executionContext.DbParameters, this.WrapWithNavigationInitializer(executionContext.ElementFactory));
            }
        }

        public virtual TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken)
        {
            IExecutionContext executionContext = this.GetExecutionContext(expression);
            if (executionContext.IsNonQuery)
            {
                var result = this.dbAdapter.ExecuteNonQueryAsync(executionContext.Sql, executionContext.DbParameters, cancellationToken);
                return (TResult)(object)result;
            }
            else if (executionContext is IChildAwareExecutionContext childAware)
            {
                return this.ExecuteChildAware<TResult>(childAware, cancellationToken);
            }
            else
            {
                return this.dbAdapter.ExecuteAsync<TResult>(executionContext.Sql, executionContext.DbParameters, this.WrapWithNavigationInitializer(executionContext.ElementFactory), cancellationToken);
            }
        }

        private static readonly MethodInfo OpenStreamChildAwareMethod = typeof(QueryExecutor).GetMethod(nameof(StreamChildAware), BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo OpenTaskChildAwareMethod = typeof(QueryExecutor).GetMethod(nameof(TaskChildAwareAsync), BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>
        ///     Async execution of a query with child collections. The child queries cannot be awaited here because the
        ///     caller gets <typeparamref name="TResult"/> back synchronously (an <c>IAsyncEnumerable&lt;T&gt;</c> or a
        ///     <c>Task&lt;T&gt;</c>), so the loading is deferred into it.
        /// </summary>
        private TResult ExecuteChildAware<TResult>(IChildAwareExecutionContext childAware, CancellationToken cancellationToken)
        {
            var resultType = typeof(TResult);
            if (resultType.IsGenericType)
            {
                var definition = resultType.GetGenericTypeDefinition();
                var elementType = resultType.GetGenericArguments()[0];
                if (definition == typeof(IAsyncEnumerable<>))
                    return (TResult)OpenStreamChildAwareMethod.MakeGenericMethod(elementType).Invoke(this, new object[] { childAware });
                if (definition == typeof(Task<>))
                    return (TResult)OpenTaskChildAwareMethod.MakeGenericMethod(elementType).Invoke(this, new object[] { childAware, cancellationToken });
            }
            throw new InvalidOperationException($"Expected TResult to be Task<T> or IAsyncEnumerable<T>, but got {resultType.Name}");
        }

        private IAsyncEnumerable<T> StreamChildAware<T>(IChildAwareExecutionContext childAware)
        {
            return new DeferredAsyncEnumerable<T>(async cancellationToken =>
            {
                var childCollections = await this.LoadChildCollectionsAsync(childAware, cancellationToken).ConfigureAwait(false);
                return this.dbAdapter.ExecuteAsync<IAsyncEnumerable<T>>(childAware.Sql, childAware.DbParameters, this.WrapWithNavigationInitializer(dr => childAware.ChildAwareElementFactory(dr, childCollections)), cancellationToken);
            });
        }

        private async Task<T> TaskChildAwareAsync<T>(IChildAwareExecutionContext childAware, CancellationToken cancellationToken)
        {
            var childCollections = await this.LoadChildCollectionsAsync(childAware, cancellationToken).ConfigureAwait(false);
            return await this.dbAdapter.ExecuteAsync<Task<T>>(childAware.Sql, childAware.DbParameters, this.WrapWithNavigationInitializer(dr => childAware.ChildAwareElementFactory(dr, childCollections)), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        ///     Runs every child query and reads it completely, grouping its rows by the link value of their parent.
        /// </summary>
        protected virtual ChildCollections LoadChildCollections(IChildAwareExecutionContext childAware)
        {
            var childCollections = new ChildCollections(childAware.ChildQueries.Count);
            for (var i = 0; i < childAware.ChildQueries.Count; i++)
            {
                var childQuery = childAware.ChildQueries[i];
                if (childQuery is null)
                    continue;
                var rows = this.dbAdapter.ExecuteEnumerable<ChildRow>(childQuery.Sql, childQuery.DbParameters, this.WrapChildRowFactory(childQuery.ElementFactory));
                foreach (var row in rows)
                    childCollections.Add(i, row);
            }
            return childCollections;
        }

        /// <summary>The asynchronous <see cref="LoadChildCollections"/>.</summary>
        protected virtual async Task<ChildCollections> LoadChildCollectionsAsync(IChildAwareExecutionContext childAware, CancellationToken cancellationToken)
        {
            var childCollections = new ChildCollections(childAware.ChildQueries.Count);
            for (var i = 0; i < childAware.ChildQueries.Count; i++)
            {
                var childQuery = childAware.ChildQueries[i];
                if (childQuery is null)
                    continue;
                var rows = this.dbAdapter.ExecuteEnumerableAsync<ChildRow>(childQuery.Sql, childQuery.DbParameters, this.WrapChildRowFactory(childQuery.ElementFactory));
                var enumerator = rows.GetAsyncEnumerator(cancellationToken);
                try
                {
                    while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                        childCollections.Add(i, enumerator.Current);
                }
                finally
                {
                    await enumerator.DisposeAsync().ConfigureAwait(false);
                }
            }
            return childCollections;
        }

        /// <summary>
        ///     The child items are entities like any other, so their lazy navigation properties are initialized too.
        /// </summary>
        protected virtual Func<IDataReader, object> WrapChildRowFactory(Func<IDataReader, object> rowFactory)
        {
            return dr =>
            {
                var row = (ChildRow)rowFactory(dr);
                this.navigationInitializer.Initialize(row.Item);
                return row;
            };
        }

        /// <inheritdoc/>
        public virtual IDbReaderSession OpenReader(Expression expression, Func<IDataReader, object> elementFactory)
        {
            if (elementFactory is null)
                throw new ArgumentNullException(nameof(elementFactory));

            var executionContext = this.GetExecutionContext(expression);
            // No navigation initializer: the caller's factory does not build an entity, so there are no
            // navigation properties to initialize and nothing for the wrapper to do but add a delegate.
            return this.dbAdapter.OpenReader(executionContext.Sql, executionContext.DbParameters, elementFactory);
        }

        /// <inheritdoc/>
        public virtual Task<IDbReaderSession> OpenReaderAsync(Expression expression, Func<IDataReader, object> elementFactory, CancellationToken cancellationToken)
        {
            if (elementFactory is null)
                throw new ArgumentNullException(nameof(elementFactory));

            var executionContext = this.GetExecutionContext(expression);
            return this.dbAdapter.OpenReaderAsync(executionContext.Sql, executionContext.DbParameters, elementFactory, cancellationToken);
        }

        /// <summary>
        ///     Wraps the (singleton-cached) element factory so that each materialized object has its
        ///     lazy navigation properties initialized via the scoped <see cref="INavigationInitializer"/>.
        ///     The wrapper only constructs lazy queries/delegates (no DB I/O), so it is safe to run while
        ///     the parent reader is open.
        /// </summary>
        protected virtual Func<IDataReader, object> WrapWithNavigationInitializer(Func<IDataReader, object> elementFactory)
        {
            return dr =>
            {
                var obj = elementFactory(dr);
                this.navigationInitializer.Initialize(obj);
                return obj;
            };
        }


        protected virtual IExecutionContext GetExecutionContext(Expression expression)
        {
            ICompiledQuery compiledQuery = this.GetOrAddCompiledQuery(expression, out bool cacheHit);
            IReadOnlyDictionary<string, object> parameterValuesByIdentity = null;
            if (cacheHit)
            {
                // Always the original tree, never a re-preprocessed one. Values are rebound by identity, so
                // the variable nodes preprocessing would add, drop or reorder are irrelevant here; and any
                // parameter the original tree cannot satisfy falls back to its translation-time value in
                // CompiledQueryBase.ResolveValue, i.e. behaves as a literal frozen at the first compile.
                parameterValuesByIdentity = this.expressionVariableValuesExtractor.ExtractVariableValuesByIdentity(expression);
            }
            IExecutionContext queryExecutionParameter = compiledQuery.GetExecutionContext(parameterValuesByIdentity, useInitialValues: !cacheHit, queryContext: this.queryContext);
            return queryExecutionParameter;
        }

        protected virtual ICompiledQuery GetOrAddCompiledQuery(Expression expression, out bool cacheHit)
        {
            if (!this.queryCacheProvider.TryGet(expression, out ICompiledQuery compiledQuery))
            {
                compiledQuery = this.queryCompiler.Compile(expression);
                this.queryCacheProvider.Add(expression, compiledQuery);
                cacheHit = false;
            }
            else
            {
                cacheHit = true;
            }
            return compiledQuery;
        }
    }
}
