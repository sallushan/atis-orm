using Atis.DependencyInjection;
using Atis.Expressions;
using Atis.Orm;
using Atis.Orm.SqlServer;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.ExpressionConverters;
using Atis.SqlExpressionEngine.Preprocessors;
using Atis.SqlExpressionEngine.Services;
using Atis.SqlExpressionEngine.SqlExpressions;
using Atis.SqlExpressionEngine.UnitTest.Converters;
using Atis.SqlExpressionEngine.UnitTest.Preprocessors;
using Microsoft.Data.SqlClient;
using System.Collections.Concurrent;
using System.Linq.Expressions;

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
            var reflectionService = new OrmReflectionService();
            var dbCommunication = new SqlDbCommunication($"Server=.;Database={TestDatabaseSetup.DatabaseName};Integrated Security=true;Encrypt=True;TrustServerCertificate=True");
            var dbAdapter = new DatabaseAdapter(reflectionService, dbCommunication);
            var cacheKeyProvider = new ExpressionCacheKeyProvider();
            var queryCacheProvider = new CompiledQueryCacheProvider(cacheKeyProvider);
            var preprocessingRequirementTester = new PreprocessingRequirementTester();
            var sqlDataTypeFactory = new SqlDataTypeFactory();
            var parameterMapper = new LambdaParameterToDataSourceMapper();
            var sqlFactory = new SqlExpressionFactory();
            var logger = new Services.Logger();
            var model = new Services.Model(reflectionService);
            var serviceCollection = new object[] { sqlDataTypeFactory, sqlFactory, model, parameterMapper, reflectionService, logger, expressionEvaluator };
            var converterServiceProvider = new ExpressionConverterDependencyProviderByCollection(serviceCollection);
            var factoryProvider = new LinqToSqlConverterFactoryProvider(reflectionService, expressionEvaluator, userProvidedFactories: [new SqlFunctionConverterFactory()]);
            var treeConverter = new LinqToSqlExpressionTreeConverter(converterServiceProvider, factoryProvider);
            var preprocessor = GetPreprocessorProvider(reflectionService, expressionEvaluator, model);
            var linqToSqlConverter = new LinqToSqlConverter(treeConverter, new SqlExpressionPostprocessorProvider(postprocessors: []));
            var sqlExpressionTranslator = new SqlExpressionTranslatorBase();
            var dbParameterFactory = new SqlDbParameterFactory();
            var elementFactoryBuilder = new ElementFactoryBuilder();
            var queryTranslator = new QueryTranslator(preprocessor, linqToSqlConverter, sqlExpressionTranslator, logger);
            var queryCompiler = new QueryCompiler(queryTranslator, preprocessingRequirementTester, dbParameterFactory, elementFactoryBuilder);
            var expressionVariableValueExtractor = new ExpressionVariableValuesExtractor();
            var queryExecutor = new QueryExecutor(dbAdapter, queryCacheProvider, queryCompiler, expressionVariableValueExtractor, preprocessor);
            var ormQueryProvider = new OrmQueryProvider(reflectionService, queryExecutor);
            var queryable = new Queryable<TestEntities.Employee>(ormQueryProvider);
            var results = queryable.Select(x => new { x.FirstName, x.EmployeeId }).Take(10).ToList();
            foreach (var result in results)
            {
                Console.WriteLine($"{result.EmployeeId}: {result.FirstName}");
            }
        }


        [TestMethod]
        public async Task ToListAsync_test()
        {
            var expressionEvaluator = new ExpressionEvaluator();
            var reflectionService = new OrmReflectionService();
            var dbCommunication = new SqlDbCommunication($"Server=.;Database={TestDatabaseSetup.DatabaseName};Integrated Security=true;Encrypt=True;TrustServerCertificate=True");
            var dbAdapter = new DatabaseAdapter(reflectionService, dbCommunication);
            var cacheKeyProvider = new ExpressionCacheKeyProvider();
            var queryCacheProvider = new CompiledQueryCacheProvider(cacheKeyProvider);
            var preprocessingRequirementTester = new PreprocessingRequirementTester();
            var sqlDataTypeFactory = new SqlDataTypeFactory();
            var parameterMapper = new LambdaParameterToDataSourceMapper();
            var sqlFactory = new SqlExpressionFactory();
            var logger = new Services.Logger();
            var model = new Services.Model(reflectionService);
            var serviceCollection = new object[] { sqlDataTypeFactory, sqlFactory, model, parameterMapper, reflectionService, logger, expressionEvaluator };
            var converterServiceProvider = new ExpressionConverterDependencyProviderByCollection(serviceCollection);
            var factoryProvider = new LinqToSqlConverterFactoryProvider(reflectionService, expressionEvaluator, userProvidedFactories: [new SqlFunctionConverterFactory()]);
            var treeConverter = new LinqToSqlExpressionTreeConverter(converterServiceProvider, factoryProvider);
            var preprocessor = GetPreprocessorProvider(reflectionService, expressionEvaluator, model);
            var linqToSqlConverter = new LinqToSqlConverter(treeConverter, new SqlExpressionPostprocessorProvider(postprocessors: []));
            var sqlExpressionTranslator = new SqlExpressionTranslatorBase();
            var dbParameterFactory = new SqlDbParameterFactory();
            var elementFactoryBuilder = new ElementFactoryBuilder();
            var queryTranslator = new QueryTranslator(preprocessor, linqToSqlConverter, sqlExpressionTranslator, logger);
            var queryCompiler = new QueryCompiler(queryTranslator, preprocessingRequirementTester, dbParameterFactory, elementFactoryBuilder);
            var expressionVariableValueExtractor = new ExpressionVariableValuesExtractor();
            var queryExecutor = new QueryExecutor(dbAdapter, queryCacheProvider, queryCompiler, expressionVariableValueExtractor, preprocessor);
            var ormQueryProvider = new OrmQueryProvider(reflectionService, queryExecutor);
            var queryable = new Queryable<TestEntities.Employee>(ormQueryProvider);
            var results = await queryable.Select(x => new { x.FirstName, x.EmployeeId }).Take(10).ToListAsync();
            foreach (var result in results)
            {
                Console.WriteLine($"{result.EmployeeId}: {result.FirstName}");
            }
        }

        private IExpressionPreprocessorProvider GetPreprocessorProvider(IReflectionService reflectionService, IExpressionEvaluator expressionEvaluator, IModel model/*, IQueryProvider queryProvider*/)
        {
            var preprocessor = new OrmExpressionPreprocessorProvider(model, reflectionService, expressionEvaluator, plugins: new[] { new CustomBusinessMethodPreprocessor() });
            return preprocessor;
        }

        [TestMethod]
        public void DataContext_CreateQuery_Test()
        {
            using var dataContext = new OrmDbContext();
            var invoices = dataContext.CreateQuery<TestEntities.Employee>();
            var results = invoices.Select(x => new { x.FirstName, x.EmployeeId }).Take(10).ToList();
            foreach (var result in results)
            {
                Console.WriteLine($"{result.EmployeeId}: {result.FirstName}");
            }
        }


        [TestMethod]
        public void DataContext_Custom_Business_Method_test()
        {
            using var dataContext = new OrmDbContext();
            var invoices = dataContext.CreateQuery<TestEntities.Employee>();
            var results = invoices.Select(x => new { x.FirstName, x.EmployeeId, FullName = GeneralTranslationTests.FullName(x.FirstName, x.LastName) }).Take(10).ToList();
            foreach (var result in results)
            {
                Console.WriteLine($"{result.EmployeeId}: {result.FullName}");
            }
        }

        [TestMethod]
        public void DataContext_Annotation_Customization_Test()
        {
            var config = new DataContextConfiguration();
            config.AddOrUpdateExtension(new ComponentAnnotationExtension());
            using var dbc = new OrmDbContext(config);
            var salesOrders = dbc.CreateQuery<SalesOrderWithSystemAnnotation>();
            var date = new DateTime(2020, 1, 1);
            var q = salesOrders.Where(x => x.OrderDate >= date);
            var queryResult = dbc.TranslateToSql(q);
            Console.WriteLine(queryResult);
            var expectedResult = @"
SELECT t1.ROW_ID AS RowId, t1.ORD_ID AS SalesOrderId, t1.ORD_DT AS OrderDate, t1.CST_NM AS CustomerName
FROM SLS_ORD AS t1
WHERE (t1.ORD_DT >= @p0)
";
            ValidateQueryResults(queryResult, expectedResult);
        }

        [TestMethod]
        public void DataContext_OnModelCreating_Test()
        {
            var config = new DataContextConfiguration();
            using var dbc = new OrmDbContext(config);
            var externalEntity = dbc.CreateQuery<SimulatedExternalEntity>();
            var q = externalEntity.Where(x => x.PrimaryKey == 1);
            var queryResult = dbc.TranslateToSql(q);
            Console.WriteLine(queryResult);
            string expectedResult = @"
SELECT t1.PK AS PrimaryKey, t1.FLD2 AS SomeOtherField
FROM SIM_EXT_TBL AS t1
WHERE (t1.PK = @p0)
";
            ValidateQueryResults(queryResult, expectedResult);
        }

        [TestMethod]
        public void Fluent_HasMany_KeyBased_Navigation_Test()
        {
            using var dbc = new OrmDbContext();
            var authors = dbc.CreateQuery<FluentAuthor>();
            // one-to-many navigation defined via fluent HasMany(key-based) used in an EXISTS subquery
            var q = authors.Where(a => a.Books.Any(b => b.Title == "Test"));
            var queryResult = dbc.TranslateToSql(q);
            Console.WriteLine(queryResult);
            string expectedResult = @"
SELECT t1.Id AS Id, t1.FRST_NM AS FirstName, t1.LAST_NM AS LastName, t1.CountryId AS CountryId
FROM dbo.AUTHOR AS t1
WHERE EXISTS(
	SELECT @p0 AS Col1
	FROM BOOK AS t2
	WHERE (t1.Id = t2.AuthorId) AND (t2.BOOK_TITLE = @p1)
)
";
            ValidateQueryResults(queryResult, expectedResult);
        }

        [TestMethod]
        public void Fluent_Calculated_Property_Test()
        {
            using var dbc = new OrmDbContext();
            var authors = dbc.CreateQuery<FluentAuthor>();
            // FullName is a fluent calculated property: x => x.FirstName + " " + x.LastName
            var q = authors.Select(a => new { a.Id, a.FullName });
            var queryResult = dbc.TranslateToSql(q);
            Console.WriteLine(queryResult);
            string expectedResult = @"
SELECT t1.ID AS Id, ((t1.FRST_NM + @p0) + t1.LAST_NM) AS FullName
FROM dbo.AUTHOR AS t1
";
            ValidateQueryResults(queryResult, expectedResult);
        }

        [TestMethod]
        public void Fluent_Navigations_Produce_Expected_Metadata()
        {
            using var dbc = new OrmDbContext();
            // touch the model so OnModelCreating runs
            dbc.CreateQuery<FluentAuthor>();

            var author = dbc.GetEntityMetadata<FluentAuthor>();
            Assert.IsNotNull(author, "FluentAuthor metadata should be registered");

            // table + schema
            Assert.AreEqual("AUTHOR", author.Table.TableName);
            Assert.AreEqual("dbo", author.Table.Schema);

            // columns: name overrides + primary key
            var idCol = author.SqlColumns.Single(c => c.ModelPropertyName == nameof(FluentAuthor.Id));
            Assert.AreEqual("Id", idCol.DatabaseColumnName, "Id keeps its default (un-renamed) column name");
            Assert.IsTrue(idCol.IsPrimaryKey, "Id should be the primary key");
            Assert.AreEqual("FRST_NM", author.SqlColumns.Single(c => c.ModelPropertyName == nameof(FluentAuthor.FirstName)).DatabaseColumnName);
            Assert.AreEqual("LAST_NM", author.SqlColumns.Single(c => c.ModelPropertyName == nameof(FluentAuthor.LastName)).DatabaseColumnName);

            // calculated property present
            Assert.IsTrue(author.CalculatedProperties.ContainsKey(nameof(FluentAuthor.FullName)));

            // ToChildren (HasMany): JoinCondition (parent=Author, child=Book), JoinedSource (Author)=>IQueryable<Book>
            AssertNavigation(author, nameof(FluentAuthor.Books), NavigationType.ToChildren,
                expectedParentType: typeof(FluentAuthor), expectedChildType: typeof(FluentBook),
                thisType: typeof(FluentAuthor), targetType: typeof(FluentBook));

            // ToSingleChild (HasChild)
            AssertNavigation(author, nameof(FluentAuthor.PrimaryBook), NavigationType.ToSingleChild,
                expectedParentType: typeof(FluentAuthor), expectedChildType: typeof(FluentBook),
                thisType: typeof(FluentAuthor), targetType: typeof(FluentBook));

            // ToParentOptional (HasParent(...).Optional()): JoinCondition (parent=Country, child=Author)
            AssertNavigation(author, nameof(FluentAuthor.Country), NavigationType.ToParentOptional,
                expectedParentType: typeof(FluentCountry), expectedChildType: typeof(FluentAuthor),
                thisType: typeof(FluentAuthor), targetType: typeof(FluentCountry));

            // ToParent (HasParent explicit lambda) on FluentBook
            var book = dbc.GetEntityMetadata<FluentBook>();
            Assert.AreEqual("BOOK", book.Table.TableName);
            Assert.AreEqual("BOOK_TITLE", book.SqlColumns.Single(c => c.ModelPropertyName == nameof(FluentBook.Title)).DatabaseColumnName);
            AssertNavigation(book, nameof(FluentBook.Author), NavigationType.ToParent,
                expectedParentType: typeof(FluentAuthor), expectedChildType: typeof(FluentBook),
                thisType: typeof(FluentBook), targetType: typeof(FluentAuthor));
        }

        private static void AssertNavigation(EntityMetadata entity, string navName, NavigationType expectedType,
            Type expectedParentType, Type expectedChildType, Type thisType, Type targetType)
        {
            Assert.IsTrue(entity.Navigations.TryGetValue(navName, out var nav), $"Navigation '{navName}' should exist");
            Assert.AreEqual(expectedType, nav.NavigationType, $"Navigation '{navName}' type");

            // JoinCondition is always (parent, child) => bool
            Assert.IsNotNull(nav.JoinCondition, $"'{navName}' JoinCondition should be set");
            Assert.AreEqual(2, nav.JoinCondition.Parameters.Count);
            Assert.AreEqual(expectedParentType, nav.JoinCondition.Parameters[0].Type, $"'{navName}' join parent param type");
            Assert.AreEqual(expectedChildType, nav.JoinCondition.Parameters[1].Type, $"'{navName}' join child param type");

            // JoinedSource is (thisEntity) => IQueryable<target>
            Assert.IsNotNull(nav.JoinedSource, $"'{navName}' JoinedSource should be set");
            Assert.AreEqual(1, nav.JoinedSource.Parameters.Count);
            Assert.AreEqual(thisType, nav.JoinedSource.Parameters[0].Type, $"'{navName}' JoinedSource param type");
            Assert.AreEqual(typeof(IQueryable<>).MakeGenericType(targetType), nav.JoinedSource.Body.Type, $"'{navName}' JoinedSource body type");
        }

        [TestMethod]
        public void OnModelCreating_IsCalledOnlyOnce()
        {
            // this is a hack to clear the cache, if this test is executed with other
            // tests, OrmDbContext will be initialized and Model will be created

            var f = typeof(ServiceManagerBase).GetField("_serviceProviderCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (f is null)
            {
                Console.WriteLine("Warning! _serviceProviderCache field was not found in ServiceManagerBase");
            }
            else
            {
                ((ConcurrentDictionary<int, IServiceProvider>)f.GetValue(null)).Clear();
            }
            OrmDbContext._onModelCreatingCallCount = 0;

            using var ctx1 = new OrmDbContext();
            using var ctx2 = new OrmDbContext();
            using var ctx3 = new OrmDbContext();

            var q1 = ctx1.CreateQuery<SimulatedExternalEntity>().Where(x => x.PrimaryKey == 1);
            var q2 = ctx2.CreateQuery<SimulatedExternalEntity>().Where(x => x.PrimaryKey == 1);
            var q3 = ctx3.CreateQuery<SimulatedExternalEntity>().Where(x => x.PrimaryKey == 1);

            Assert.AreEqual(1, OrmDbContext._onModelCreatingCallCount);
        }
    }
}
