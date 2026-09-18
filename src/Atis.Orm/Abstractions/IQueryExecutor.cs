using System;
using System.Data;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

using Atis.Orm.DataAccess;

namespace Atis.Orm.Abstractions
{
    public interface IQueryExecutor
    {
        TResult Execute<TResult>(Expression expression);
        TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken);

        /// <summary>
        ///     <para>
        ///         Compiles <paramref name="expression"/> the way <see cref="Execute{TResult}"/> does --
        ///         same cache entry, same rebound parameters -- but hands back the open reader session
        ///         rather than reading it, and maps its rows with <paramref name="elementFactory"/> rather
        ///         than with the one compiled for the expression's element type.
        ///     </para>
        ///     <para>
        ///         Both departures are for the same caller: a column read as a stream. The value has to be
        ///         taken off the reader by something other than the compiled materializer, and the reader
        ///         has to stay open after this returns. The caller owns the session and disposing it is
        ///         what releases the connection.
        ///     </para>
        /// </summary>
        IDbReaderSession OpenReader(Expression expression, Func<IDataReader, object> elementFactory);

        /// <summary>The asynchronous <see cref="OpenReader"/>.</summary>
        Task<IDbReaderSession> OpenReaderAsync(Expression expression, Func<IDataReader, object> elementFactory, CancellationToken cancellationToken);
    }
}