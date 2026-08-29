using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
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
        // case when this variable will have value is while an IDbReaderSession handed out by
        // OpenReader is still being read.
        private DbConnection _localConnection;
        // How many callers currently hold _localConnection open. Reference counted because the
        // field is a single slot with no ownership information of its own: two overlapping
        // readers, or a command running while a reader is enumerated, share the one connection,
        // and without a count the first to finish would close it under the others.
        private int _localConnectionOpenCount;
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

        /// <summary>
        ///     <para>
        ///         Releases one claim on the connection this instance opened for itself. The underlying
        ///         connection is closed and disposed only when the last claim goes, so a command running
        ///         while a reader is being enumerated does not close the reader's connection.
        ///     </para>
        ///     <para>
        ///         A connection the caller supplied, or one a transaction owns, is never touched here --
        ///         this instance did not open it and does not close it.
        ///     </para>
        /// </summary>
        public void CloseConnection()
        {
            if (!this.ReleaseLocalConnection())
                return;

            var connection = this._localConnection;
            this._localConnection = null;
            connection.Close();
            connection.Dispose();
        }

        /// <summary>The asynchronous <see cref="CloseConnection"/>.</summary>
        public async Task CloseConnectionAsync()
        {
            if (!this.ReleaseLocalConnection())
                return;

            var connection = this._localConnection;
            // Cleared before the await so a re-entrant call cannot find a connection that is already
            // on its way out.
            this._localConnection = null;
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
            await connection.CloseAsync().ConfigureAwait(false);
            await connection.DisposeAsync().ConfigureAwait(false);
#else
            connection.Close();
            connection.Dispose();
#endif
        }

        /// <summary>
        ///     Gives up one claim on <c>_localConnection</c> and reports whether that was the last one, so
        ///     the caller should now close it.
        /// </summary>
        private bool ReleaseLocalConnection()
        {
            if (this._localConnection == null)
                return false;

            if (this._localConnectionOpenCount > 0)
                return --this._localConnectionOpenCount == 0;

            // A connection still in hand with nobody claiming it means an unbalanced Open/Close
            // somewhere -- these methods are public on IDbCommunication, so a caller can close what it
            // never opened. Deliberately not an error: the count cannot go negative, and closing is the
            // safe reading when no one claims to be holding it.
            return true;
        }

        protected abstract DbCommand CreateCommand(string commandText, IEnumerable<DbParameter> dbParameters, CommandType commandType);

        private DbCommand CreateCommandInternal(string commandText, IEnumerable<DbParameter> dbParameters, CommandType commandType)
        {
            this.EnsureCallerTransactionIsStillUsable();
            var connection = this.GetCurrentConnection()
                 ?? throw new InvalidOperationException("No connection is available; the connection must be opened before creating a command.");
            var dbCommand = this.CreateCommand(commandText, dbParameters, commandType);
            dbCommand.Connection = connection;
            dbCommand.Transaction = this._transaction;
            return dbCommand;
        }

        /// <summary>
        ///     A transaction handed over by <see cref="UseTransaction"/> can be ended by its owner at any
        ///     time without telling this instance. A <see cref="DbTransaction"/> drops its
        ///     <see cref="DbTransaction.Connection"/> once that happens, which is the one signal available.
        ///     Reported rather than quietly cleared: carrying on would run the remaining commands outside
        ///     any transaction, which looks like success and is the harder failure to notice.
        /// </summary>
        private void EnsureCallerTransactionIsStillUsable()
        {
            if (this._transactionIsCallerOwned && this._transaction?.Connection is null)
            {
                throw new InvalidOperationException(
                    $"The transaction given to {nameof(UseTransaction)} has been committed, rolled back or " +
                    $"disposed, so no further command can run inside it. Call {nameof(UseTransaction)}(null) " +
                    "when the transaction ends, or hand over the new one.");
            }
        }

        /// <summary>
        ///     <para>
        ///         Runs <paramref name="commandText"/> and hands back the result set as a live session that
        ///         maps each row with <paramref name="elementFactory"/>, for a caller that wants to stream
        ///         rows rather than buffer them.
        ///     </para>
        ///     <para>
        ///         The connection, the command and the reader are opened here and owned by the session, so
        ///         <strong>the caller's whole obligation is to dispose it</strong> -- once, in any of the
        ///         spellings <see cref="IDbReaderSession"/> offers. Nothing has to be opened beforehand and
        ///         nothing has to be closed afterwards. If anything fails on the way in, the pieces already
        ///         opened are released before the exception leaves, so a failed call hands back nothing and
        ///         leaves nothing behind.
        ///     </para>
        ///     <para>
        ///         The row shape is the caller's, exactly as it is for <see cref="ExecuteDictionary"/> --
        ///         that one just has its factory built in. The reader is passed to
        ///         <paramref name="elementFactory"/> and is not otherwise reachable; a caller wanting it
        ///         raw passes an identity factory and takes responsibility for reading it in step with the
        ///         session.
        ///     </para>
        ///     <para>
        ///         This is the one command here whose work outlives the call -- every other one buffers its
        ///         result and returns with the connection already closed. Whether a second command may run
        ///         while a session is open is the driver's business, not this class's: SQL Server needs
        ///         <c>MultipleActiveResultSets=True</c> and otherwise fails with its own "there is already
        ///         an open DataReader" error, some providers allow it outright, and others never do. The
        ///         connection itself is safe either way -- it is reference counted, so a command running
        ///         meanwhile releases only its own claim.
        ///     </para>
        /// </summary>
        public virtual IDbReaderSession OpenReader(string commandText, IEnumerable<DbParameter> dbParameters, CommandType commandType, Func<IDataReader, object> elementFactory)
        {
            // Before the connection is opened, so a missing factory costs nothing to recover from.
            if (elementFactory is null)
                throw new ArgumentNullException(nameof(elementFactory));

            this.OpenConnection();
            DbCommand dbCommand = null;
            try
            {
                dbCommand = this.CreateCommandInternal(commandText, dbParameters, commandType);
                var dataReader = dbCommand.ExecuteReader(CommandBehavior.SequentialAccess);
                return new DbReaderSession(dataReader, dbCommand, this, elementFactory);
            }
            catch
            {
                dbCommand?.Dispose();
                // No session was handed back, so nothing else will ever release this claim.
                this.CloseConnection();
                throw;
            }
        }

        /// <summary>
        ///     The asynchronous <see cref="OpenReader"/>, and the same ownership contract: everything the
        ///     session needs is opened here, and disposing the session releases all of it.
        /// </summary>
        public virtual async Task<IDbReaderSession> OpenReaderAsync(string commandText, IEnumerable<DbParameter> dbParameters, CommandType commandType, Func<IDataReader, object> elementFactory, CancellationToken cancellationToken)
        {
            if (elementFactory is null)
                throw new ArgumentNullException(nameof(elementFactory));

            await this.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            DbCommand dbCommand = null;
            try
            {
                dbCommand = this.CreateCommandInternal(commandText, dbParameters, commandType);
                var dataReader = await dbCommand.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);
                return new DbReaderSession(dataReader, dbCommand, this, elementFactory);
            }
            catch
            {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
                if (dbCommand != null)
                {
                    await dbCommand.DisposeAsync().ConfigureAwait(false);
                }
#else
                dbCommand?.Dispose();
#endif
                await this.CloseConnectionAsync().ConfigureAwait(false);
                throw;
            }
        }

        public virtual int ExecuteNonQueryCommand(string commandText, IEnumerable<DbParameter> dbParameters, CommandType commandType)
        {
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
            await this.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using (var command = this.CreateCommandInternal(sql, dbParameters, text))
                {
                    return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                await this.CloseConnectionAsync().ConfigureAwait(false);
            }

        }

        /// <summary>
        ///     Runs <paramref name="commandText"/> and returns the first column of the first row, converted
        ///     to <typeparamref name="T"/>. A missing row or a <c>NULL</c> comes back as
        ///     <c>default(T)</c>.
        /// </summary>
        public virtual T ExecuteScalarCommand<T>(string commandText, IEnumerable<DbParameter> dbParameters, CommandType commandType)
        {
            this.OpenConnection();
            try
            {
                using (var command = this.CreateCommandInternal(commandText, dbParameters, commandType))
                {
                    return ConvertScalarResult<T>(command.ExecuteScalar());
                }
            }
            finally
            {
                this.CloseConnection();
            }
        }

        /// <summary>The asynchronous <see cref="ExecuteScalarCommand{T}"/>.</summary>
        public virtual async Task<T> ExecuteScalarCommandAsync<T>(string commandText, IEnumerable<DbParameter> dbParameters, CommandType commandType, CancellationToken cancellationToken)
        {
            await this.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using (var command = this.CreateCommandInternal(commandText, dbParameters, commandType))
                {
                    return ConvertScalarResult<T>(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
                }
            }
            finally
            {
                await this.CloseConnectionAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     <para>
        ///         Runs <paramref name="commandText"/> and buffers every row of the first result set as a
        ///         map of column name to value. Keys are matched with
        ///         <see cref="DictionaryKeyComparer"/>, case-insensitively unless a derived class says
        ///         otherwise; a <c>NULL</c> column comes back as <c>null</c> rather than
        ///         <see cref="DBNull"/>, and the key is still there. A query that returns no rows gives an
        ///         empty list, never <c>null</c>.
        ///     </para>
        ///     <para>
        ///         Everything is read before the method returns, so unlike <see cref="OpenReader"/> there
        ///         is nothing for the caller to dispose -- no reader, no command, no connection.
        ///     </para>
        /// </summary>
        /// <exception cref="InvalidOperationException">
        ///     Two columns in the result set have the same name, so one would overwrite the other.
        /// </exception>
        public virtual IReadOnlyList<IReadOnlyDictionary<string, object>> ExecuteDictionary(string commandText, IEnumerable<DbParameter> dbParameters, CommandType commandType)
        {
            this.OpenConnection();
            try
            {
                using (var command = this.CreateCommandInternal(commandText, dbParameters, commandType))
                // Not SequentialAccess: OpenReader streams and benefits from it, this buffers the whole
                // result set anyway, so the constraint would only buy a way to break later.
                using (var reader = command.ExecuteReader(CommandBehavior.Default))
                {
                    // Read the seam once rather than once per row -- an override is free to compute it.
                    var keyComparer = this.DictionaryKeyComparer ?? StringComparer.OrdinalIgnoreCase;
                    var columnNames = GetColumnNames(reader, keyComparer);
                    var rows = new List<IReadOnlyDictionary<string, object>>();
                    while (reader.Read())
                    {
                        rows.Add(ReadRow(reader, columnNames, keyComparer));
                    }
                    return rows;
                }
            }
            finally
            {
                this.CloseConnection();
            }
        }

        /// <summary>The asynchronous <see cref="ExecuteDictionary"/>.</summary>
        public virtual async Task<IReadOnlyList<IReadOnlyDictionary<string, object>>> ExecuteDictionaryAsync(string commandText, IEnumerable<DbParameter> dbParameters, CommandType commandType, CancellationToken cancellationToken)
        {
            await this.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using (var command = this.CreateCommandInternal(commandText, dbParameters, commandType))
                {
                    // The reader cannot go in a `using` here: where the framework has DisposeAsync it is
                    // the one to call, so the disposal has to be spelled out in a finally.
                    var reader = await command.ExecuteReaderAsync(CommandBehavior.Default, cancellationToken).ConfigureAwait(false);
                    try
                    {
                        // Read the seam once rather than once per row -- an override is free to compute it.
                        var keyComparer = this.DictionaryKeyComparer ?? StringComparer.OrdinalIgnoreCase;
                        var columnNames = GetColumnNames(reader, keyComparer);
                        var rows = new List<IReadOnlyDictionary<string, object>>();
                        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            rows.Add(ReadRow(reader, columnNames, keyComparer));
                        }
                        return rows;
                    }
                    finally
                    {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
                        await reader.DisposeAsync().ConfigureAwait(false);
#else
                        reader.Dispose();
#endif
                    }
                }
            }
            finally
            {
                await this.CloseConnectionAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     <para>
        ///         How <see cref="ExecuteDictionary"/> matches the column names it uses as keys. Case
        ///         insensitive by default, which is what an ADO.NET reader itself does: <c>GetOrdinal</c>
        ///         tries an exact match and then falls back to a case-insensitive one, so
        ///         <c>reader["tag"]</c> already finds a column named <c>Tag</c> and a dictionary standing
        ///         in for that indexer should not be stricter.
        ///     </para>
        ///     <para>
        ///         Override to return <see cref="StringComparer.Ordinal"/> for a database where two
        ///         columns of one result set can meaningfully differ only in case -- PostgreSQL and Oracle
        ///         allow it through quoted identifiers. Under the default comparer such a result set is
        ///         rejected as a duplicate rather than silently losing a column.
        ///     </para>
        /// </summary>
        protected virtual StringComparer DictionaryKeyComparer => StringComparer.OrdinalIgnoreCase;

        /// <summary>
        ///     The column names of the result set, in ordinal order. Read once per result set rather than
        ///     once per row: the names cannot change between rows, and neither can the answer to whether
        ///     two of them collide.
        /// </summary>
        // Shared with the element factory for an UPDATE ... OUTPUT, which builds the same shape from
        // plan-time column names rather than from the reader. See DictionaryRow.
        private static string[] GetColumnNames(DbDataReader reader, StringComparer keyComparer)
            => DictionaryRow.GetColumnNames(reader, keyComparer);

        private static IReadOnlyDictionary<string, object> ReadRow(DbDataReader reader, string[] columnNames, StringComparer keyComparer)
            => DictionaryRow.Read(reader, columnNames, keyComparer);

        /// <summary>
        ///     <para>
        ///         Maps the raw value <c>ExecuteScalar</c> returned onto <typeparamref name="T"/>. The cast
        ///         is tried first, so the common case -- the provider already handed back the right type --
        ///         costs nothing; only a mismatch falls through to <see cref="Convert.ChangeType(object, Type)"/>.
        ///         That mismatch is normal rather than exceptional: <c>COUNT</c> is <c>int</c> where the
        ///         caller may want <c>long</c>, and <c>SUM</c> over an integer column can come back wider
        ///         than the column.
        ///     </para>
        /// </summary>
        private static T ConvertScalarResult<T>(object value)
        {
            // No row at all, or the single column was NULL -- both are "nothing to convert".
            if (value is null || value is DBNull)
            {
                return default;
            }

            if (value is T typedValue)
            {
                return typedValue;
            }

            // Convert to the underlying type: ChangeType cannot target Nullable<> and would throw on it,
            // and the boxed result of the underlying type unboxes into T? fine.
            var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            if (targetType.IsEnum)
            {
                // ChangeType cannot produce an enum, so neither storage shape gets there on its own:
                // an int column is the underlying value, a varchar one is the member name.
                return value is string enumName
                        ? (T)Enum.Parse(targetType, enumName, ignoreCase: true)
                        : (T)Enum.ToObject(targetType, value);
            }
            return (T)Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }

        /// <summary>
        ///     <para>
        ///         Makes a connection available and takes a claim on it. Every call must be matched by
        ///         exactly one <see cref="CloseConnection"/>; the connection stays open until the last
        ///         claim is released, so overlapping readers and commands share one connection safely.
        ///     </para>
        ///     <para>
        ///         Whether the database then allows two commands to be active on that one connection is
        ///         the driver's business, not this class's: SQL Server needs
        ///         <c>MultipleActiveResultSets=True</c> and otherwise fails with its own "there is already
        ///         an open DataReader" error, while some providers allow it outright and others never do.
        ///         Nothing here inspects or second-guesses that.
        ///     </para>
        /// </summary>
        public void OpenConnection()
        {
            var shared = this._transactionConnection ?? this._externalConnection;
            if (shared != null)
            {
                // Not ours to own: open it if the caller left it closed, but never count it and never
                // close it.
                if (shared.State != ConnectionState.Open)
                    shared.Open();
                return;
            }

            if (this._localConnection != null)
            {
                this.PrepareExistingLocalConnection();
                if (this._localConnection.State != ConnectionState.Open)
                    this._localConnection.Open();
                this._localConnectionOpenCount++;
                return;
            }

            var connection = this.CreateConnection();
            try
            {
                connection.Open();
            }
            catch
            {
                // Storing a connection that never opened would leave every later call retrying Open()
                // on the same dead instance.
                connection.Dispose();
                throw;
            }
            this._localConnection = connection;
            this._localConnectionOpenCount = 1;
        }

        /// <summary>The asynchronous <see cref="OpenConnection"/>.</summary>
        public async Task OpenConnectionAsync(CancellationToken cancellationToken)
        {
            var shared = this._transactionConnection ?? this._externalConnection;
            if (shared != null)
            {
                if (shared.State != ConnectionState.Open)
                    await shared.OpenAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            if (this._localConnection != null)
            {
                this.PrepareExistingLocalConnection();
                if (this._localConnection.State != ConnectionState.Open)
                    await this._localConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
                this._localConnectionOpenCount++;
                return;
            }

            var connection = this.CreateConnection();
            try
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                connection.Dispose();
                throw;
            }
            this._localConnection = connection;
            this._localConnectionOpenCount = 1;
        }

        /// <summary>
        ///     A broken connection cannot be reopened as it stands; closing it first puts it back in a
        ///     state where <c>Open</c> works. The instance is kept rather than replaced, so any claim
        ///     already counted against it stays valid.
        /// </summary>
        private void PrepareExistingLocalConnection()
        {
            if (this._localConnection.State == ConnectionState.Broken)
                this._localConnection.Close();
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

        // Set when the transaction came from the caller through UseTransaction rather than being begun
        // here. It is the difference between "there is a transaction" and "there is a transaction we are
        // responsible for ending", and only the second may be committed or rolled back.
        private bool _transactionIsCallerOwned = false;

        /// <summary>
        ///     <para>
        ///         Runs everything from here on inside <paramref name="transaction"/>, which the caller began
        ///         and continues to own: every command is enlisted in it, and <see cref="Transaction(Action)"/>
        ///         stops beginning one of its own and simply runs the work. Nothing here ever commits, rolls
        ///         back, or disposes it, and the connection it belongs to is neither opened nor closed.
        ///     </para>
        ///     <para>
        ///         The point is that existing code wrapping its work in <see cref="Transaction(Action)"/>
        ///         keeps working unchanged when a caller supplies a transaction from outside -- it neither
        ///         has to know nor has to be rewritten. Pass <c>null</c> to stop using the caller's
        ///         transaction, after which <see cref="Transaction(Action)"/> begins its own again.
        ///     </para>
        ///     <para>
        ///         Without this, a connection that already carries a transaction cannot be used at all: the
        ///         driver refuses a command that is not enlisted in the pending transaction, and refuses a
        ///         second <c>BeginTransaction</c> on top of it. Joining it cannot be done automatically
        ///         because ADO.NET offers no way to ask a connection what transaction it is in -- hence the
        ///         caller handing it over explicitly.
        ///     </para>
        /// </summary>
        /// <exception cref="InvalidOperationException">
        ///     A transaction begun by this instance is currently running; <paramref name="transaction"/> has
        ///     already ended; or it belongs to a different connection than the one this instance was given.
        /// </exception>
        public virtual void UseTransaction(DbTransaction transaction)
        {
            if (transaction is null)
            {
                if (!this._transactionIsCallerOwned)
                    return;

                this._transaction = null;
                this._transactionConnection = null;
                this._transactionStarted = false;
                this._transactionIsCallerOwned = false;
                this._savepointCount = 0;
                this._transactionPoisoned = false;
                return;
            }

            if (this._transactionStarted && !this._transactionIsCallerOwned)
                throw new InvalidOperationException(
                    $"{nameof(UseTransaction)} cannot be called while a transaction started by this " +
                    $"{nameof(IDbCommunication)} is running -- the work already done would be left on a " +
                    "transaction nothing will commit. Call it before entering " +
                    $"{nameof(Transaction)}, not inside it.");

            var transactionConnection = transaction.Connection
                ?? throw new InvalidOperationException(
                    $"The transaction passed to {nameof(UseTransaction)} has already been committed, rolled " +
                    "back or disposed, so nothing can run inside it.");

            if (this._externalConnection != null && !ReferenceEquals(this._externalConnection, transactionConnection))
                throw new InvalidOperationException(
                    $"The transaction passed to {nameof(UseTransaction)} belongs to a different connection " +
                    $"than the one this {nameof(IDbCommunication)} was constructed with. A command can only " +
                    "run in a transaction on its own connection.");

            this._transaction = transaction;
            // Also the connection everything runs on: GetCurrentConnection prefers it, and it is treated as
            // a connection this instance does not own, so it is never counted, closed or disposed here.
            this._transactionConnection = transactionConnection;
            // Makes Transaction() take its nested pass-through branch at any depth, which is exactly the
            // wanted behaviour: run the work, begin and end nothing.
            this._transactionStarted = true;
            this._transactionIsCallerOwned = true;
            this._savepointCount = 0;
            this._transactionPoisoned = false;
        }

        /// <summary>
        ///     Whether work running now is inside a transaction -- one begun by
        ///     <see cref="Transaction(Action, IsolationLevel?)"/> or one handed over by
        ///     <see cref="UseTransaction"/>. Says nothing about which of the two: to a caller deciding
        ///     whether its work is already covered, they are the same thing.
        /// </summary>
        public virtual bool IsInTransaction => this._transactionStarted;

        /// <summary>
        ///     <para>
        ///         Runs <paramref name="work"/> inside a transaction, committing when it returns and rolling
        ///         back when it throws. A nested call joins the transaction already in progress and neither
        ///         commits nor rolls back on its own -- only the outermost call does.
        ///     </para>
        ///     <para>
        ///         The transaction begins at the provider's default isolation level; use the overload taking
        ///         an <see cref="IsolationLevel"/> to choose one.
        ///     </para>
        /// </summary>
        public virtual void Transaction(Action work) => this.RunTransaction(work, null);

        /// <inheritdoc cref="Transaction(Action)"/>
        /// <param name="work">The work to run inside the transaction.</param>
        /// <param name="isolationLevel">
        ///     The level to begin at. It applies only to a transaction begun here: on a nested call, and
        ///     after <see cref="UseTransaction"/>, the surrounding transaction already exists and its level
        ///     stands -- passing one there changes nothing rather than failing, so that business code
        ///     written as <c>Transaction(work, level)</c> keeps running unchanged when a caller supplies a
        ///     transaction from outside.
        /// </param>
        public virtual void Transaction(Action work, IsolationLevel isolationLevel)
            => this.RunTransaction(work, isolationLevel);

        private void RunTransaction(Action work, IsolationLevel? isolationLevel)
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
                var (conn, tx, wasClosed) = this.GetTransactionAndConnection(isolationLevel);
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
        ///         The asynchronous <see cref="Transaction(Action, IsolationLevel?)"/>. Shares
        ///         <c>_transactionStarted</c> with the synchronous version, so mixing the two nests
        ///         correctly rather than starting a second transaction.
        ///     </para>
        ///     <para>
        ///         One transaction per instance, one flow at a time. Running two of these concurrently on
        ///         the same instance -- <c>Task.WhenAll(db.TransactionAsync(a), db.TransactionAsync(b))</c>
        ///         -- is not supported: the second would see a transaction already in progress and quietly
        ///         join it. <see cref="IDbCommunication"/> is registered per scope, so one instance belongs
        ///         to one unit of work.
        ///     </para>
        /// </summary>
        public virtual Task TransactionAsync(Func<Task> work, CancellationToken cancellationToken = default)
            => this.RunTransactionAsync(work, null, cancellationToken);

        /// <inheritdoc cref="TransactionAsync(Func{Task}, CancellationToken)"/>
        /// <param name="work">The work to run inside the transaction.</param>
        /// <param name="isolationLevel">
        ///     The level to begin at; see <see cref="Transaction(Action, IsolationLevel)"/> for when it
        ///     applies.
        /// </param>
        /// <param name="cancellationToken">Cancels the connection open and the begin.</param>
        public virtual Task TransactionAsync(Func<Task> work, IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
            => this.RunTransactionAsync(work, isolationLevel, cancellationToken);

        private async Task RunTransactionAsync(Func<Task> work, IsolationLevel? isolationLevel, CancellationToken cancellationToken)
        {
            if (work is null)
                throw new ArgumentNullException(nameof(work));

            if (this._transactionStarted)
            {
                await work().ConfigureAwait(false);
                return;
            }

            _transactionStarted = true;

            try
            {
                // conn will be null in-case of _externalConnection is set
                var (conn, tx, wasClosed) = await this.GetTransactionAndConnectionAsync(isolationLevel, cancellationToken).ConfigureAwait(false);
                this._transactionConnection = conn;
                this._transaction = tx;
                try
                {
                    try
                    {
                        await work().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            await this.RollbackTransactionAsync(tx, cancellationToken).ConfigureAwait(false);
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
                            await this.RollbackTransactionAsync(tx, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            throw new AggregateException(poisonExp, ex);
                        }
                        throw poisonExp;
                    }

                    await this.CommitTransactionAsync(tx, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
                    if (tx != null)
                    {
                        try { await tx.DisposeAsync().ConfigureAwait(false); } catch { /*don't worry about it*/ }
                    }
                    // conn will be null in-case if _externalConnection is set.
                    if (conn != null)
                    {
                        try { await conn.DisposeAsync().ConfigureAwait(false); } catch { /*don't worry about it*/ }
                    }
                    if (wasClosed && this._externalConnection != null)
                    {
                        try { await this._externalConnection.CloseAsync().ConfigureAwait(false); } catch { /*don't worry about it*/ }
                    }
#else
                    try { tx?.Dispose(); } catch { /*don't worry about it*/ }
                    try { conn?.Dispose(); } catch { /*don't worry about it*/ }

                    if (wasClosed && this._externalConnection != null)
                    {
                        try { this._externalConnection.Close(); } catch { /*don't worry about it*/ }
                    }
#endif
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
            // The reference below survives its owner ending the transaction, so this has to be asked
            // separately -- otherwise the savepoint fails somewhere further down with a worse message.
            this.EnsureCallerTransactionIsStillUsable();

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
        ///     The asynchronous <see cref="TransactionWithSavepoint(Action)"/>. Works inside a transaction
        ///     opened by either <see cref="Transaction(Action)"/> or
        ///     <see cref="TransactionAsync(Func{Task}, CancellationToken)"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">There is no surrounding transaction.</exception>
        public virtual async Task TransactionWithSavepointAsync(Func<Task> work, CancellationToken cancellationToken = default)
        {
            if (work is null)
                throw new ArgumentNullException(nameof(work));
            if (!this._transactionStarted)
                throw new InvalidOperationException(
                    $"{nameof(TransactionWithSavepointAsync)} cannot be called without an outer transaction.");
            if (this._transactionPoisoned)
                throw new InvalidOperationException(
                    "The transaction can no longer be used because rolling back to an earlier savepoint failed.");
            // The reference below survives its owner ending the transaction, so this has to be asked
            // separately -- otherwise the savepoint fails somewhere further down with a worse message.
            this.EnsureCallerTransactionIsStillUsable();

            var tx = this._transaction
                     ?? throw new InvalidOperationException(
                         "Savepoint: transaction is no longer available; probably the connection was lost.");

            this._savepointCount++;
            var savepoint = $"at_tran_savepoint_{this._savepointCount}";

            // Outside the try: if creating the savepoint fails there is nothing to roll back to, and
            // rolling back to a name the server never saw would mask the real failure.
            await this.CreateSavepointAsync(savepoint, cancellationToken).ConfigureAwait(false);

            try
            {
                await work().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                try
                {
                    await this.RollbackToSavepointAsync(savepoint, cancellationToken).ConfigureAwait(false);
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

            await this.ReleaseSavepointAsync(savepoint, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        ///     Creates a savepoint with the given name inside the current transaction. The name is
        ///     generated by <see cref="TransactionWithSavepoint(Action)"/>; implementations may use
        ///     <see cref="GetCurrentTransaction"/> or issue provider-specific SQL.
        /// </summary>
        protected abstract void CreateSavepoint(string savepoint);

        /// <summary>The asynchronous <see cref="CreateSavepoint(string)"/>.</summary>
        protected abstract Task CreateSavepointAsync(string savepoint, CancellationToken cancellationToken);

        /// <summary>
        ///     The asynchronous <see cref="RollbackToSavepoint(string)"/>. The same note applies:
        ///     <see cref="ReleaseSavepointAsync"/> is not called afterwards.
        /// </summary>
        protected abstract Task RollbackToSavepointAsync(string savepoint, CancellationToken cancellationToken);

        /// <summary>The asynchronous <see cref="ReleaseSavepoint(string)"/>; does nothing by default.</summary>
        protected virtual Task ReleaseSavepointAsync(string savepoint, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

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
        protected virtual (DbConnection, DbTransaction, bool) GetTransactionAndConnection(IsolationLevel? isolationLevel)
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
                    transaction1 = BeginTransaction(this._externalConnection, isolationLevel);
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
                transaction = BeginTransaction(transactionConnection, isolationLevel);
            }
            catch
            {
                try { transaction?.Dispose(); } catch { /*don't worry*/ }
                try { transactionConnection?.Dispose(); } catch { /*don't worry*/ }
                throw;
            }

            return (transactionConnection, transaction, false);
        }

        /// <summary>
        ///     <c>BeginTransaction()</c> and <c>BeginTransaction(IsolationLevel)</c> are separate overloads
        ///     rather than one with a default, and there is no value meaning "the provider's own default" --
        ///     <see cref="IsolationLevel.Unspecified"/> is a level a provider may reject, not an absence.
        ///     So the choice has to be made by calling one or the other.
        /// </summary>
        private static DbTransaction BeginTransaction(DbConnection connection, IsolationLevel? isolationLevel)
            => isolationLevel.HasValue
                    ? connection.BeginTransaction(isolationLevel.Value)
                    : connection.BeginTransaction();

        protected virtual void CommitTransaction(DbTransaction tx)
        {
            if (tx is null)
                throw new ArgumentNullException(nameof(tx));

            tx.Commit();
        }

        protected virtual void RollbackTransaction(DbTransaction tx)
        {
            if (tx is null)
                throw new ArgumentNullException(nameof(tx));

            tx.Rollback();
        }

        /// <summary>
        ///     The asynchronous <see cref="GetTransactionAndConnection"/>. Only the connection open is
        ///     genuinely asynchronous on every target; see <see cref="BeginTransactionAsync"/>.
        /// </summary>
        protected virtual async Task<(DbConnection, DbTransaction, bool)> GetTransactionAndConnectionAsync(IsolationLevel? isolationLevel, CancellationToken cancellationToken)
        {
            if (this._externalConnection != null)
            {
                DbTransaction transaction1;

                var wasClosed = false;
                if (this._externalConnection.State != ConnectionState.Open)
                {
                    wasClosed = true;
                    await this._externalConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
                }
                try
                {
                    transaction1 = await BeginTransactionAsync(this._externalConnection, isolationLevel, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (wasClosed)
                    {
                        try
                        {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
                            await this._externalConnection.CloseAsync().ConfigureAwait(false);
#else
                            this._externalConnection.Close();
#endif
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
                await transactionConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
                transaction = await BeginTransactionAsync(transactionConnection, isolationLevel, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                try { transaction?.Dispose(); } catch { /*don't worry*/ }
                try { transactionConnection?.Dispose(); } catch { /*don't worry*/ }
                throw;
            }

            return (transactionConnection, transaction, false);
        }

        /// <summary>
        ///     <c>DbConnection.BeginTransactionAsync</c> only exists on .NET 6 and later. Everywhere else
        ///     the begin is synchronous -- which costs nothing in practice, since the statement is not sent
        ///     to the server until the first command runs under it.
        /// </summary>
        private static async Task<DbTransaction> BeginTransactionAsync(DbConnection connection, IsolationLevel? isolationLevel, CancellationToken cancellationToken)
        {
#if NET6_0_OR_GREATER
            return isolationLevel.HasValue
                    ? await connection.BeginTransactionAsync(isolationLevel.Value, cancellationToken).ConfigureAwait(false)
                    : await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
#else
            await Task.CompletedTask.ConfigureAwait(false);
            return BeginTransaction(connection, isolationLevel);
#endif
        }

        /// <summary>The asynchronous <see cref="CommitTransaction(DbTransaction)"/>.</summary>
        protected virtual async Task CommitTransactionAsync(DbTransaction tx, CancellationToken cancellationToken)
        {
            if (tx is null)
                throw new ArgumentNullException(nameof(tx));

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
#else
            await Task.CompletedTask.ConfigureAwait(false);
            tx.Commit();
#endif
        }

        /// <summary>The asynchronous <see cref="RollbackTransaction(DbTransaction)"/>.</summary>
        protected virtual async Task RollbackTransactionAsync(DbTransaction tx, CancellationToken cancellationToken)
        {
            if (tx is null)
                throw new ArgumentNullException(nameof(tx));

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
#else
            await Task.CompletedTask.ConfigureAwait(false);
            tx.Rollback();
#endif
        }
    }
}
