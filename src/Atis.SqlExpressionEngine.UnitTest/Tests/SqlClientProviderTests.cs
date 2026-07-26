using Atis.Orm;
using Atis.Orm.Abstractions;
using Atis.Orm.DataAccess;
using Atis.Orm.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Data.Common;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     Covers the ADO.NET client abstraction: Atis.Orm.SqlServer creates connections, commands and
    ///     parameters through <see cref="DbProviderFactory"/>, so it drives either System.Data.SqlClient or
    ///     Microsoft.Data.SqlClient. On net8.0 the default is Microsoft.Data.SqlClient.
    /// </summary>
    [TestClass]
    public class SqlClientProviderTests
    {
        private const string TestConnectionString = "Server=.;Database=ProviderDb;Integrated Security=true;Encrypt=True;TrustServerCertificate=True";

        [TestMethod]
        public void Default_OnNet8_IsMicrosoftDataSqlClient()
        {
            Assert.AreSame(SqlClientFactory.Instance, SqlServerClientFactory.Default);
        }

        [TestMethod]
        public void ForConnection_InfersTheClientBehindTheConnection()
        {
            using var connection = new SqlConnection(TestConnectionString);

            Assert.AreSame(SqlClientFactory.Instance, SqlServerClientFactory.ForConnection(connection));
        }

        /// <summary>
        ///     The whole point of the abstraction: a command and the parameters bound into it must come from
        ///     one client. Microsoft.Data.SqlClient.SqlCommand rejects a System.Data.SqlClient.SqlParameter.
        /// </summary>
        [TestMethod]
        public void CommandAndParameters_ComeFromTheSameClient()
        {
            var communication = new SqlDbCommunication(TestConnectionString, null, SqlClientFactory.Instance);
            var parameterFactory = new SqlDbParameterFactory(new SqlDbParameterNameGenerator(), SqlClientFactory.Instance);

            var parameter = parameterFactory.CreateDbParameter(0, null, 42);

            Assert.IsInstanceOfType(parameter, typeof(SqlParameter));
            Assert.AreEqual("@p0", parameter.ParameterName);
            Assert.AreEqual(42, parameter.Value);
            Assert.AreSame(SqlClientFactory.Instance, communication.ProviderFactory);
        }

        [TestMethod]
        public void ParameterFactory_MapsNullToDBNull()
        {
            var parameterFactory = new SqlDbParameterFactory(new SqlDbParameterNameGenerator(), SqlClientFactory.Instance);

            Assert.AreEqual(DBNull.Value, parameterFactory.CreateDbParameter(0, null, null).Value);

            var expanded = parameterFactory.CreateDbParameters(1, null, new object[] { "a", null });
            Assert.AreEqual(2, expanded.Count);
            Assert.AreEqual("@p1_1", expanded[0].ParameterName);
            Assert.AreEqual("a", expanded[0].Value);
            Assert.AreEqual("@p1_2", expanded[1].ParameterName);
            Assert.AreEqual(DBNull.Value, expanded[1].Value);
        }

        [TestMethod]
        public void UseSqlServer_WithAConnection_InfersItsClient()
        {
            using var connection = new SqlConnection(TestConnectionString);
            var config = new DataContextConfiguration();
            config.UseSqlServer(connection);

            var extension = config.GetRequiredExtension<SqlServerExtension>();

            Assert.AreSame(connection, extension.Connection);
            Assert.AreSame(SqlClientFactory.Instance, extension.ProviderFactory);
        }

        [TestMethod]
        public void UseSqlServer_WithAnExplicitFactory_PinsIt()
        {
            var pinned = new FakeProviderFactory();
            var config = new DataContextConfiguration();
            config.UseSqlServer(TestConnectionString, pinned, commandTimeout: 45);

            var extension = config.GetRequiredExtension<SqlServerExtension>();

            Assert.AreSame(pinned, extension.ProviderFactory);
            Assert.AreEqual(45, extension.CommandTimeout);
        }

        /// <summary>
        ///     IDbParameterFactory is a singleton captured by compiled queries in the process-wide cache, so
        ///     the client cannot be swapped per scope the way a connection string can. Configurations that
        ///     differ by client must therefore resolve to different root providers.
        /// </summary>
        [TestMethod]
        public void DifferentClients_DoNotShareARootServiceProvider()
        {
            var config1 = new DataContextConfiguration();
            config1.UseSqlServer(TestConnectionString, SqlClientFactory.Instance);

            var config2 = new DataContextConfiguration();
            config2.UseSqlServer(TestConnectionString, new FakeProviderFactory());

            var sp1 = OrmServiceManager.Instance.GetOrAdd(config1);
            var sp2 = OrmServiceManager.Instance.GetOrAdd(config2);

            Assert.IsFalse(ReferenceEquals(sp1, sp2),
                "Two configurations on different ADO.NET clients must not share singletons.");
        }

        [TestMethod]
        public void SameClient_SharesARootServiceProvider()
        {
            var config1 = new DataContextConfiguration();
            config1.UseSqlServer("Server=.;Database=A;Integrated Security=true", SqlClientFactory.Instance);

            var config2 = new DataContextConfiguration();
            config2.UseSqlServer("Server=.;Database=B;Integrated Security=true", SqlClientFactory.Instance);

            var sp1 = OrmServiceManager.Instance.GetOrAdd(config1);
            var sp2 = OrmServiceManager.Instance.GetOrAdd(config2);

            Assert.IsTrue(ReferenceEquals(sp1, sp2),
                "Only the client splits the cache — the connection string must not.");
        }

        /// <summary>
        ///     End to end: the client configured on a context reaches the singleton IDbParameterFactory and
        ///     the scoped IDbCommunication resolved from that context.
        /// </summary>
        [TestMethod]
        public void ConfiguredClient_ReachesBothTheSingletonAndTheScopedService()
        {
            var pinned = new FakeProviderFactory();
            using var context = new ProviderTestContext(pinned);

            var parameter = context.GetParameterFactory().CreateDbParameter(0, null, "x");

            Assert.AreSame(pinned, ((SqlDbCommunication)context.GetDbCommunication()).ProviderFactory);
            Assert.IsInstanceOfType(parameter, typeof(FakeParameter));
        }

        private sealed class ProviderTestContext : Atis.Orm.DataContext
        {
            private readonly DbProviderFactory _providerFactory;

            public ProviderTestContext(DbProviderFactory providerFactory)
            {
                _providerFactory = providerFactory;
            }

            protected override void OnConfiguring(DataContextConfiguration config)
                => config.UseSqlServer(TestConnectionString, _providerFactory);

            public IDbCommunication GetDbCommunication() => this.ServiceProvider.GetRequiredService<IDbCommunication>();

            public IDbParameterFactory GetParameterFactory() => this.ServiceProvider.GetRequiredService<IDbParameterFactory>();
        }

        /// <summary>
        ///     Stands in for "some other ADO.NET client". Its parameters are a distinct type, which is what
        ///     lets the tests above prove the configured client is the one actually used.
        /// </summary>
        private sealed class FakeProviderFactory : DbProviderFactory
        {
            public override DbConnection CreateConnection() => new SqlConnection();
            public override DbCommand CreateCommand() => new SqlCommand();
            public override DbParameter CreateParameter() => new FakeParameter();
        }

        private sealed class FakeParameter : DbParameter
        {
            public override DbType DbType { get; set; }
            public override ParameterDirection Direction { get; set; }
            public override bool IsNullable { get; set; }
            public override string ParameterName { get; set; }
            public override string SourceColumn { get; set; }
            public override bool SourceColumnNullMapping { get; set; }
            public override int Size { get; set; }
            public override object Value { get; set; }
            public override void ResetDbType() { }
        }
    }
}
