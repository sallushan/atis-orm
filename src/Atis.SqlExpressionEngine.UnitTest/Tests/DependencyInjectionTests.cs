using Atis.Orm;
using Atis.Orm.SqlServer;
using Atis.SqlExpressionEngine.Abstractions;
using Microsoft.Extensions.DependencyInjection;

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
    }
}