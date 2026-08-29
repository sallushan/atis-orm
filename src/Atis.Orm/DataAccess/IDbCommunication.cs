using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Atis.Orm.DataAccess
{
    public interface IDbCommunication
    {
        void OpenConnection();
        Task OpenConnectionAsync(CancellationToken cancellationToken);
        void CloseConnection();
        Task CloseConnectionAsync();
        T ExecuteScalarCommand<T>(string sql, IEnumerable<DbParameter> dbParameters, CommandType text);
        Task<T> ExecuteScalarCommandAsync<T>(string sql, IEnumerable<DbParameter> dbParameters, CommandType text, CancellationToken cancellationToken);
        IReadOnlyList<IReadOnlyDictionary<string, object>> ExecuteDictionary(string sql, IEnumerable<DbParameter> dbParameters, CommandType text);
        Task<IReadOnlyList<IReadOnlyDictionary<string, object>>> ExecuteDictionaryAsync(string sql, IEnumerable<DbParameter> dbParameters, CommandType text, CancellationToken cancellationToken);
        int ExecuteNonQueryCommand(string sql, IEnumerable<DbParameter> dbParameters, CommandType text);
        Task<int> ExecuteNonQueryCommandAsync(string sql, IEnumerable<DbParameter> dbParameters, CommandType text, CancellationToken cancellationToken);
        bool IsInTransaction { get; }
        void UseTransaction(DbTransaction transaction);
        void Transaction(Action work);
        void Transaction(Action work, IsolationLevel isolationLevel);
        Task TransactionAsync(Func<Task> work, CancellationToken cancellationToken = default);
        Task TransactionAsync(Func<Task> work, IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
        void TransactionWithSavepoint(Action work);
        Task TransactionWithSavepointAsync(Func<Task> work, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Runs <paramref name="sql"/> and hands back the result set as a live session that maps each
        ///     row with <paramref name="elementFactory"/>, for a caller that wants to stream rows rather
        ///     than buffer them. Dispose the session when done -- that is the caller's whole obligation.
        ///     See the implementation for the full contract.
        /// </summary>
        IDbReaderSession OpenReader(string sql, IEnumerable<DbParameter> dbParameters, CommandType text, Func<IDataReader, object> elementFactory);

        /// <summary>The asynchronous <see cref="OpenReader"/>, same ownership contract.</summary>
        Task<IDbReaderSession> OpenReaderAsync(string sql, IEnumerable<DbParameter> dbParameters, CommandType text, Func<IDataReader, object> elementFactory, CancellationToken cancellationToken);
    }
}