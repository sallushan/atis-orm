using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Atis.Orm
{
    public abstract class DbCommunicationBase : IDbCommunication
    {
        private DbConnection _externalConnection;
        private DbConnection _transactionConnection;
        private DbConnection _currentConnection;
        private DbTransaction _transaction;
        private int _transactionCount = 0;

        public string ConnectionString { get; set; }
        public int? CommandTimeout { get; set; }

        public DbCommunicationBase(string connString)
        {
            this.InitializeInstance(connString, null, null);
        }

        public DbCommunicationBase(string connString, int commandTimeout)
        {
            this.InitializeInstance(connString, commandTimeout, null);
        }
        public DbCommunicationBase(DbConnection dbConnection)
        {
            this.InitializeInstance(null, null, dbConnection);
        }

        protected abstract DbConnection CreateConnection();

        protected DbConnection GetCurrentConnection()
        {
            return (this._transactionConnection ?? this._externalConnection)
                        ??
                        this._currentConnection;
        }

        private void InitializeInstance(string connString, int? commandTimeout, DbConnection dbConnection)
        {
            this.ConnectionString = connString;
            this.CommandTimeout = commandTimeout;
            this._externalConnection = dbConnection;
        }

        public void CloseConnection()
        {
            if (this._currentConnection != null)
            {
                this._currentConnection.Close();
                this._currentConnection.Dispose();
                this._currentConnection = null;
            }
        }

        public async Task CloseConnectionAsync()
        {
            if (this._currentConnection != null)
            {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
                await this._currentConnection.CloseAsync();
                await this._currentConnection.DisposeAsync();
#else
                this._currentConnection.Close();
                this._currentConnection.Dispose();
#endif
                this._currentConnection = null;
            }
        }

        public abstract DbCommand CreateCommand(string commandText, IEnumerable<DbParameter> dbParameters, CommandType commandType);

        public int ExecuteNonQueryCommand(DbCommand command)
        {
            if (this._transactionConnection == null && this._externalConnection == null)
            {
                using (var conn = this.CreateConnection())
                {
                    if (conn.State != ConnectionState.Open)
                    {
                        conn.Open();
                    }
                    command.Connection = conn;
                    return command.ExecuteNonQuery();
                }
            }
            else if (this._currentConnection != null)
            {
                throw new InvalidOperationException("_currentConnection should be null when _transactionConnection or _externalConnection is set.");
            }
            else
            {
                var conn = (this._transactionConnection ?? this._externalConnection);
                command.Connection = conn;
                bool wasOpened = false;
                if (conn.State != ConnectionState.Open)
                {
                    conn.Open();
                    wasOpened = true;
                }
                try
                {
                    return command.ExecuteNonQuery();
                }
                finally
                {
                    if (wasOpened)
                        conn.Close();
                }
            }
        }

        public async Task<int> ExecuteNonQueryCommandAsync(DbCommand command, CancellationToken cancellationToken)
        {
            if (this._transactionConnection == null && this._externalConnection == null)
            {
                using (var conn = this.CreateConnection())
                {
                    if (conn.State != ConnectionState.Open)
                    {
                        await conn.OpenAsync(cancellationToken);
                    }
                    command.Connection = conn;
                    return await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            else if (this._currentConnection != null)
            {
                throw new InvalidOperationException("_currentConnection should be null when _transactionConnection or _externalConnection is set.");
            }
            else
            {
                var conn = (this._transactionConnection ?? this._externalConnection);
                command.Connection = conn;
                bool wasOpened = false;

                if (conn.State != ConnectionState.Open)
                {
                    await conn.OpenAsync(cancellationToken);
                    wasOpened = true;
                }
                try
                {
                    return await command.ExecuteNonQueryAsync(cancellationToken);
                }
                finally
                {
                    if (wasOpened)
                    {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
                        await conn.CloseAsync();
#else
                        conn.Close();
#endif
                    }
                }
            }
        }

        public T ExecuteScalarCommand<T>(DbCommand command)
        {
            throw new NotImplementedException();
        }

        public Task<T> ExecuteScalarCommandAsync<T>(DbCommand command, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public void OpenConnection()
        {
            var conn = (this._transactionConnection ?? this._externalConnection)
                        ??
                        this._currentConnection;
            if (conn is null)
            {
                conn = this.CreateConnection();
                this._currentConnection = conn;
            }
            if (conn.State != ConnectionState.Open)
            {
                conn.Open();
            }
        }

        public Task OpenConnectionAsync(CancellationToken cancellationToken)
        {
            var conn = (this._transactionConnection ?? this._externalConnection)
                        ??
                        this._currentConnection;
            if (conn is null)
            {
                conn = this.CreateConnection();
                this._currentConnection = conn;
            }
            if (conn.State != ConnectionState.Open)
            {
                return conn.OpenAsync(cancellationToken);
            }
            return Task.CompletedTask;
        }
    }
}
