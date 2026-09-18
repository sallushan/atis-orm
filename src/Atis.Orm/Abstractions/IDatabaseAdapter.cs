using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

using Atis.Orm.DataAccess;

namespace Atis.Orm.Abstractions
{
    public interface IDatabaseAdapter
    {
        /// <summary>
        ///     <para>
        ///         Runs <paramref name="query"/> and hands back the reader session over its result, instead
        ///         of a sequence that reads it. The caller owns the session and must dispose it, which is
        ///         what closes the reader and releases the connection.
        ///     </para>
        ///     <para>
        ///         The <c>Execute*</c> methods above cover every case where a row is wanted as an object.
        ///         This one exists for the case where what is wanted is the reader still being open: a
        ///         column consumed as a stream stays valid only while its row is current, so the session
        ///         has to outlive the call that produced it.
        ///     </para>
        /// </summary>
        IDbReaderSession OpenReader(string query, IEnumerable<DbParameter> dbParameters, Func<IDataReader, object> elementFactory);

        /// <summary>The asynchronous <see cref="OpenReader"/>.</summary>
        Task<IDbReaderSession> OpenReaderAsync(string query, IEnumerable<DbParameter> dbParameters, Func<IDataReader, object> elementFactory, CancellationToken cancellationToken = default);

        T Execute<T>(string query, IEnumerable<DbParameter> dbParameters, Func<IDataReader, object> elementFactory);
        T ExecuteAsync<T>(string query, IEnumerable<DbParameter> dbParameters, Func<IDataReader, object> elementFactory, CancellationToken cancellationToken = default);
        IEnumerable<T> ExecuteEnumerable<T>(string query, IEnumerable<DbParameter> dbParameters, Func<IDataReader, object> elementFactory);
        IAsyncEnumerable<T> ExecuteEnumerableAsync<T>(string query, IEnumerable<DbParameter> dbParameters, Func<IDataReader, object> elementFactory);
        int ExecuteNonQuery(string query, IEnumerable<DbParameter> dbParameters);
        Task<int> ExecuteNonQueryAsync(string query, IEnumerable<DbParameter> dbParameters, CancellationToken cancellationToken = default);
    }
}