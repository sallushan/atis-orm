using Atis.Orm;
using Atis.Orm.SqlServer;
using Atis.SqlExpressionEngine.Abstractions;
using Microsoft.Extensions.DependencyInjection;

using Atis.Orm.Abstractions;
using Atis.Orm.DataAccess;
namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    [TestClass]
    public class DependencyInjectionTests
    {
        [TestMethod]
        public void OrmServiceManager_SameLogicalConfig_ReturnsSameServiceProvider()
        {
            // Arrange — two separate config instances, same logical key
            var config1 = new DataContextConfiguration();
            config1.UseSqlServer("Server=.;Database=Db1;Integrated Security=true;Encrypt=True;TrustServerCertificate=True");
            config1.UseUnitTestCustomization();

            var config2 = new DataContextConfiguration();
            config2.UseSqlServer("Server=.;Database=Db2;Integrated Security=true;Encrypt=True;TrustServerCertificate=True");  // different connection string
            config2.UseUnitTestCustomization();

            // Act
            var sp1 = OrmServiceManager.Instance.GetOrAdd(config1);
            var sp2 = OrmServiceManager.Instance.GetOrAdd(config2);

            // Assert — same root IServiceProvider, cache hit
            Assert.IsTrue(ReferenceEquals(sp1, sp2),
                "Two configs with the same logical key must return the same cached IServiceProvider.");
        }

        [TestMethod]
        public void ServiceLifetimes_AreCorrect()
        {
            // Arrange
            var config = new DataContextConfiguration();
            config.UseSqlServer("Server=.;Database=Db1;Integrated Security=true;Encrypt=True;TrustServerCertificate=True");
            config.UseUnitTestCustomization();

            var rootSp = OrmServiceManager.Instance.GetOrAdd(config);

            using var scope1 = rootSp.GetRequiredService<IServiceScopeFactory>().CreateScope();
            using var scope2 = rootSp.GetRequiredService<IServiceScopeFactory>().CreateScope();

            var sp1 = scope1.ServiceProvider;
            var sp2 = scope2.ServiceProvider;

            // Singleton — same instance across scopes
            var ormModel1 = sp1.GetRequiredService<IOrmModel>();
            var ormModel2 = sp2.GetRequiredService<IOrmModel>();
            Assert.IsTrue(ReferenceEquals(ormModel1, ormModel2),
                "IOrmModel must be singleton — same instance across scopes.");

            // Scoped — different instance across scopes
            var compiler1 = sp1.GetRequiredService<IQueryCompiler>();
            var compiler2 = sp2.GetRequiredService<IQueryCompiler>();
            Assert.IsFalse(ReferenceEquals(compiler1, compiler2),
                "IQueryCompiler must be scoped — different instance across scopes.");

            // Scoped — same instance within same scope
            var compiler1Again = sp1.GetRequiredService<IQueryCompiler>();
            Assert.IsTrue(ReferenceEquals(compiler1, compiler1Again),
                "IQueryCompiler must be scoped — same instance within same scope.");

            // Transient — new instance every resolution
            var mapper1 = sp1.GetRequiredService<ILambdaParameterToDataSourceMapper>();
            var mapper2 = sp1.GetRequiredService<ILambdaParameterToDataSourceMapper>();
            Assert.IsFalse(ReferenceEquals(mapper1, mapper2),
                "ILambdaParameterToDataSourceMapper must be transient — new instance every resolution.");
        }

        private const string Db1ConnectionString = "Server=.;Database=Db1;Integrated Security=true;Encrypt=True;TrustServerCertificate=True";
        private const string Db2ConnectionString = "Server=.;Database=Db2;Integrated Security=true;Encrypt=True;TrustServerCertificate=True";

        /// <summary>
        ///     Two contexts pointed at different databases share one cached root provider (the cache key
        ///     excludes the connection string). Each must still talk to its own database — the options come
        ///     from the scope's configuration, not from a delegate that captured whichever extension instance
        ///     happened to build the provider first.
        /// </summary>
        [TestMethod]
        public void ContextsSharingAProvider_EachUseTheirOwnConnectionString()
        {
            using var context1 = new ConnectionStringTestContext(Db1ConnectionString);
            using var context2 = new ConnectionStringTestContext(Db2ConnectionString);

            var communication1 = (DbCommunicationBase)context1.GetDbCommunication();
            var communication2 = (DbCommunicationBase)context2.GetDbCommunication();

            Assert.IsTrue(ReferenceEquals(context1.GetRootServiceProvider(), context2.GetRootServiceProvider()),
                "Precondition: both contexts must resolve to the same cached root provider, otherwise this test proves nothing.");
            Assert.AreEqual(Db1ConnectionString, communication1.ConnectionString);
            Assert.AreEqual(Db2ConnectionString, communication2.ConnectionString);
        }

        [TestMethod]
        public void DataContextServices_IsInitializedWithTheOwningContextsConfiguration()
        {
            using var context = new ConnectionStringTestContext(Db1ConnectionString);

            var contextServices = context.GetDataContextServices();

            Assert.IsTrue(contextServices.IsInitialized);
            Assert.IsTrue(ReferenceEquals(context, contextServices.Context));
            Assert.AreEqual(Db1ConnectionString,
                contextServices.Configuration.GetRequiredExtension<SqlServerExtension>().ConnectionString);
        }

        /// <summary>
        ///     A scope created straight off the root provider was never bound to a context, so options-reading
        ///     services must fail loudly rather than silently pick up somebody else's connection string.
        /// </summary>
        [TestMethod]
        public void UninitializedScope_ResolvingDbCommunication_Throws()
        {
            var config = new DataContextConfiguration();
            config.UseSqlServer(Db1ConnectionString);
            config.UseUnitTestCustomization();

            var rootSp = OrmServiceManager.Instance.GetOrAdd(config);
            using var scope = rootSp.GetRequiredService<IServiceScopeFactory>().CreateScope();

            Assert.ThrowsException<InvalidOperationException>(
                () => scope.ServiceProvider.GetRequiredService<IDbCommunication>());
        }

        [TestMethod]
        public void DataContextServices_CannotBeInitializedTwice()
        {
            using var context = new ConnectionStringTestContext(Db1ConnectionString);
            var contextServices = context.GetDataContextServices();

            Assert.ThrowsException<InvalidOperationException>(
                () => contextServices.Initialize(context, new DataContextConfiguration()));
        }

        // Fully qualified: this namespace already has an unrelated `DataContext` (TestBase.cs).
        private sealed class ConnectionStringTestContext : Atis.Orm.DataContext
        {
            private readonly string _connectionString;

            public ConnectionStringTestContext(string connectionString)
            {
                _connectionString = connectionString;
            }

            protected override void OnConfiguring(DataContextConfiguration config)
            {
                config.UseSqlServer(_connectionString);
                config.UseUnitTestCustomization();
            }

            public IDbCommunication GetDbCommunication() => this.ServiceProvider.GetRequiredService<IDbCommunication>();

            public IDataContextServices GetDataContextServices() => this.ServiceProvider.GetRequiredService<IDataContextServices>();

            // The scope's own IServiceScopeFactory is the root provider's singleton, so identical factories
            // mean identical root providers.
            public object GetRootServiceProvider() => this.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        }
    }
}