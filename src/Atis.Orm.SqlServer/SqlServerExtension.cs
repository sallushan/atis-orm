using Atis.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

using Atis.Orm.Abstractions;
using Atis.Orm.DataAccess;
using Atis.Orm.Translation;
namespace Atis.Orm.SqlServer
{
    /// <summary>
    ///     <para>
    ///         Registers the SQL Server services and carries the per-context connection options.
    ///     </para>
    ///     <para>
    ///         The options live on the extension instance, not in the registrations. The root
    ///         <see cref="IServiceProvider"/> produced by <see cref="AddServices"/> is cached process-wide by
    ///         configuration type + extension types, so it is shared by contexts pointed at different
    ///         databases; a factory delegate that captured <c>this</c> would hand the first context's
    ///         connection string to all of them. Each delegate below therefore resolves the extension
    ///         belonging to the scope it is being resolved into, via
    ///         <see cref="DataContextServiceExtensions.GetContextExtension{TExtension}(IServiceProvider)"/>.
    ///     </para>
    ///     <para>
    ///         The one exception is <see cref="ProviderFactory"/>, which a singleton depends on and which is
    ///         therefore folded into the cache key — see <see cref="GetServiceProviderCacheKey"/>.
    ///     </para>
    /// </summary>
    public class SqlServerExtension : IServiceContextExtension, IServiceProviderCacheKeyContributor
    {
        /// <summary>Configures the context against <paramref name="connectionString"/>.</summary>
        public SqlServerExtension(string connectionString)
            : this(connectionString, null, null, null)
        {
        }

        /// <summary>
        ///     Configures the context against <paramref name="connectionString"/>, applying
        ///     <paramref name="commandTimeout"/> (seconds) to every command it issues.
        /// </summary>
        public SqlServerExtension(string connectionString, int? commandTimeout)
            : this(connectionString, commandTimeout, null, null)
        {
        }

        /// <summary>
        ///     Configures the context against <paramref name="connectionString"/> using a specific ADO.NET
        ///     client, e.g. <c>Microsoft.Data.SqlClient.SqlClientFactory.Instance</c>.
        /// </summary>
        public SqlServerExtension(string connectionString, DbProviderFactory providerFactory, int? commandTimeout = null)
            : this(connectionString, commandTimeout, null, providerFactory ?? throw new ArgumentNullException(nameof(providerFactory)))
        {
        }

        /// <summary>
        ///     Configures the context against a connection the caller owns. The context opens it when needed
        ///     but never disposes it. The client is inferred from the connection unless
        ///     <paramref name="providerFactory"/> says otherwise.
        /// </summary>
        public SqlServerExtension(DbConnection connection, int? commandTimeout = null, DbProviderFactory providerFactory = null)
            : this(null, commandTimeout, connection ?? throw new ArgumentNullException(nameof(connection)), providerFactory)
        {
        }

        private SqlServerExtension(string connectionString, int? commandTimeout, DbConnection connection, DbProviderFactory providerFactory)
        {
            if (connection is null && string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("A connection string is required.", nameof(connectionString));
            if (commandTimeout.HasValue && commandTimeout.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(commandTimeout), "Command timeout cannot be negative.");

            this.ConnectionString = connectionString;
            this.CommandTimeout = commandTimeout;
            this.Connection = connection;
            this.ProviderFactory = providerFactory
                                   ?? (connection != null
                                           ? SqlServerClientFactory.ForConnection(connection)
                                           : SqlServerClientFactory.Default);
        }

        /// <summary>The connection string, or <c>null</c> when <see cref="Connection"/> was supplied.</summary>
        public string ConnectionString { get; }

        /// <summary>The command timeout in seconds, or <c>null</c> to use the provider default.</summary>
        public int? CommandTimeout { get; }

        /// <summary>A caller-owned connection to use instead of opening one per query, or <c>null</c>.</summary>
        public DbConnection Connection { get; }

        /// <summary>
        ///     The ADO.NET client every connection, command and parameter is created from. Defaults to
        ///     <see cref="SqlServerClientFactory.Default"/>, or to the client behind <see cref="Connection"/>.
        /// </summary>
        public DbProviderFactory ProviderFactory { get; }

        /// <inheritdoc />
        public void AddServices(IServiceCollection services)
        {
            var builder = new OrmServiceBuilder(services);

            // IDbParameterFactory is a singleton and is captured by compiled queries in the process-wide
            // query cache, so its client cannot be swapped per context. Capturing it here is sound only
            // because GetServiceProviderCacheKey() puts it in the cache key: every context sharing this
            // provider is guaranteed to have configured the very same factory.
            var providerFactory = this.ProviderFactory;
            builder.TryAdd<IDbParameterFactory>(sp => new SqlDbParameterFactory(
                sp.GetRequiredService<IDbParameterNameGenerator>(), providerFactory));

            // Non-capturing: connection string, timeout and connection are per-context, so they are read
            // from the resolving scope's own configuration rather than from `this`.
            builder.TryAdd<IDbCommunication>(sp => CreateDbCommunication(sp));

            builder.TryAdd<IDbParameterNameGenerator, SqlDbParameterNameGenerator>();
            // Register the SQL Server dialect translator before AddCoreServices so it wins the
            // TryAdd<ISqlExpressionTranslator, SqlExpressionTranslatorBase>() default (first-wins).
            builder.TryAdd<ISqlExpressionTranslator, SqlServerSqlExpressionTranslator>();
            builder.AddCoreServices();
        }

        /// <summary>
        ///     <para>
        ///         Splits the provider cache by ADO.NET client. Two contexts on different clients must not
        ///         share singletons: a <c>Microsoft.Data.SqlClient.SqlCommand</c> will not accept a
        ///         <c>System.Data.SqlClient.SqlParameter</c>, and the compiled-query cache hands out
        ///         parameters built by whichever <see cref="IDbParameterFactory"/> that provider was built
        ///         with.
        ///     </para>
        ///     <para>
        ///         The factory <em>instance</em> is the key, not its type, so a scoped
        ///         <see cref="IDbCommunication"/> reading <see cref="ProviderFactory"/> from its own
        ///         configuration can never disagree with the captured singleton. In practice this does not
        ///         fragment the cache: <c>SqlClientFactory.Instance</c> is itself a singleton.
        ///     </para>
        /// </summary>
        public object GetServiceProviderCacheKey() => this.ProviderFactory;

        private static IDbCommunication CreateDbCommunication(IServiceProvider serviceProvider)
        {
            var options = serviceProvider.GetContextExtension<SqlServerExtension>();
            return options.Connection != null
                ? new SqlDbCommunication(options.Connection, options.CommandTimeout, options.ProviderFactory)
                : new SqlDbCommunication(options.ConnectionString, options.CommandTimeout, options.ProviderFactory);
        }
    }
}
