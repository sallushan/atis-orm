using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace Atis.Orm
{
    public class CompiledQuery : ICompiledQuery
    {
        private readonly string sql;
        private readonly IReadOnlyList<IQueryParameter> queryParameters;
        private readonly IDbParameterFactory dbParameterFactory;
        private readonly bool isNonQuery;
        private readonly Func<IDataReader, object> elementFactory;

        public CompiledQuery(string sql, IReadOnlyList<IQueryParameter> queryParameters, IDbParameterFactory dbParameterFactory, bool isNonQuery, Func<IDataReader, object> elementFactory)
        {
            this.sql = sql;
            this.queryParameters = queryParameters;
            this.dbParameterFactory = dbParameterFactory;
            this.isNonQuery = isNonQuery;
            this.elementFactory = elementFactory;
        }

        public IExecutionContext GetExecutionContext(IReadOnlyList<object> parameterValues, bool useInitialValues)
        {
            var dbParameters = new DbParameter[queryParameters.Count];
            for (int i = 0; i < queryParameters.Count; i++)
            {
                var queryParameter = queryParameters[i];
                var parameterValue = useInitialValues ? queryParameter.InitialValue : parameterValues[i];
                var dbParameter = dbParameterFactory.CreateDbParameter(queryParameter, parameterValue);
                dbParameters[i] = dbParameter;
            }
            return new ExecutionContext(sql, dbParameters, isNonQuery, elementFactory);
        }
    }
}
