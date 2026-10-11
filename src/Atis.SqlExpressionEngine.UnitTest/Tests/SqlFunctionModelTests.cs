using System.Reflection;

using Atis.Orm;
using Atis.Orm.Abstractions;
using Atis.Orm.Annotations;
using Atis.Orm.DataAccess;
using Atis.Orm.Functions;
using Atis.Orm.Metadata;
using Atis.Orm.SqlServer;
using Atis.SqlExpressionEngine.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

using OrmDataContext = Atis.Orm.DataContext;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    [TestClass]
    public class SqlFunctionModelTests
    {
        private static class MarkerFunctions
        {
            [SqlFunction(Template = "annotated({0})")]
            public static int AnnotatedOnly(int value)
                => throw new DirectCallNotSupportedException(nameof(AnnotatedOnly));

            [SqlFunction(Template = "old_name({0})")]
            public static int FluentOverridesAnnotation(int value)
                => throw new DirectCallNotSupportedException(nameof(FluentOverridesAnnotation));

            [SqlFunction(FunctionName = "attribute_name")]
            public static int AttributeFunctionName(int value)
                => throw new DirectCallNotSupportedException(nameof(AttributeFunctionName));

            [SqlFunction(FunctionName = "ignored_function_name", Template = "template_wins({0})")]
            public static int TemplateOverridesFunctionName(int value)
                => throw new DirectCallNotSupportedException(nameof(TemplateOverridesFunctionName));

            public static int FluentTemplate(int value)
                => throw new DirectCallNotSupportedException(nameof(FluentTemplate));
        }

        private sealed class FunctionModelConfig : DataContextConfiguration { }

        private sealed class FunctionModelContext : OrmDataContext
        {
            public FunctionModelContext() : base(new FunctionModelConfig()) { }

            protected override void OnConfiguring(DataContextConfiguration config)
                => config.UseSqlServer("Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True");

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.SqlFunction(Method(nameof(MarkerFunctions.FluentOverridesAnnotation)))
                            .HasName("fluent_name");
                modelBuilder.SqlFunction(() => MarkerFunctions.FluentTemplate(default))
                            .HasTemplate("fluent_template({0})");
            }

            public IOrmModel GetModel() => this.ServiceProvider.GetRequiredService<IOrmModel>();
        }

        [TestMethod]
        public void Model_returns_polymorphic_function_metadata_and_fluent_configuration_wins()
        {
            using var context = new FunctionModelContext();
            var model = context.GetModel();

            Assert.IsTrue(model.TryGetSqlFunction(Method(nameof(MarkerFunctions.AnnotatedOnly)), out var annotated));
            Assert.IsInstanceOfType<SqlTemplateFunctionMetadata>(annotated);

            Assert.IsTrue(model.TryGetSqlFunction(Method(nameof(MarkerFunctions.FluentOverridesAnnotation)), out var overridden));
            Assert.AreEqual(typeof(SqlFunctionMetadata), overridden.GetType());
            Assert.AreEqual("fluent_name", overridden.FunctionName);

            Assert.IsTrue(model.TryGetSqlFunction(Method(nameof(MarkerFunctions.AttributeFunctionName)), out var named));
            Assert.AreEqual(typeof(SqlFunctionMetadata), named.GetType());
            Assert.AreEqual("attribute_name", named.FunctionName);

            Assert.IsTrue(model.TryGetSqlFunction(Method(nameof(MarkerFunctions.FluentTemplate)), out var fluentTemplate));
            Assert.IsInstanceOfType<SqlTemplateFunctionMetadata>(fluentTemplate);

            Assert.IsTrue(model.TryGetSqlFunction(typeof(Sql).GetMethod(nameof(Sql.RowNumber)), out var window));
            Assert.IsInstanceOfType<SqlWindowFunctionMetadata>(window);
            Assert.AreEqual(SqlWindowFunction.RowNumber, ((SqlWindowFunctionMetadata)window).Function);

            Assert.IsTrue(model.TryGetSqlFunction(typeof(Sql).GetMethod(nameof(Sql.Over)), out var over));
            Assert.IsInstanceOfType<SqlWindowClauseMetadata>(over);
        }

        [TestMethod]
        public void Query_conversion_resolves_annotations_and_fluent_configuration_through_the_model()
        {
            using var context = new FunctionModelContext();
            var employees = context.CreateQuery<TestEntities.Employee>();
            var query = employees.Select(employee => new
            {
                Annotated = MarkerFunctions.AnnotatedOnly(employee.EmployeeId),
                AttributeNamed = MarkerFunctions.AttributeFunctionName(employee.EmployeeId),
                TemplateWins = MarkerFunctions.TemplateOverridesFunctionName(employee.EmployeeId),
                Fluent = MarkerFunctions.FluentTemplate(employee.EmployeeId),
                Renamed = MarkerFunctions.FluentOverridesAnnotation(employee.EmployeeId),
            });

            var sql = context.TranslateToSql(query);

            StringAssert.Contains(sql, "annotated(");
            StringAssert.Contains(sql, "attribute_name(");
            StringAssert.Contains(sql, "template_wins(");
            Assert.IsFalse(sql.Contains("ignored_function_name("), "A template must control the complete SQL shape.");
            StringAssert.Contains(sql, "fluent_template(");
            StringAssert.Contains(sql, "fluent_name(");
            Assert.IsFalse(sql.Contains("old_name("), "Fluent model configuration must override the annotation.");
        }

        private static MethodInfo Method(string name)
            => typeof(MarkerFunctions).GetMethod(name, BindingFlags.Public | BindingFlags.Static)
               ?? throw new InvalidOperationException($"Method '{name}' was not found.");
    }
}
