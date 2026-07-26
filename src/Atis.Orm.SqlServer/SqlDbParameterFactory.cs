using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

using Atis.Orm.Abstractions;
namespace Atis.Orm.SqlServer
{
    /// <summary>
    ///     <para>
    ///         Creates the <see cref="DbParameter"/>s bound into a rendered command. Parameters come from the
    ///         same <see cref="DbProviderFactory"/> as the command they are added to — a
    ///         <c>Microsoft.Data.SqlClient.SqlCommand</c> rejects a <c>System.Data.SqlClient.SqlParameter</c>
    ///         and vice versa.
    ///     </para>
    /// </summary>
    public class SqlDbParameterFactory : IDbParameterFactory
    {
        private readonly IDbParameterNameGenerator _parameterNameGenerator;
        private readonly DbProviderFactory _providerFactory;

        public SqlDbParameterFactory(IDbParameterNameGenerator parameterNameGenerator)
            : this(parameterNameGenerator, null)
        {
        }

        public SqlDbParameterFactory(IDbParameterNameGenerator parameterNameGenerator, DbProviderFactory providerFactory)
        {
            _parameterNameGenerator = parameterNameGenerator ?? throw new ArgumentNullException(nameof(parameterNameGenerator));
            _providerFactory = providerFactory ?? SqlServerClientFactory.Default;
        }

        // parameterValue is already resolved by the renderer (literals carry their InitialValue), so it is
        // used as-is here.
        public DbParameter CreateDbParameter(int parameterIndex, IQueryParameter queryParameter, object parameterValue)
        {
            var parameterName = this._parameterNameGenerator.GenerateParameterName(parameterIndex);
            return this.CreateDbParameter(parameterName, parameterValue);
        }

        public IReadOnlyList<DbParameter> CreateDbParameters(int parameterIndex, IQueryParameter queryParameter, IEnumerable parameterValue)
        {
            var dbParameterList = new List<DbParameter>();
            int subIndex = 1;
            foreach (var individualValue in parameterValue)
            {
                var parameterName = this._parameterNameGenerator.GenerateSubParameterName(parameterIndex, subIndex);
                dbParameterList.Add(this.CreateDbParameter(parameterName, individualValue));
                subIndex++;
            }
            return dbParameterList;
        }

        private DbParameter CreateDbParameter(string parameterName, object value)
        {
            var dbParameter = SqlServerClientFactory.Create(this._providerFactory, f => f.CreateParameter(), "DbParameter");
            dbParameter.ParameterName = parameterName;
            dbParameter.Value = value ?? DBNull.Value;
            return dbParameter;
        }
    }
}
