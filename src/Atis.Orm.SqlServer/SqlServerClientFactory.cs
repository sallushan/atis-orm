using System;
using System.Data.Common;
using System.Reflection;

namespace Atis.Orm.SqlServer
{
    /// <summary>
    ///     <para>
    ///         Locates the <see cref="DbProviderFactory"/> that creates connections, commands and parameters.
    ///         Everything else in this assembly talks only to <c>System.Data.Common</c>, so the choice of
    ///         ADO.NET client is confined to this one type.
    ///     </para>
    ///     <para>
    ///         The compile-time default differs per target framework only because
    ///         <c>Microsoft.Data.SqlClient</c> 6.x ships no <c>netstandard2.0</c> asset. Either leg can be
    ///         overridden with <c>UseSqlServer(connectionString, DbProviderFactory)</c>.
    ///     </para>
    /// </summary>
    public static class SqlServerClientFactory
    {
        /// <summary>
        ///     The client used when the caller does not supply one: <c>Microsoft.Data.SqlClient</c> on
        ///     <c>net8.0</c>, <c>System.Data.SqlClient</c> on <c>netstandard2.0</c>.
        /// </summary>
        public static DbProviderFactory Default =>
#if NET8_0_OR_GREATER
            Microsoft.Data.SqlClient.SqlClientFactory.Instance;
#else
// The netstandard2.0 leg has no supported alternative; the caller can still pass a Microsoft.Data.SqlClient
// factory explicitly.
#pragma warning disable CS0618
            System.Data.SqlClient.SqlClientFactory.Instance;
#pragma warning restore CS0618
#endif

        /// <summary>
        ///     <para>
        ///         Determines which client produced <paramref name="connection"/>, so that commands and
        ///         parameters created alongside it come from the same provider. A
        ///         <c>Microsoft.Data.SqlClient.SqlCommand</c> will not accept a
        ///         <c>System.Data.SqlClient.SqlParameter</c>, so guessing here is not an option.
        ///     </para>
        /// </summary>
        /// <exception cref="InvalidOperationException">
        ///     The connection's provider could not be identified. Pass the factory explicitly.
        /// </exception>
        public static DbProviderFactory ForConnection(DbConnection connection)
        {
            if (connection is null)
                throw new ArgumentNullException(nameof(connection));

            var factory = TryGetProviderFactory(connection);
            if (factory != null)
                return factory;

            throw new InvalidOperationException(
                $"Could not determine the DbProviderFactory behind '{connection.GetType().AssemblyQualifiedName}'. " +
                $"Use the UseSqlServer overload that takes a DbProviderFactory to supply it explicitly.");
        }

        private static DbProviderFactory TryGetProviderFactory(DbConnection connection)
        {
#if NET8_0_OR_GREATER
            try
            {
                return DbProviderFactories.GetFactory(connection);
            }
            catch (ArgumentException)
            {
                // Provider not registered with DbProviderFactories; fall through to the conventional lookup.
            }
#endif
            // Every ADO.NET SQL Server client places SqlClientFactory beside SqlConnection in the same
            // namespace and assembly, and exposes it as a static Instance field.
            var connectionType = connection.GetType();
            var factoryType = connectionType.Assembly.GetType($"{connectionType.Namespace}.SqlClientFactory", throwOnError: false);
            var instanceField = factoryType?.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
            return instanceField?.GetValue(null) as DbProviderFactory;
        }

        /// <summary>
        ///     Creates an object of type <typeparamref name="T"/> from <paramref name="providerFactory"/>,
        ///     turning the <c>null</c> that <see cref="DbProviderFactory"/> is allowed to return into a
        ///     diagnosable failure.
        /// </summary>
        internal static T Create<T>(DbProviderFactory providerFactory, Func<DbProviderFactory, T> create, string what)
            where T : class
        {
            return create(providerFactory)
                   ?? throw new InvalidOperationException(
                       $"'{providerFactory.GetType().FullName}' did not produce a {what}.");
        }
    }
}
