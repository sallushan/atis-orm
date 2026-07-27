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
        T ExecuteScalarCommand<T>(DbCommand command);
        Task<T> ExecuteScalarCommandAsync<T>(DbCommand command, CancellationToken cancellationToken);
        int ExecuteNonQueryCommand(string sql, IEnumerable<DbParameter> dbParameters, CommandType text);
        Task<int> ExecuteNonQueryCommandAsync(string sql, IEnumerable<DbParameter> dbParameters, CommandType text, CancellationToken cancellationToken);
        void Transaction(Action work);
        DbReaderExecutionResult ExecuteReader(string sql, IEnumerable<DbParameter> dbParameters, CommandType text);
        Task<DbReaderExecutionResult> ExecuteReaderAsync(string sql, IEnumerable<DbParameter> dbParameters, CommandType text, CancellationToken cancellationToken);
    }
}