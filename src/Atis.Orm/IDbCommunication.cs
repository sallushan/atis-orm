using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Atis.Orm
{
    public class ConnectionInfo
    {
        public DbConnection Connection { get; set; }
        public bool ShouldDispose { get; set; }
        public DbTransaction Transaction { get; set; }
    }
    
    public interface IDbCommunication
    {
        void OpenConnection();
        Task OpenConnectionAsync(CancellationToken cancellationToken);
        void CloseConnection();
        Task CloseConnectionAsync();
        T ExecuteScalarCommand<T>(DbCommand command);
        Task<T> ExecuteScalarCommandAsync<T>(DbCommand command, CancellationToken cancellationToken);
        DbCommand CreateCommand(string commandText, IEnumerable<DbParameter> dbParameters, CommandType commandType);
        //IDataReader ExecuteReader(DbCommand command, CommandBehavior sequentialAccess);
        //Task<DbDataReader> ExecuteReaderAsync(DbCommand command, CommandBehavior sequentialAccess, CancellationToken cancellationToken);
        int ExecuteNonQueryCommand(DbCommand command);
        Task<int> ExecuteNonQueryCommandAsync(DbCommand command, CancellationToken cancellationToken);
    }
}