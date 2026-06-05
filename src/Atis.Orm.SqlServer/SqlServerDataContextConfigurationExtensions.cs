using System;
using System.Collections.Generic;
using System.Text;

namespace Atis.Orm.SqlServer
{
    public static class SqlServerDataContextConfigurationExtensions
    {
        public static DataContextConfiguration UseSqlServer(
            this DataContextConfiguration config,
            string connectionString)
        {
            config.AddOrUpdateExtension(new SqlServerExtension(connectionString));
            return config;
        }
    }
}
