using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Text;

namespace Atis.Orm.SqlServer
{
    public class SqlDbCommunication : DbCommunicationBase
    {
        public SqlDbCommunication(string connString) : base(connString)
        {
        }

        public SqlDbCommunication(DbConnection dbConnection) : base(dbConnection)
        {
        }

        public SqlDbCommunication(string connString, int commandTimeout) : base(connString, commandTimeout)
        {
        }

        public override DbCommand CreateCommand(string commandText, IEnumerable<DbParameter> dbParameters, CommandType commandType)
        {
            var sqlCommand = new SqlCommand(commandText);
            sqlCommand.CommandType = commandType;
            if (this.CommandTimeout.HasValue)
            {
                sqlCommand.CommandTimeout = this.CommandTimeout.Value;
            }
            if (dbParameters != null)
            {
                foreach (var dbParameter in dbParameters)
                {
                    sqlCommand.Parameters.Add(dbParameter);
                }
            }
            sqlCommand.Connection = this.GetCurrentConnection() as SqlConnection
                                    ??
                                    throw new InvalidOperationException("Current connection is not a SqlConnection or null");
            return sqlCommand;
        }

        protected override DbConnection CreateConnection()
        {
            var sqlConnection = new SqlConnection(this.ConnectionString);
            return sqlConnection;
        }
    }
}
