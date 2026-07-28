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

        /// <summary>
        ///     The transaction the current <see cref="Transaction(Action)"/> scope is running under, or
        ///     <c>null</c> when there is no active transaction. Provided so a derived class can implement
        ///     savepoints through the ADO.NET API (<c>DbTransaction.Save</c> on .NET 6+, or
        ///     <c>SqlTransaction.Save</c>) instead of issuing SQL.
        /// </summary>
        protected DbTransaction GetCurrentTransaction()
        {
            return this._transaction;
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
        // Savepoint counter for the *current* transaction only; reset when the transaction ends so the
        // generated names stay short and predictable.
        private int _savepointCount = 0;
        // Set when rolling back to a savepoint fails. At that point SQL Server reports
        // XACT_STATE() = -1 and the transaction can no longer be committed, but the caller of
        // TransactionWithSavepoint is *expected* to catch and carry on -- so without this flag they would
        // keep piling work onto a transaction that is already dead. Checked before commit.
        private bool _transactionPoisoned = false;

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

                    if (this._transactionPoisoned)
                    {
                        var poisonExp = new InvalidOperationException(
                            "The transaction cannot be committed because rolling back to a savepoint failed; " +
                            "the whole transaction has been rolled back.");
                        try
                        {
                            this.RollbackTransaction(tx);
                        }
                        catch (Exception ex)
                        {
                            throw new AggregateException(poisonExp, ex);
                        }
                        throw poisonExp;
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
                this._savepointCount = 0;
                this._transactionPoisoned = false;
                _transactionStarted = false;
            }
        }

        /// <summary>
        ///     <para>
        ///         Runs <paramref name="work"/> inside a savepoint of the surrounding transaction. If it
        ///         throws, only the work done since the savepoint is undone and the exception is
        ///         rethrown -- the surrounding transaction stays usable.
        ///     </para>
        ///     <para>
        ///         This is the one place where catching an exception inside a transaction is safe: a plain
        ///         nested <see cref="Transaction(Action)"/> has no way to undo partial work, so swallowing
        ///         there would commit it. Catch around this method instead.
        ///     </para>
        /// </summary>
        /// <exception cref="InvalidOperationException">There is no surrounding transaction.</exception>
        public virtual void TransactionWithSavepoint(Action work)
        {
            if (work is null)
                throw new ArgumentNullException(nameof(work));
            if (!this._transactionStarted)
                throw new InvalidOperationException(
                    $"{nameof(TransactionWithSavepoint)} cannot be called without an outer transaction.");
            if (this._transactionPoisoned)
                throw new InvalidOperationException(
                    "The transaction can no longer be used because rolling back to an earlier savepoint failed.");

            var tx = this._transaction
                     ?? throw new InvalidOperationException(
                         "Savepoint: transaction is no longer available; probably the connection was lost.");

            this._savepointCount++;
            var savepoint = $"at_tran_savepoint_{this._savepointCount}";

            // Outside the try: if creating the savepoint fails there is nothing to roll back to, and
            // rolling back to a name the server never saw would mask the real failure.
            this.CreateSavepoint(savepoint);

            try
            {
                work();
            }
            catch (Exception ex)
            {
                try
                {
                    this.RollbackToSavepoint(savepoint);
                }
                catch (Exception ex2)
                {
                    // SQL Server error 3931 lands here: the transaction is doomed and cannot roll back to
                    // a savepoint. The caller is expected to catch and continue, so mark the transaction
                    // dead rather than let them keep working in it.
                    this._transactionPoisoned = true;
                    throw new AggregateException(ex, ex2);
                }
                throw;
            }

            // Success only -- the failure path is RollbackToSavepoint's to clean up, since whether a
            // rollback discards the savepoint is provider specific. Exceptions propagate: a failed
            // release means the sub transaction is in a bad state, and the work itself has already
            // succeeded, so nothing is being masked.
            this.ReleaseSavepoint(savepoint);
        }

        /// <summary>
        ///     Creates a savepoint with the given name inside the current transaction. The name is
        ///     generated by <see cref="TransactionWithSavepoint(Action)"/>; implementations may use
        ///     <see cref="GetCurrentTransaction"/> or issue provider-specific SQL.
        /// </summary>
        protected abstract void CreateSavepoint(string savepoint);

        /// <summary>
        ///     <para>
        ///         Undoes everything done since <paramref name="savepoint"/> was created, leaving the
        ///         surrounding transaction open and usable.
        ///     </para>
        ///     <para>
        ///         <see cref="ReleaseSavepoint"/> is <em>not</em> called afterwards, because whether a
        ///         rollback discards the savepoint is provider specific: SQLite discards it, PostgreSQL
        ///         explicitly does not -- the savepoint stays established and can be rolled back to again.
        ///         An implementation whose rollback leaves the savepoint behind, and for which that costs
        ///         something, must release it here itself.
        ///     </para>
        /// </summary>
        protected abstract void RollbackToSavepoint(string savepoint);

        /// <summary>
        ///     <para>
        ///         Discards <paramref name="savepoint"/> after the work inside it succeeded. Called only
        ///         on the success path -- rolling back to a savepoint already discards it.
        ///     </para>
        ///     <para>
        ///         SQL Server and Oracle have no such statement; their savepoints simply persist until the
        ///         transaction ends, so the default here does nothing. Providers that do have it
        ///         (PostgreSQL, MySQL, SQLite, DB2 -- <c>RELEASE SAVEPOINT</c>) should override. For those,
        ///         releasing is not cosmetic: an unreleased savepoint leaves a live sub transaction, and a
        ///         loop that takes one savepoint per item will accumulate them for the life of the
        ///         transaction.
        ///     </para>
        /// </summary>
        protected virtual void ReleaseSavepoint(string savepoint)
        {
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
            // TODO: savepoint
        }

        protected virtual void RollbackTransaction(DbTransaction tx)
        {
            if (tx is null)
                throw new ArgumentNullException(nameof(tx));

            tx.Rollback();
        }
    }
}
