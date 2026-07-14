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

        public IExecutionContext GetExecutionContext(IReadOnlyDictionary<string, object> parameterValuesByIdentity, bool useInitialValues)
        {
            // parameterValuesByIdentity holds the re-extracted values of the non-literal (variable) parameters,
            // keyed by the source variable's stable identity. Rebinding is a lookup by identity rather than by
            // position: the translator's parameter order can differ from LINQ visit order after SqlExpression
            // reshaping (CTE hoisting, subtree copying), and one variable can back several parameters. Literal
            // parameters keep their translation-time InitialValue.
            var dbParameters = new DbParameter[queryParameters.Count];
            for (int i = 0; i < queryParameters.Count; i++)
            {
                var queryParameter = queryParameters[i];
                object parameterValue;
                if (queryParameter.IsLiteral || useInitialValues)
                {
                    parameterValue = queryParameter.InitialValue;
                }
                else if (parameterValuesByIdentity != null
                         && queryParameter.ParameterIdentity != null
                         && parameterValuesByIdentity.TryGetValue(queryParameter.ParameterIdentity, out var reboundValue))
                {
                    parameterValue = reboundValue;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Could not rebind parameter '{queryParameter.Name}' (identity '{queryParameter.ParameterIdentity}') " +
                        $"on a cache hit: no re-extracted value matched its identity.");
                }
                var dbParameter = dbParameterFactory.CreateDbParameter(queryParameter, parameterValue);
                dbParameters[i] = dbParameter;
            }
            return new ExecutionContext(sql, dbParameters, isNonQuery, elementFactory);
        }
    }
}
