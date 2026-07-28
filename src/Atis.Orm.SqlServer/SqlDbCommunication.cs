using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;

using Atis.Orm.DataAccess;
namespace Atis.Orm.SqlServer
{
    /// <summary>
    ///     <para>
    ///         SQL Server connection and command handling. Everything goes through
    ///         <see cref="DbProviderFactory"/> and <c>System.Data.Common</c>, so the same code drives either
    ///         <c>System.Data.SqlClient</c> or <c>Microsoft.Data.SqlClient</c>.
    ///     </para>
    /// </summary>
    public class SqlDbCommunication : DbCommunicationBase
    {
        private readonly DbProviderFactory _providerFactory;

        public SqlDbCommunication(string connString)
            : this(connString, null, null)
        {
        }

        public SqlDbCommunication(string connString, int? commandTimeout)
            : this(connString, commandTimeout, null)
        {
        }

        public SqlDbCommunication(string connString, int? commandTimeout, DbProviderFactory providerFactory)
            : base(connString, commandTimeout)
        {
            this._providerFactory = providerFactory ?? SqlServerClientFactory.Default;
        }

        public SqlDbCommunication(DbConnection dbConnection)
            : this(dbConnection, null, null)
        {
        }

        public SqlDbCommunication(DbConnection dbConnection, int? commandTimeout)
            : this(dbConnection, commandTimeout, null)
        {
        }

        public SqlDbCommunication(DbConnection dbConnection, int? commandTimeout, DbProviderFactory providerFactory)
            : base(dbConnection, commandTimeout)
        {
            if (dbConnection is null)
                throw new ArgumentNullException(nameof(dbConnection));
            // Commands and parameters must come from the same client as the connection they run on.
            this._providerFactory = providerFactory ?? SqlServerClientFactory.ForConnection(dbConnection);
        }

        /// <summary>The client this instance creates connections, commands and parameters from.</summary>
        public DbProviderFactory ProviderFactory => this._providerFactory;

        protected override DbCommand CreateCommand(string commandText, IEnumerable<DbParameter> dbParameters, CommandType commandType)
        {
            var command = SqlServerClientFactory.Create(this._providerFactory, f => f.CreateCommand(), "DbCommand");
            command.CommandText = commandText;
            command.CommandType = commandType;
            if (this.CommandTimeout.HasValue)
            {
                command.CommandTimeout = this.CommandTimeout.Value;
            }
            if (dbParameters != null)
            {
                foreach (var dbParameter in dbParameters)
                {
                    command.Parameters.Add(dbParameter);
                }
            }
            return command;
        }

        /// <summary>
        ///     <para>
        ///         Issues <c>SAVE TRANSACTION</c>. Deliberately plain T-SQL rather than
        ///         <c>DbTransaction.Save</c>: that overload only exists on .NET 6+, and a cast to a
        ///         concrete <c>SqlTransaction</c> would break the <see cref="DbProviderFactory"/>
        ///         indirection that lets either SqlClient drive this class. The wire cost is identical.
        ///     </para>
        /// </summary>
        protected override void CreateSavepoint(string savepoint)
        {
            this.ExecuteNonQueryCommand("SAVE TRANSACTION " + ValidateSavepointName(savepoint), null, CommandType.Text);
        }

        /// <summary>
        ///     Issues <c>ROLLBACK TRANSACTION &lt;savepoint&gt;</c>, which undoes work back to the
        ///     savepoint without ending the transaction or changing <c>@@TRANCOUNT</c>.
        /// </summary>
        protected override void RollbackToSavepoint(string savepoint)
        {
            this.ExecuteNonQueryCommand("ROLLBACK TRANSACTION " + ValidateSavepointName(savepoint), null, CommandType.Text);
        }

        /// <summary>
        ///     Savepoint names cannot be parameterised the way values can, so the name is concatenated
        ///     into the statement. It is generated internally, but this keeps that guarantee local: a
        ///     derived class overriding <c>TransactionWithSavepoint</c> cannot turn it into an injection
        ///     point. SQL Server caps savepoint names at 32 characters.
        /// </summary>
        private static string ValidateSavepointName(string savepoint)
        {
            if (string.IsNullOrEmpty(savepoint))
                throw new ArgumentException("Savepoint name cannot be null or empty.", nameof(savepoint));
            if (savepoint.Length > 32)
                throw new ArgumentException($"Savepoint name '{savepoint}' exceeds SQL Server's 32 character limit.", nameof(savepoint));
            if (!char.IsLetter(savepoint[0]) && savepoint[0] != '_')
                throw new ArgumentException($"Savepoint name '{savepoint}' must start with a letter or underscore.", nameof(savepoint));
            foreach (var c in savepoint)
            {
                if (!char.IsLetterOrDigit(c) && c != '_')
                    throw new ArgumentException($"Savepoint name '{savepoint}' may only contain letters, digits and underscores.", nameof(savepoint));
            }
            return savepoint;
        }

        protected override DbConnection CreateConnection()
        {
            var connection = SqlServerClientFactory.Create(this._providerFactory, f => f.CreateConnection(), "DbConnection");
            connection.ConnectionString = this.ConnectionString;
            return connection;
        }
    }
}
