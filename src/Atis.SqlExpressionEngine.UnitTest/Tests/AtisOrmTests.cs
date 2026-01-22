using Atis.Expressions;
using Atis.Orm;
using Atis.Orm.SqlServer;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.Preprocessors;
using Atis.SqlExpressionEngine.Services;
using Atis.SqlExpressionEngine.SqlExpressions;
using Atis.SqlExpressionEngine.UnitTest.Converters;
using Atis.SqlExpressionEngine.UnitTest.Preprocessors;
using Atis.SqlExpressionEngine.UnitTest.Services;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    [TestClass]
    public class AtisOrmTests : TestBase
    {
        [TestMethod]
        public async Task Element_factory_basic_test()
        {
            var setup = new TestDatabaseSetup($"Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True");
            await setup.SetupAsync();

            var query = queryProvider.DataSet<TestEntities.Employee>();
            var result = query.Select(x => new { EmpId = x.EmployeeId, NameParts = new { x.FirstName, x.LastName }, x.HireDate }).Top(5);
            
            var sqlExpression = ConvertExpressionToSqlExpression(result.Expression, out var updatedQueryExpression);

            if (sqlExpression is SqlDerivedTableExpression derivedTable)
            {
                var translator = new SqlExpressionTranslatorBase();

                var translationResult = translator.Translate(derivedTable);
                var sql = translationResult.Sql;

                Console.WriteLine(sql);

                var elementFactoryBuilder = new ElementFactoryBuilder();
                var elementFactory = elementFactoryBuilder.CreateElementFactory(updatedQueryExpression, derivedTable);
                
                using var conn = new SqlConnection($"Server=.;Database={TestDatabaseSetup.DatabaseName};Integrated Security=true;Encrypt=True;TrustServerCertificate=True");
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                conn.Open();
                using var reader = cmd.ExecuteReader(System.Data.CommandBehavior.SequentialAccess);
                while (reader.Read())
                {
                    var element = elementFactory(reader);
                    Console.WriteLine(element);
                }
                reader.Close();
                conn.Close();
            }
            else
            {
                Assert.Fail("Expected SqlDerivedTableExpression");
            }
        }

        [TestMethod]
        public void ExpressionEqualityComparer_test()
        {
            Expression<Func<TestEntities.Employee, object>> expr1 = x => new { x.EmployeeId, NameParts = new { x.FirstName, x.LastName }, x.HireDate };
            Expression<Func<TestEntities.Employee, object>> expr2 = x => new { x.EmployeeId, NameParts = new { x.FirstName, x.LastName }, x.HireDate };
            var comparer = ExpressionEqualityComparer.Instance;
            var areEqual = comparer.Equals(expr1, expr2);
            Assert.IsTrue(areEqual, "Expressions should be equal");
            var hash1 = comparer.GetHashCode(expr1);
            var hash2 = comparer.GetHashCode(expr2);
            Assert.AreEqual(hash1, hash2, "Hash codes should be equal");

            var hireDate = DateTime.Today;
            Expression<Func<TestEntities.Employee, bool>> expr3 = x => x.HireDate > hireDate;
            hireDate = DateTime.Today.AddDays(1);
            Expression<Func<TestEntities.Employee, bool>> expr4 = x => x.HireDate > hireDate;

            areEqual = comparer.Equals(expr3, expr4);
            Assert.IsTrue(areEqual, "Expressions should be equal");

            var hash3 = comparer.GetHashCode(expr3);
            var hash4 = comparer.GetHashCode(expr4);
            Assert.AreEqual(hash3, hash4, "Hash codes should be equal");

            var name = "John";
            Expression<Func<TestEntities.Employee, bool>> expr5 = x => x.FirstName == name;
            Expression<Func<TestEntities.Employee, bool>> expr6 = x => x.LastName == name;

            areEqual = comparer.Equals(expr5, expr6);
            Assert.IsFalse(areEqual, "Expressions should not be equal");

            var hash5 = comparer.GetHashCode(expr5);
            var hash6 = comparer.GetHashCode(expr6);
            Assert.AreNotEqual(hash5, hash6, "Hash codes should not be equal");

            Expression<Func<TestEntities.Employee, bool>> expr7 = x => x.FirstName == "John";
            Expression<Func<TestEntities.Employee, bool>> expr8 = x => x.FirstName == name;

            areEqual = comparer.Equals(expr7, expr8);
            Assert.IsFalse(areEqual, "Expressions should not be equal");

            var hash7 = comparer.GetHashCode(expr7);
            var hash8 = comparer.GetHashCode(expr8);
            Assert.AreNotEqual(hash7, hash8, "Hash codes should not be equal");

            var marksGained = 85;
            Expression<Func<StudentGrade, bool>> expr9 = x => x.NavStudentGradeDetails.Where(y => y.MarksGained > marksGained).Any();
            marksGained = 90;
            Expression<Func<StudentGrade, bool>> expr10 = x => x.NavStudentGradeDetails.Where(y => y.MarksGained > marksGained).Any();

            areEqual = comparer.Equals(expr9, expr10);
            Assert.IsTrue(areEqual, "Expressions should be equal");
            var hash9 = comparer.GetHashCode(expr9);
            var hash10 = comparer.GetHashCode(expr10);
            Assert.AreEqual(hash9, hash10, "Hash codes should be equal");
        }

        [TestMethod]
        public void ToList_test()
        {
            var expressionEvaluator = new ExpressionEvaluator();
            var reflectionService = new ReflectionService(expressionEvaluator);
            var dbCommunication = new SqlDbCommunication($"Server=.;Database={TestDatabaseSetup.DatabaseName};Integrated Security=true;Encrypt=True;TrustServerCertificate=True");
            var dbAdapter = new DatabaseAdapter(reflectionService, dbCommunication);
            var cacheKeyProvider = new ExpressionCacheKeyProvider();
            var queryCacheProvider = new CompiledQueryCacheProvider(cacheKeyProvider);            
            var preprocessingRequirementTester = new PreprocessingRequirementTester();
            var sqlDataTypeFactory = new SqlDataTypeFactory();
            var parameterMapper = new LambdaParameterToDataSourceMapper();
            var sqlFactory = new SqlExpressionFactory();
            var logger = new Services.Logger();
            var model = new Services.Model();
            var contextExtensions = new object[] { sqlDataTypeFactory, sqlFactory, model, parameterMapper, reflectionService, logger };
            var conversionContext = new ConversionContext(contextExtensions);
            var expressionConverterProvider = new LinqToSqlExpressionConverterProvider(conversionContext, factories: [new SqlFunctionConverterFactory(conversionContext)]);
            var preprocessor = GetPreprocessorProvider(reflectionService, model);
            var linqToSqlConverter = new LinqToSqlConverter(reflectionService, expressionConverterProvider, new SqlExpressionPostprocessorProvider(postprocessors: []));
            var sqlExpressionTranslator = new SqlExpressionTranslatorBase();
            var dbParameterFactory = new SqlDbParameterFactory();
            var elementFactoryBuilder = new ElementFactoryBuilder();
            var queryCompiler = new QueryCompiler(preprocessor, preprocessingRequirementTester, linqToSqlConverter, sqlExpressionTranslator, dbParameterFactory, elementFactoryBuilder);
            var expressionVariableValueExtrator = new ExpressionVariableValuesExtractor();
            var queryExecutor = new QueryExecutor(dbAdapter, queryCacheProvider, queryCompiler, expressionVariableValueExtrator, preprocessor);
            var ormQueryProvider = new OrmQueryProvider(reflectionService, queryExecutor);
            var queryable = new Queryable<TestEntities.Employee>(ormQueryProvider);
            var results = queryable.Select(x => new { x.FirstName, x.EmployeeId }).Take(10).ToList();
            foreach (var result in results)
            {
                Console.WriteLine($"{result.EmployeeId}: {result.FirstName}");
            }
        }

        private IExpressionPreprocessorProvider GetPreprocessorProvider(IReflectionService reflectionService, IModel model/*, IQueryProvider queryProvider*/)
        {
            //var navigateToManyPreprocessor = new NavigateToManyPreprocessor(queryProvider, reflectionService);
            //var navigateToOnePreprocessor = new NavigateToOnePreprocessor(reflectionService, queryProvider);
            var queryVariablePreprocessor = new QueryVariableReplacementPreprocessor();
            //var childJoinReplacementPreprocessor = new ChildJoinReplacementPreprocessor(reflectionService);
            var calculatedPropertyReplacementPreprocessor = new CalculatedPropertyPreprocessor(reflectionService);
            var specificationPreprocessor = new SpecificationCallRewriterPreprocessor(reflectionService);
            var convertPreprocessor = new ConvertExpressionReplacementPreprocessor();
            var allToAnyRewriterPreprocessor = new AllToAnyRewriterPreprocessor();
            var inValuesReplacementPreprocessor = new InValuesExpressionReplacementPreprocessor(reflectionService);
            //var nonPrimitivePropertyReplacementPreprocessor = new NonPrimitiveCalculatedPropertyPreprocessor(reflectionService);
            //var concreteParameterPreprocessor = new ConcreteParameterReplacementPreprocessor(new QueryPartsIdentifier(), reflectionService);
            var methodInterfaceTypeReplacementPreprocessor = new QueryMethodGenericTypeReplacementPreprocessor(reflectionService);
            var customMethodReplacementPreprocessor = new CustomBusinessMethodPreprocessor();
            var navigationEqualityPreprocessor = new NavigationNullEqualityPreprocessor(model);
            var preprocessor = new ExpressionPreprocessorProvider([queryVariablePreprocessor, methodInterfaceTypeReplacementPreprocessor, /*navigateToManyPreprocessor, navigateToOnePreprocessor,*/ /*childJoinReplacementPreprocessor, */calculatedPropertyReplacementPreprocessor, specificationPreprocessor, convertPreprocessor, allToAnyRewriterPreprocessor, inValuesReplacementPreprocessor, customMethodReplacementPreprocessor,
                navigationEqualityPreprocessor
                /*, concreteParameterPreprocessor*/]);
            return preprocessor;
        }
    }
}
