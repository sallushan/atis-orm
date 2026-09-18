using System;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

using Atis.Orm.DataAccess;

namespace Atis.Orm.Abstractions
{
    /// <summary>
    ///     <para>
    ///         The extra a query provider offers when a terminal needs the reader itself rather than the
    ///         rows read off it. Separate from <see cref="IQueryProvider"/> and from
    ///         <see cref="IAsyncQueryProvider"/> for the same reason those are separate from each other: a
    ///         provider that cannot do this must stay usable for everything else, so the demand is made at
    ///         the one terminal that needs it and nowhere earlier.
    ///     </para>
    ///     <para>
    ///         Only the streaming terminals ask for it. Everything else in the ORM wants a finished object.
    ///     </para>
    /// </summary>
    public interface IReaderSessionProvider
    {
        /// <inheritdoc cref="IQueryExecutor.OpenReader"/>
        IDbReaderSession OpenReader(Expression expression, Func<IDataReader, object> elementFactory);

        /// <inheritdoc cref="IQueryExecutor.OpenReaderAsync"/>
        Task<IDbReaderSession> OpenReaderAsync(Expression expression, Func<IDataReader, object> elementFactory, CancellationToken cancellationToken);
    }
}
