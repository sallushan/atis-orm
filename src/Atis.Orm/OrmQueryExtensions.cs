using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

using Atis.Orm.Abstractions;
using Atis.Orm.Querying;
namespace Atis.Orm
{
    public static class OrmQueryExtensions
    {
        /// <summary>Starts the ORM's single-row fluent insert API.</summary>
        public static InsertValueStage<T> InsertEntity<T>(this IQueryProvider provider)
        {
            if (provider is null)
                throw new ArgumentNullException(nameof(provider));
            return new InsertValueStage<T>(provider);
        }

        /// <summary>
        ///     Starts the ORM's key-based fluent update API. Its terminal stages encode the operation
        ///     as a standard SqlExpressionEngine QueryExtensions.Update method call.
        /// </summary>
        public static UpdateSetStage<T> UpdateEntity<T>(this IQueryProvider provider)
        {
            if (provider is null)
                throw new ArgumentNullException(nameof(provider));
            return new UpdateSetStage<T>(provider);
        }

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

            return query.Provider.RequireAsync().ExecuteAsync<Task<int>>(call, cancellationToken);
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

            return query.Provider.RequireAsync().ExecuteAsync<Task<int>>(call, cancellationToken);
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

            return query.Provider.RequireAsync().ExecuteAsync<Task<int>>(call, cancellationToken);
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

            return query.Provider.RequireAsync().ExecuteAsync<Task<int>>(call, cancellationToken);
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

            return query.Provider.RequireAsync().ExecuteAsync<Task<int>>(call, cancellationToken);
        }

        /// <summary>Inserts one row built from <paramref name="insertFields"/> and returns the affected row count.</summary>
        public static Task<int> InsertAsync<T>(
            this IQueryable<T> query,
            Expression<Func<T>> insertFields,
            CancellationToken cancellationToken = default)
        {
            if (query is null)
                throw new ArgumentNullException(nameof(query));
            if (insertFields is null)
                throw new ArgumentNullException(nameof(insertFields));

            var insertMethod = new Func<IQueryable<T>, Expression<Func<T>>, int>(
                Atis.SqlExpressionEngine.QueryExtensions.Insert).Method;
            var call = Expression.Call(
                null,
                insertMethod,
                query.Expression,
                Expression.Quote(insertFields));
            return query.Provider.RequireAsync().ExecuteAsync<Task<int>>(call, cancellationToken);
        }

        /// <summary>Inserts one row and returns the <paramref name="outputFields"/> from its inserted row image.</summary>
        public static async Task<IReadOnlyList<IReadOnlyDictionary<string, object>>> InsertAsync<T>(
            this IQueryable<T> query,
            Expression<Func<T>> insertFields,
            Expression<Func<T, object[]>> outputFields,
            CancellationToken cancellationToken = default)
        {
            if (query is null)
                throw new ArgumentNullException(nameof(query));
            if (insertFields is null)
                throw new ArgumentNullException(nameof(insertFields));
            if (outputFields is null)
                throw new ArgumentNullException(nameof(outputFields));

            var insertMethod = new Func<IQueryable<T>, Expression<Func<T>>, Expression<Func<T, object[]>>, IReadOnlyList<IReadOnlyDictionary<string, object>>>(
                Atis.SqlExpressionEngine.QueryExtensions.Insert).Method;
            var call = Expression.Call(
                null,
                insertMethod,
                query.Expression,
                Expression.Quote(insertFields),
                Expression.Quote(outputFields));
            var outputRows = query.Provider.RequireAsync()
                                  .ExecuteAsync<IAsyncEnumerable<Dictionary<string, object>>>(call, cancellationToken);

            return await outputRows.DrainAsync(cancellationToken).ConfigureAwait(false);
        }

        public static async Task<List<T>> ToListAsync<T>(
            this IQueryable<T> query,
            CancellationToken cancellationToken = default)
        {
            if (query is null)
                throw new ArgumentNullException(nameof(query));

            var asyncEnumerable = query.Provider.RequireAsync()
                                       .ExecuteAsync<IAsyncEnumerable<T>>(query.Expression, cancellationToken);

            return await asyncEnumerable.DrainAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
