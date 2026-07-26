using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

using Atis.Orm.DataAccess;
namespace Atis.Orm.SqlServer
{
    public static class SqlServerDataContextConfigurationExtensions
    {
        /// <summary>Points the context at <paramref name="connectionString"/>.</summary>
        public static DataContextConfiguration UseSqlServer(
            this DataContextConfiguration config,
            string connectionString)
        {
            return config.UseSqlServer(connectionString, commandTimeout: null);
        }

        /// <summary>
        ///     Points the context at <paramref name="connectionString"/>, applying
        ///     <paramref name="commandTimeout"/> (seconds) to every command it issues.
        /// </summary>
        public static DataContextConfiguration UseSqlServer(
            this DataContextConfiguration config,
            string connectionString,
            int? commandTimeout)
        {
            if (config is null)
                throw new ArgumentNullException(nameof(config));

            config.AddOrUpdateExtension(new SqlServerExtension(connectionString, commandTimeout));
            return config;
        }

        /// <summary>
        ///     <para>
        ///         Points the context at <paramref name="connectionString"/> using a specific ADO.NET client
        ///         — pass <c>Microsoft.Data.SqlClient.SqlClientFactory.Instance</c> or
        ///         <c>System.Data.SqlClient.SqlClientFactory.Instance</c>.
        ///     </para>
        ///     <para>
        ///         Without this, the client is the one this assembly was built against for the consuming
        ///         target framework: <c>Microsoft.Data.SqlClient</c> on <c>net8.0</c>,
        ///         <c>System.Data.SqlClient</c> on <c>netstandard2.0</c>.
        ///     </para>
        /// </summary>
        public static DataContextConfiguration UseSqlServer(
            this DataContextConfiguration config,
            string connectionString,
            DbProviderFactory providerFactory,
            int? commandTimeout = null)
        {
            if (config is null)
                throw new ArgumentNullException(nameof(config));

            config.AddOrUpdateExtension(new SqlServerExtension(connectionString, providerFactory, commandTimeout));
            return config;
        }

        /// <summary>
        ///     Points the context at a connection the caller owns. The context opens it when needed but never
        ///     disposes it, which also lets the caller supply a connection from either ADO.NET SQL Server
        ///     client — the client is inferred from <paramref name="connection"/>.
        /// </summary>
        public static DataContextConfiguration UseSqlServer(
            this DataContextConfiguration config,
            DbConnection connection,
            int? commandTimeout = null,
            DbProviderFactory providerFactory = null)
        {
            if (config is null)
                throw new ArgumentNullException(nameof(config));

            config.AddOrUpdateExtension(new SqlServerExtension(connection, commandTimeout, providerFactory));
            return config;
        }
    }
}
