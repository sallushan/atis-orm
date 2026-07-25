using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Text;

using Atis.Orm.Abstractions;
namespace Atis.Orm.SqlServer
{
    public class SqlDbParameterFactory : IDbParameterFactory
    {
        private readonly IDbParameterNameGenerator _parameterNameGenerator;

        public SqlDbParameterFactory(IDbParameterNameGenerator parameterNameGenerator)
        {
            _parameterNameGenerator = parameterNameGenerator ?? throw new ArgumentNullException(nameof(parameterNameGenerator));
        }

        // parameterValue is already resolved by the renderer (literals carry their InitialValue), so it is
        // used as-is here.
        public DbParameter CreateDbParameter(int parameterIndex, IQueryParameter queryParameter, object parameterValue)
        {
            var parameterName = this._parameterNameGenerator.GenerateParameterName(parameterIndex);
            return new SqlParameter(parameterName, parameterValue ?? DBNull.Value);
        }

        public IReadOnlyList<DbParameter> CreateDbParameters(int parameterIndex, IQueryParameter queryParameter, IEnumerable parameterValue)
        {
            var dbParameterList = new List<DbParameter>();
            int subIndex = 1;
            foreach (var individualValue in parameterValue)
            {
                var parameterName = this._parameterNameGenerator.GenerateSubParameterName(parameterIndex, subIndex);
                var dbParameter = new SqlParameter(parameterName, individualValue ?? DBNull.Value);
                dbParameterList.Add(dbParameter);
                subIndex++;
            }
            return dbParameterList;
        }
    }
}
