using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Atis.Orm
{
    public static class OrmQueryExtensions
    {
        public static Task<int> DeleteAsync<T>(
            this IQueryable<T> query,
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            if (query is null)
                throw new ArgumentNullException(nameof(query));
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            var deleteMethod = new Func<IQueryable<T>, Expression<Func<T, bool>>, int>(
                Atis.SqlExpressionEngine.QueryExtensions.Delete).Method;

            var call = Expression.Call(
                null,
                deleteMethod,
                query.Expression,
                Expression.Quote(predicate));

            var asyncQueryProvider = query.Provider as IAsyncQueryProvider
                ?? throw new InvalidOperationException("The query provider does not support asynchronous operations.");

            return asyncQueryProvider.ExecuteAsync<Task<int>>(call, cancellationToken);
        }

        public static Task<int> DeleteAsync<T, R>(
            this IQueryable<T> query,
            Expression<Func<T, R>> tableSelection,
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            if (query is null)
                throw new ArgumentNullException(nameof(query));
            if (tableSelection is null)
                throw new ArgumentNullException(nameof(tableSelection));
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            var deleteMethod = new Func<IQueryable<T>, Expression<Func<T, R>>, Expression<Func<T, bool>>, int>(
                Atis.SqlExpressionEngine.QueryExtensions.Delete).Method;

            var call = Expression.Call(
                null,
                deleteMethod,
                query.Expression,
                Expression.Quote(tableSelection),
                Expression.Quote(predicate));

            var asyncQueryProvider = query.Provider as IAsyncQueryProvider
                ?? throw new InvalidOperationException("The query provider does not support asynchronous operations.");

            return asyncQueryProvider.ExecuteAsync<Task<int>>(call, cancellationToken);
        }

        public static Task<int> UpdateAsync<T>(
            this IQueryable<T> query,
            Expression<Func<T, T>> tableUpdateFields,
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            if (query is null)
                throw new ArgumentNullException(nameof(query));
            if (tableUpdateFields is null)
                throw new ArgumentNullException(nameof(tableUpdateFields));
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            var updateMethod = new Func<IQueryable<T>, Expression<Func<T, T>>, Expression<Func<T, bool>>, int>(
                Atis.SqlExpressionEngine.QueryExtensions.Update).Method;

            var call = Expression.Call(
                null,
                updateMethod,
                query.Expression,
                Expression.Quote(tableUpdateFields),
                Expression.Quote(predicate));

            var asyncQueryProvider = query.Provider as IAsyncQueryProvider
                ?? throw new InvalidOperationException("The query provider does not support asynchronous operations.");

            return asyncQueryProvider.ExecuteAsync<Task<int>>(call, cancellationToken);
        }

        public static Task<int> UpdateAsync<T, R>(
            this IQueryable<T> query,
            Expression<Func<T, R>> tableSelection,
            Expression<Func<T, R>> tableUpdateFields,
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            if (query is null)
                throw new ArgumentNullException(nameof(query));
            if (tableSelection is null)
                throw new ArgumentNullException(nameof(tableSelection));
            if (tableUpdateFields is null)
                throw new ArgumentNullException(nameof(tableUpdateFields));
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            var updateMethod = new Func<IQueryable<T>, Expression<Func<T, R>>, Expression<Func<T, R>>, Expression<Func<T, bool>>, int>(
                Atis.SqlExpressionEngine.QueryExtensions.Update).Method;

            var call = Expression.Call(
                null,
                updateMethod,
                query.Expression,
                Expression.Quote(tableSelection),
                Expression.Quote(tableUpdateFields),
                Expression.Quote(predicate));

            var asyncQueryProvider = query.Provider as IAsyncQueryProvider
                ?? throw new InvalidOperationException("The query provider does not support asynchronous operations.");

            return asyncQueryProvider.ExecuteAsync<Task<int>>(call, cancellationToken);
        }

        public static Task<int> BulkInsertAsync<T>(
            this IQueryable<T> query,
            CancellationToken cancellationToken = default)
        {
            if (query is null)
                throw new ArgumentNullException(nameof(query));

            // Your "insert" method in QueryExtensions is BulkInsert<T>(IQueryable<T>)
            var bulkInsertMethod = new Func<IQueryable<T>, int>(
                Atis.SqlExpressionEngine.QueryExtensions.BulkInsert).Method;

            var call = Expression.Call(
                null,
                bulkInsertMethod,
                query.Expression);

            var asyncQueryProvider = query.Provider as IAsyncQueryProvider
                ?? throw new InvalidOperationException("The query provider does not support asynchronous operations.");

            return asyncQueryProvider.ExecuteAsync<Task<int>>(call, cancellationToken);
        }

        public static async Task<List<T>> ToListAsync<T>(
            this IQueryable<T> query,
            CancellationToken cancellationToken = default)
        {
            if (query is null)
                throw new ArgumentNullException(nameof(query));

            var asyncQueryProvider = query.Provider as IAsyncQueryProvider
                ?? throw new InvalidOperationException("The query provider does not support asynchronous operations.");

            var asyncEnumerable = asyncQueryProvider.ExecuteAsync<IAsyncEnumerable<T>>(query.Expression, cancellationToken);

            var list = new List<T>();
            var enumerator = asyncEnumerable.GetAsyncEnumerator(cancellationToken);
            try
            {
                while (await enumerator.MoveNextAsync())
                    list.Add(enumerator.Current);
            }
            finally
            {
                await enumerator.DisposeAsync();
            }
            return list;
        }
    }
}
