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

        bool _transactionStarted = false;

        public virtual void Transaction(Action work)
        {
            if (work is null)
                throw new ArgumentNullException(nameof(work));

            if (this._transactionStarted)
            {
                work();
                return;
            }

            _transactionStarted = true;

            try
            {
                // conn will be null in-case of _externalConnection is set
                var (conn, tx, wasClosed) = this.GetTransactionAndConnection();
                this._transactionConnection = conn;
                this._transaction = tx;
                try
                {
                    try
                    {
                        work();
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            this.RollbackTransaction(tx);
                        }
                        catch (Exception ex2)
                        {
                            throw new AggregateException(ex, ex2);
                        }
                        throw;
                    }
                    this.CommitTransaction(tx);
                }
                finally
                {
                    try { tx?.Dispose(); } catch { /*don't worry about it*/ }
                    // conn will be null in-case if _externalConnection is set.
                    try { conn?.Dispose(); } catch { /*don't worry about it*/ }

                    if (wasClosed && this._externalConnection != null)
                    {
                        try { this._externalConnection.Close(); } catch { /*don't worry about it*/ }
                    }
                }
            }
            finally
            {
                this._transaction = null;
                this._transactionConnection = null;
                _transactionStarted = false;
            }
        }

        // TODO: see if we can create a readonly struct for this tuple to avoid heap allocation.
        protected virtual (DbConnection, DbTransaction, bool) GetTransactionAndConnection()
        {
            if (this._externalConnection != null)
            {
                DbTransaction transaction1;

                var wasClosed = false;
                if (this._externalConnection.State != ConnectionState.Open)
                {
                    wasClosed = true;
                    this._externalConnection.Open();
                }
                try
                {
                    transaction1 = this._externalConnection.BeginTransaction();
                }
                catch (Exception ex)
                {
                    if (wasClosed)
                    {
                        try
                        {
                            this._externalConnection.Close();
                        }
                        catch (Exception ex2)
                        {
                            throw new AggregateException(ex, ex2);
                        }
                    }
                    throw;
                }

                return (null, transaction1, wasClosed);
            }

            DbConnection transactionConnection = null;
            DbTransaction transaction = null;

            try
            {
                transactionConnection = this.CreateConnection();
                transactionConnection.Open();
                transaction = transactionConnection.BeginTransaction();
            }
            catch
            {
                try { transaction?.Dispose(); } catch { /*don't worry*/ }
                try { transactionConnection?.Dispose(); } catch { /*don't worry*/ }
                throw;
            }

            return (transactionConnection, transaction, false);
        }

        protected virtual void CommitTransaction(DbTransaction tx)
        {
            if (tx is null)
                throw new ArgumentNullException(nameof(tx));

            tx.Commit();
            // TODO: save point
        }

        protected virtual void RollbackTransaction(DbTransaction tx)
        {
            if (tx is null)
                throw new ArgumentNullException(nameof(tx));

            tx.Rollback();
        }
    }
}
