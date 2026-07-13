using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

using Atis.Orm.Abstractions;
using Atis.Orm.DataAccess;
namespace Atis.Orm.Querying
{
    public class CompiledQuery : ICompiledQuery
    {
        private readonly string sql;
        private readonly IReadOnlyList<IQueryParameter> queryParameters;
        private readonly IDbParameterFactory dbParameterFactory;
        private readonly bool isNonQuery;
        private readonly Func<IDataReader, object> elementFactory;
        
        public CompiledQuery(string sql, IReadOnlyList<IQueryParameter> queryParameters, IDbParameterFactory dbParameterFactory, bool isNonQuery, Func<IDataReader, object> elementFactory, bool isPreprocessingRequired)
        {
            this.sql = sql;
            this.queryParameters = queryParameters;
            this.dbParameterFactory = dbParameterFactory;
            this.isNonQuery = isNonQuery;
            this.elementFactory = elementFactory;
            this.IsPreprocessingRequired = isPreprocessingRequired;
        }

        public bool IsPreprocessingRequired { get; }

        public IExecutionContext GetExecutionContext(IReadOnlyList<object> parameterValues, bool useInitialValues)
        {
            // parameterValues holds only the re-extracted values of the non-literal (variable) parameters, in order.
            // Literal parameters keep their translation-time InitialValue and do not consume a slot, so we walk the
            // parameter template with a running index that advances only for non-literal parameters.
            var dbParameters = new DbParameter[queryParameters.Count];
            int variableIndex = 0;
            for (int i = 0; i < queryParameters.Count; i++)
            {
                var queryParameter = queryParameters[i];
                object parameterValue;
                if (queryParameter.IsLiteral || useInitialValues)
                    parameterValue = queryParameter.InitialValue;
                else
                    parameterValue = parameterValues[variableIndex++];
                var dbParameter = dbParameterFactory.CreateDbParameter(queryParameter, parameterValue);
                dbParameters[i] = dbParameter;
            }
            return new ExecutionContext(sql, dbParameters, isNonQuery, elementFactory);
        }
    }
}
