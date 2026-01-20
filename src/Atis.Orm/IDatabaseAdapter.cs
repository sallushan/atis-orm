using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Atis.Orm
{
    public interface IDatabaseAdapter
    {
        T Execute<T>(string query, IEnumerable<DbParameter> dbParameters, Func<IDataReader, object> elementFactory);
        T ExecuteAsync<T>(string query, IEnumerable<DbParameter> dbParameters, Func<IDataReader, object> elementFactory, CancellationToken cancellationToken = default);
        IEnumerable<T> ExecuteEnumerable<T>(string query, IEnumerable<DbParameter> dbParameters, Func<IDataReader, object> elementFactory);
        IAsyncEnumerable<T> ExecuteEnumerableAsync<T>(string query, IEnumerable<DbParameter> dbParameters, Func<IDataReader, object> elementFactory);
        int ExecuteNonQuery(string query, IEnumerable<DbParameter> dbParameters);
        Task<int> ExecuteNonQueryAsync(string query, IEnumerable<DbParameter> dbParameters, CancellationToken cancellationToken = default);
    }
}