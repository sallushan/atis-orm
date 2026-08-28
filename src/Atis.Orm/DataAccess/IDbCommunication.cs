using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Atis.Orm.DataAccess
{
    public readonly struct DbReaderExecutionResult
    {
        public DbDataReader DataReader { get; }
        public DbCommand Command { get; }

        public DbReaderExecutionResult(DbDataReader dataReader, DbCommand command)
        {
            DataReader = dataReader ?? throw new ArgumentNullException(nameof(dataReader));
            Command = command ?? throw new ArgumentNullException(nameof(command));
        }
    }
    
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
        ///     Unlike every other command here, the caller owns what comes back: call
        ///     <see cref="OpenConnection"/> first, and afterwards dispose the reader, dispose the command and
        ///     call <see cref="CloseConnection"/>. See the implementation for the full contract.
        /// </summary>
        DbReaderExecutionResult ExecuteReader(string sql, IEnumerable<DbParameter> dbParameters, CommandType text);

        /// <summary>The asynchronous <see cref="ExecuteReader"/>, same ownership contract.</summary>
        Task<DbReaderExecutionResult> ExecuteReaderAsync(string sql, IEnumerable<DbParameter> dbParameters, CommandType text, CancellationToken cancellationToken);
    }
}