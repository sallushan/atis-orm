using Atis.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

using Atis.Orm.Abstractions;
using Atis.Orm.DataAccess;
namespace Atis.Orm.SqlServer
{
    public class SqlServerExtension : IServiceContextExtension
    {
        private readonly string _connectionString;

        public SqlServerExtension(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public void AddServices(IServiceCollection services)
        {
            var builder = new OrmServiceBuilder(services);
            builder.TryAdd<IDbCommunication>(sp => new SqlDbCommunication(_connectionString));
            builder.TryAdd<IDbParameterFactory, SqlDbParameterFactory>();
            builder.AddCoreServices();
        }
    }
}
