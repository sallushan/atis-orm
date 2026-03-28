using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Text;

namespace Atis.Orm.SqlServer
{
    public class SqlDbParameterFactory : IDbParameterFactory
    {
        public DbParameter CreateDbParameter(IQueryParameter queryParameter, object parameterValue)
        {
            var paramValue = queryParameter.IsLiteral ? queryParameter.InitialValue : parameterValue;
            return new SqlParameter(queryParameter.Name, paramValue ?? DBNull.Value);
        }
    }
}
