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

        protected override DbConnection CreateConnection()
        {
            var connection = SqlServerClientFactory.Create(this._providerFactory, f => f.CreateConnection(), "DbConnection");
            connection.ConnectionString = this.ConnectionString;
            return connection;
        }
    }
}
