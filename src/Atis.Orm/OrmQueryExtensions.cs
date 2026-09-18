using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

using Atis.Orm.Abstractions;
using Atis.Orm.DataAccess;
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

        /// <summary>
        ///     Starts the ORM's key-based fluent delete API. Its terminal encodes the operation as a
        ///     standard SqlExpressionEngine QueryExtensions.Delete method call.
        /// </summary>
        public static DeleteKeyStage<T> DeleteEntity<T>(this IQueryProvider provider)
        {
            if (provider is null)
                throw new ArgumentNullException(nameof(provider));
            return new DeleteKeyStage<T>(provider);
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

        /// <summary>
        ///     The asynchronous <see cref="Queryable.FirstOrDefault{TSource}(IQueryable{TSource})"/>: returns
        ///     the first row, or <c>default</c> when the query matches none.
        /// </summary>
        /// <remarks>
        ///     The <c>FirstOrDefault</c> call is appended to the expression tree rather than applied to the
        ///     rows coming back, so <c>FirstOrDefaultQueryMethodExpressionConverter</c> runs and the row limit
        ///     is pushed into the SQL as <c>TOP 1</c> — the same statement the synchronous path produces.
        ///     Taking the first row client-side instead would leave the statement unbounded.
        /// </remarks>
        public static Task<T> FirstOrDefaultAsync<T>(
            this IQueryable<T> query,
            CancellationToken cancellationToken = default)
        {
            if (query is null)
                throw new ArgumentNullException(nameof(query));

            var firstOrDefaultMethod = new Func<IQueryable<T>, T>(Queryable.FirstOrDefault).Method;
            var call = Expression.Call(null, firstOrDefaultMethod, query.Expression);

            return query.Provider.RequireAsync().ExecuteAsync<Task<T>>(call, cancellationToken);
        }

        /// <summary>
        ///     <para>
        ///         Reads one binary column of one row as a stream, so that a value too large to want in
        ///         memory -- a document, an image, a backup -- crosses the connection in pieces as it is
        ///         consumed. Everything else the ORM returns is a finished object with the connection
        ///         already given back; this is the exception, and the reason it is a separate terminal
        ///         rather than something the ordinary query path does on its own.
        ///     </para>
        ///     <para>
        ///         <strong>Dispose what comes back.</strong> It owns the reader, the command and a claim on
        ///         the connection until it is disposed. It is also only valid until then, which is why the
        ///         value cannot simply be a property on a materialized entity.
        ///     </para>
        ///     <para>
        ///         <strong>Returns <c>null</c> when there is nothing to read</strong> -- whether the query
        ///         matched no row or the column in that row is null. The two are one situation to a caller
        ///         holding a handle on a value, and a caller that must tell them apart should ask with an
        ///         ordinary query, which does not touch the large column at all.
        ///     </para>
        ///     <para>
        ///         Only the first row is fetched; the statement is limited rather than the result trimmed,
        ///         so the query stays as cheap as the caller wrote it. Anything the query can express --
        ///         joins, sub-queries, a predicate over a related table -- is available here, because this
        ///         differs from a normal query only in how the row is read.
        ///     </para>
        /// </summary>
        /// <param name="query">The query naming the row, for example <c>dbc.Documents.Where(x =&gt; x.Id == id)</c>.</param>
        /// <param name="field">The column to stream, as <c>x =&gt; x.Content</c>.</param>
        /// <param name="size">
        ///     Optional. A column holding the value's size in bytes, which becomes
        ///     <see cref="DbFieldStream.Length"/>. Worth passing when the schema has one: a database
        ///     streaming a value does not say up front how long it is, so without this the length is simply
        ///     not known until the stream ends.
        /// </param>
        /// <param name="progress">Optional. Receives the running total of bytes read.</param>
        public static DbFieldStream StreamField<T>(
            this IQueryable<T> query,
            Expression<Func<T, byte[]>> field,
            Expression<Func<T, long?>> size = null,
            IProgress<long> progress = null)
        {
            if (query is null)
                throw new ArgumentNullException(nameof(query));
            if (field is null)
                throw new ArgumentNullException(nameof(field));

            var expression = FieldStreamSupport.BuildQuery(query, field, size);
            var elementFactory = FieldStreamSupport.CreateReaderFactory(size != null, binary: true);
            var session = query.Provider.RequireReaderSession().OpenReader(expression, elementFactory);

            return FieldStreamSupport.ReadStream(session, progress);
        }

        /// <inheritdoc cref="StreamField{T}(IQueryable{T}, Expression{Func{T, byte[]}}, Expression{Func{T, long?}}, IProgress{long})"/>
        public static async Task<DbFieldStream> StreamFieldAsync<T>(
            this IQueryable<T> query,
            Expression<Func<T, byte[]>> field,
            Expression<Func<T, long?>> size = null,
            IProgress<long> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (query is null)
                throw new ArgumentNullException(nameof(query));
            if (field is null)
                throw new ArgumentNullException(nameof(field));

            var expression = FieldStreamSupport.BuildQuery(query, field, size);
            var elementFactory = FieldStreamSupport.CreateReaderFactory(size != null, binary: true);
            var session = await query.Provider.RequireReaderSession()
                                     .OpenReaderAsync(expression, elementFactory, cancellationToken)
                                     .ConfigureAwait(false);

            return await FieldStreamSupport.ReadStreamAsync(session, progress, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        ///     The character-column counterpart of
        ///     <see cref="StreamField{T}(IQueryable{T}, Expression{Func{T, byte[]}}, Expression{Func{T, long?}}, IProgress{long})"/>,
        ///     returning a <see cref="System.IO.TextReader"/> instead of a stream. Same ownership, same
        ///     <c>null</c> result, same single row. A separate name rather than an overload, because two
        ///     methods that differ only in what they return are two methods.
        /// </summary>
        /// <param name="query">The query naming the row.</param>
        /// <param name="field">The column to stream, as <c>x =&gt; x.Body</c>.</param>
        /// <param name="size">Optional. A column holding the value's length, which becomes <see cref="DbFieldTextReader.Length"/>.</param>
        /// <param name="progress">Optional. Receives the running total of characters read.</param>
        public static DbFieldTextReader StreamTextField<T>(
            this IQueryable<T> query,
            Expression<Func<T, string>> field,
            Expression<Func<T, long?>> size = null,
            IProgress<long> progress = null)
        {
            if (query is null)
                throw new ArgumentNullException(nameof(query));
            if (field is null)
                throw new ArgumentNullException(nameof(field));

            var expression = FieldStreamSupport.BuildQuery(query, field, size);
            var elementFactory = FieldStreamSupport.CreateReaderFactory(size != null, binary: false);
            var session = query.Provider.RequireReaderSession().OpenReader(expression, elementFactory);

            return FieldStreamSupport.ReadTextReader(session, progress);
        }

        /// <inheritdoc cref="StreamTextField{T}(IQueryable{T}, Expression{Func{T, string}}, Expression{Func{T, long?}}, IProgress{long})"/>
        public static async Task<DbFieldTextReader> StreamTextFieldAsync<T>(
            this IQueryable<T> query,
            Expression<Func<T, string>> field,
            Expression<Func<T, long?>> size = null,
            IProgress<long> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (query is null)
                throw new ArgumentNullException(nameof(query));
            if (field is null)
                throw new ArgumentNullException(nameof(field));

            var expression = FieldStreamSupport.BuildQuery(query, field, size);
            var elementFactory = FieldStreamSupport.CreateReaderFactory(size != null, binary: false);
            var session = await query.Provider.RequireReaderSession()
                                     .OpenReaderAsync(expression, elementFactory, cancellationToken)
                                     .ConfigureAwait(false);

            return await FieldStreamSupport.ReadTextReaderAsync(session, progress, cancellationToken).ConfigureAwait(false);
        }
    }
}
