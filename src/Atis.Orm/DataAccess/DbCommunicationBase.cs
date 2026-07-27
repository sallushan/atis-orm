using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Atis.Orm.DataAccess
{
    public abstract class DbCommunicationBase : IDbCommunication
    {
        private DbConnection _externalConnection;
        private DbConnection _transactionConnection;
        // _localConnection is something that should live for a single command.
        // Usually ExecuteNonQueryCommand or ExecuteScalarCommand within this class
        // opens and closes the connection immediately if there is no _externalConnection
        // or _transactionConnection.
        // Ideally speaking _localConnection should remain null almost all the time. Only
        // case when this variable will have value is through DataReader enumeration that
        // is done in DbAsyncEnumerator and DbEnumerator.
        private DbConnection _localConnection;
        private DbTransaction _transaction;
        private int _transactionCount = 0;

        public string ConnectionString { get; set; }
        public int? CommandTimeout { get; set; }

        public DbCommunicationBase(string connString)
        {
            this.InitializeInstance(connString, null, null);
        }

        public DbCommunicationBase(string connString, int? commandTimeout)
        {
            this.InitializeInstance(connString, commandTimeout, null);
        }

        public DbCommunicationBase(DbConnection dbConnection)
        {
            this.InitializeInstance(null, null, dbConnection);
        }

        public DbCommunicationBase(DbConnection dbConnection, int? commandTimeout)
        {
            this.InitializeInstance(null, commandTimeout, dbConnection);
        }

        protected abstract DbConnection CreateConnection();

        protected DbConnection GetCurrentConnection()
        {
            return (this._transactionConnection ?? this._externalConnection)
                        ??
                        this._localConnection;
        }

        private void InitializeInstance(string connString, int? commandTimeout, DbConnection dbConnection)
        {
            this.ConnectionString = connString;
            this.CommandTimeout = commandTimeout;
            this._externalConnection = dbConnection;
        }

        public void CloseConnection()
        {
            if (this._localConnection != null)
            {
                this._localConnection.Close();
                this._localConnection.Dispose();
                this._localConnection = null;
            }
        }

        public async Task CloseConnectionAsync()
        {
            if (this._localConnection != null)
            {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
                await this._localConnection.CloseAsync();
                await this._localConnection.DisposeAsync();
#else
                this._localConnection.Close();
                this._localConnection.Dispose();
#endif
                this._localConnection = null;
            }
        }

        protected abstract DbCommand CreateCommand(string commandText, IEnumerable<DbParameter> dbParameters, CommandType commandType);

        private DbCommand CreateCommandInternal(string commandText, IEnumerable<DbParameter> dbParameters, CommandType commandType)
        {
            var connection = this.GetCurrentConnection()
                 ?? throw new InvalidOperationException("No connection is available; the connection must be opened before creating a command.");
            var dbCommand = this.CreateCommand(commandText, dbParameters, commandType);
            dbCommand.Connection = connection;
            dbCommand.Transaction = this._transaction;
            return dbCommand;
        }

        public virtual DbReaderExecutionResult ExecuteReader(string commandText, IEnumerable<DbParameter> dbParameters, CommandType commandType)
        {
            DbCommand dbCommand = null;
            try
            {
                dbCommand = this.CreateCommandInternal(commandText, dbParameters, commandType);
                var dataReader = dbCommand.ExecuteReader(CommandBehavior.SequentialAccess);
                return new DbReaderExecutionResult(dataReader, dbCommand);
            }
            catch
            {
                dbCommand?.Dispose();
                throw;
            }
        }

        public virtual async Task<DbReaderExecutionResult> ExecuteReaderAsync(string commandText, IEnumerable<DbParameter> dbParameters, CommandType commandType, CancellationToken cancellationToken)
        {
            DbCommand dbCommand = null;
            try
            {
                dbCommand = this.CreateCommandInternal(commandText, dbParameters, commandType);
                var dataReader = await dbCommand.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
                return new DbReaderExecutionResult(dataReader, dbCommand);
            }
            catch
            {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
                if (dbCommand != null)
                {
                    await dbCommand.DisposeAsync();
                }
#else
                dbCommand?.Dispose();
#endif
                throw;
            }
        }

        public virtual int ExecuteNonQueryCommand(string commandText, IEnumerable<DbParameter> dbParameters, CommandType commandType)
        {
            if (this._localConnection != null)
            {
                throw new InvalidOperationException("_currentConnection should be null when _transactionConnection or _externalConnection is set.");
            }

            this.OpenConnection();
            try
            {
                using (var command = this.CreateCommandInternal(commandText, dbParameters, commandType))
                {
                    return command.ExecuteNonQuery();
                }
            }
            finally
            {
                this.CloseConnection();
            }
        }


        public async Task<int> ExecuteNonQueryCommandAsync(string sql, IEnumerable<DbParameter> dbParameters, CommandType text, CancellationToken cancellationToken)
        {
            if (this._localConnection != null)
            {
                throw new InvalidOperationException("_currentConnection should be null when _transactionConnection or _externalConnection is set.");
            }

            await this.OpenConnectionAsync(cancellationToken);
            try
            {
                using (var command = this.CreateCommandInternal(sql, dbParameters, text))
                {
                    return await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            finally
            {
                await this.CloseConnectionAsync();
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
                        this._localConnection;
            if (conn is null)
            {
                conn = this.CreateConnection();
                this._localConnection = conn;
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
                        this._localConnection;
            if (conn is null)
            {
                conn = this.CreateConnection();
                this._localConnection = conn;
            }
            if (conn.State != ConnectionState.Open)
            {
                return conn.OpenAsync(cancellationToken);
            }
            return Task.CompletedTask;
        }

        public virtual void Transaction(Action work)
        {
            if (work is null)
                throw new ArgumentNullException(nameof(work));

            this._transactionCount++;

            using (this.GetTransactionConnection())
            {
                try
                {
                    work();
                    this.CommitTransaction();
                }
                catch (Exception outerExp)
                {
                    try
                    {
                        this.RollbackTransaction();
                    }
                    catch (Exception ex)
                    {
                        throw new AggregateException(outerExp, ex);
                    }
                    throw;
                }
            }
        }

        protected virtual IDbConnection GetTransactionConnection()
        {
            IDbConnection result;
            if (this._externalConnection == null && this._transactionConnection == null)
            {
                this._transactionConnection = this.CreateConnection();
                this._transactionConnection.StateChange += new StateChangeEventHandler(this._transactionConnection_StateChange);
                this.OpenConnection();
                this._transaction = this._transactionConnection.BeginTransaction();
                result = this._transactionConnection;
            }
            else
            {
                if (this._externalConnection != null)
                {
                    if (this._externalConnection.State != ConnectionState.Open)
                    {
                        this.OpenConnection();
                    }
                    if (this._transaction == null)
                    {
                        this._transaction = this._externalConnection.BeginTransaction();
                    }
                }
                result = null;
            }
            return result;
        }

        private void _transactionConnection_StateChange(object sender, StateChangeEventArgs e)
        {
            if (e.CurrentState == ConnectionState.Closed)
            {
                this._transactionConnection = null;
                this._transaction = null;
            }
        }
        protected virtual void CommitTransaction()
        {
            this._transactionCount--;

            if (this._transactionCount <= 0)
            {
                this._transaction.Commit();
                this._transaction.Dispose();
                this._transaction = null;
                // TODO: implement save point
                //this._savePoints = 0;
            }
        }
        protected virtual void RollbackTransaction()
        {
            this._transactionCount--;

            if (this._transactionCount <= 0)
            {
                this._transaction.Rollback();
                this._transaction.Dispose();
                this._transaction = null;
            }
        }
    }
}
