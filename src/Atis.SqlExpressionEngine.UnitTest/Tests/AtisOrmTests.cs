using Atzonix.DependencyInjection;
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

using Atis.Orm.Abstractions;
using Atis.Orm.DataAccess;
using Atis.Orm.Metadata;
using Atis.Orm.Preprocessing;
using Atis.Orm.Querying;
using Atis.Orm.Services;
using Atis.Orm.Translation;
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

                var translation = translator.Translate(derivedTable);
                var nameGenerator = new SqlDbParameterNameGenerator();
                var renderer = new CommandRenderer(new SqlDbParameterFactory(nameGenerator));
                var sql = renderer.Render(translation.Fragments, p => p.InitialValue).Sql;

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
        public async Task OrderBy_on_full_entity_without_projection_executes_against_db()
        {
            var setup = new TestDatabaseSetup("Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True");
            await setup.SetupAsync();

            var db = new OrmDbContext();
            var employees = db.CreateQuery<TestEntities.Employee>();
            // Full entity, NO projection -> ORDER BY lands on the top-level query. Before the
            // translator stopped wrapping the root query in parentheses, this produced an invalid
            // "( SELECT ... ORDER BY ... )" statement and SQL Server rejected it with
            // "Incorrect syntax near 'ORDER'".
            var query = employees.OrderByDescending(x => x.LastName)
                                    .Take(50);
            var sql = db.TranslateToSql(query);
            Console.WriteLine(sql);
            var results = await query.ToListAsync();
            Assert.IsTrue(results.Count > 0, "Full-entity ordered query should materialize rows.");
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
            var expressionVariableValueExtractor = new ExpressionVariableValuesExtractor(expressionEvaluator, new VariableIdentityProvider());
            var sqlDataTypeFactory = new SqlDataTypeFactory();
            var parameterMapper = new LambdaParameterToDataSourceMapper();
            var sqlFactory = new SqlExpressionFactory();
            var logger = new Services.Logger();
            var model = new Services.Model(reflectionService);
            var serviceCollection = new object[] { sqlDataTypeFactory, sqlFactory, model, parameterMapper, reflectionService, logger, expressionEvaluator };
            var converterServiceProvider = new ExpressionConverterDependencyProviderByCollection(serviceCollection);
            var factoryProvider = new LinqToSqlConverterFactoryProvider(reflectionService, expressionEvaluator, new VariableIdentityProvider(), userProvidedFactories: [new SqlFunctionConverterFactory()]);
            var treeConverter = new LinqToSqlExpressionTreeConverter(converterServiceProvider, factoryProvider);
            var preprocessor = GetPreprocessorProvider(reflectionService, expressionEvaluator, model);
            var linqToSqlConverter = new LinqToSqlConverter(treeConverter, new SqlExpressionPostprocessorProvider(postprocessors: []));
            var sqlExpressionTranslator = new SqlExpressionTranslatorBase();
            var dbParameterFactory = new SqlDbParameterFactory(new SqlDbParameterNameGenerator());
            var commandRenderer = new CommandRenderer(dbParameterFactory);
            var elementFactoryBuilder = new ElementFactoryBuilder();
            var queryTranslator = new QueryTranslator(preprocessor, linqToSqlConverter, sqlExpressionTranslator, logger);
            var queryCompiler = new QueryCompiler(queryTranslator, commandRenderer, dbParameterFactory, elementFactoryBuilder);
            var queryExecutor = new QueryExecutor(dbAdapter, queryCacheProvider, queryCompiler, expressionVariableValueExtractor, new NoOpNavigationInitializer());
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
            var expressionVariableValueExtractor = new ExpressionVariableValuesExtractor(expressionEvaluator, new VariableIdentityProvider());
            var sqlDataTypeFactory = new SqlDataTypeFactory();
            var parameterMapper = new LambdaParameterToDataSourceMapper();
            var sqlFactory = new SqlExpressionFactory();
            var logger = new Services.Logger();
            var model = new Services.Model(reflectionService);
            var serviceCollection = new object[] { sqlDataTypeFactory, sqlFactory, model, parameterMapper, reflectionService, logger, expressionEvaluator };
            var converterServiceProvider = new ExpressionConverterDependencyProviderByCollection(serviceCollection);
            var factoryProvider = new LinqToSqlConverterFactoryProvider(reflectionService, expressionEvaluator, new VariableIdentityProvider(), userProvidedFactories: [new SqlFunctionConverterFactory()]);
            var treeConverter = new LinqToSqlExpressionTreeConverter(converterServiceProvider, factoryProvider);
            var preprocessor = GetPreprocessorProvider(reflectionService, expressionEvaluator, model);
            var linqToSqlConverter = new LinqToSqlConverter(treeConverter, new SqlExpressionPostprocessorProvider(postprocessors: []));
            var sqlExpressionTranslator = new SqlExpressionTranslatorBase();
            var dbParameterFactory = new SqlDbParameterFactory(new SqlDbParameterNameGenerator());
            var commandRenderer = new CommandRenderer(dbParameterFactory);
            var elementFactoryBuilder = new ElementFactoryBuilder();
            var queryTranslator = new QueryTranslator(preprocessor, linqToSqlConverter, sqlExpressionTranslator, logger);
            var queryCompiler = new QueryCompiler(queryTranslator, commandRenderer, dbParameterFactory, elementFactoryBuilder);
            var queryExecutor = new QueryExecutor(dbAdapter, queryCacheProvider, queryCompiler, expressionVariableValueExtractor, new NoOpNavigationInitializer());
            var ormQueryProvider = new OrmQueryProvider(reflectionService, queryExecutor);
            var queryable = new Queryable<TestEntities.Employee>(ormQueryProvider);
            var results = await queryable.Select(x => new { x.FirstName, x.EmployeeId }).Take(10).ToListAsync();
            foreach (var result in results)
            {
                Console.WriteLine($"{result.EmployeeId}: {result.FirstName}");
            }
        }

        // The manually-wired ToList/ToListAsync tests materialize anonymous projections only, so lazy
        // navigation initialization is a no-op for them.
        private sealed class NoOpNavigationInitializer : INavigationInitializer
        {
            public void Initialize(object entity) { }
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
        public void Sub_query_Any_compared_with_bool_variable_parameterizes_the_variable()
        {
            using var dbc = new OrmDbContext();
            var authors = dbc.CreateQuery<FluentAuthor>();
            var flag = true;
            var q = authors.Where(a => a.Books.Any(b => b.Title == "Test") == flag);
            var queryResult = dbc.TranslateToSql(q);
            Console.WriteLine(queryResult);
            // `exists` is a predicate, so comparing it with a bool first turns it into a value
            // via `CASE WHEN ... THEN 1 ELSE 0 END`; the captured `flag` becomes @p2.
            string expectedResult = @"
SELECT t1.Id AS Id, t1.FRST_NM AS FirstName, t1.LAST_NM AS LastName, t1.CountryId AS CountryId
FROM dbo.AUTHOR AS t1
WHERE (CASE WHEN EXISTS(
	SELECT @p0 AS Col1
	FROM BOOK AS t2
	WHERE (t1.Id = t2.AuthorId) AND (t2.BOOK_TITLE = @p1)
) THEN 1 ELSE 0 END = @p2)
";
            ValidateQueryResults(queryResult, expectedResult);
        }

        // Baseline for the shape above: plain `Any()` and `!Any()` produce a bare EXISTS /
        // NOT EXISTS with no CASE WHEN wrapper. Kept next to the `== flag` test so the cost
        // of the comparison form stays visible.
        [TestMethod]
        public void Sub_query_Any_without_comparison_emits_bare_exists()
        {
            using var dbc = new OrmDbContext();
            var authors = dbc.CreateQuery<FluentAuthor>();

            var any = dbc.TranslateToSql(authors.Where(a => a.Books.Any(b => b.Title == "Test")));
            Console.WriteLine(any);
            ValidateQueryResults(any, @"
SELECT t1.Id AS Id, t1.FRST_NM AS FirstName, t1.LAST_NM AS LastName, t1.CountryId AS CountryId
FROM dbo.AUTHOR AS t1
WHERE EXISTS(
	SELECT @p0 AS Col1
	FROM BOOK AS t2
	WHERE (t1.Id = t2.AuthorId) AND (t2.BOOK_TITLE = @p1)
)
");

            var notAny = dbc.TranslateToSql(authors.Where(a => !a.Books.Any(b => b.Title == "Test")));
            Console.WriteLine(notAny);
            ValidateQueryResults(notAny, @"
SELECT t1.Id AS Id, t1.FRST_NM AS FirstName, t1.LAST_NM AS LastName, t1.CountryId AS CountryId
FROM dbo.AUTHOR AS t1
WHERE NOT EXISTS(
	SELECT @p0 AS Col1
	FROM BOOK AS t2
	WHERE (t1.Id = t2.AuthorId) AND (t2.BOOK_TITLE = @p1)
)
");
        }

        // An inline `== true` literal is parameterized just like a captured variable, so it
        // gets the same CASE WHEN wrapper -- it is NOT folded back to a bare EXISTS.
        [TestMethod]
        public void Sub_query_Any_compared_with_bool_literal_is_not_folded()
        {
            using var dbc = new OrmDbContext();
            var authors = dbc.CreateQuery<FluentAuthor>();
            var q = authors.Where(a => a.Books.Any(b => b.Title == "Test") == true);
            var queryResult = dbc.TranslateToSql(q);
            Console.WriteLine(queryResult);
            ValidateQueryResults(queryResult, @"
SELECT t1.Id AS Id, t1.FRST_NM AS FirstName, t1.LAST_NM AS LastName, t1.CountryId AS CountryId
FROM dbo.AUTHOR AS t1
WHERE (CASE WHEN EXISTS(
	SELECT @p0 AS Col1
	FROM BOOK AS t2
	WHERE (t1.Id = t2.AuthorId) AND (t2.BOOK_TITLE = @p1)
) THEN 1 ELSE 0 END = @p2)
");
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
        public void Fluent_HasMany_CompositeKey_Navigation_Test()
        {
            using var dbc = new OrmDbContext();
            var companies = dbc.CreateQuery<FluentCompany>();
            // composite-key one-to-many navigation defined via fluent HasMany with `new { }` selectors
            var q = companies.Where(c => c.Employees.Any(e => e.EmployeeName == "Test"));
            var queryResult = dbc.TranslateToSql(q);
            Console.WriteLine(queryResult);
            string expectedResult = @"
SELECT t1.CompanyId AS CompanyId, t1.DivisionId AS DivisionId, t1.Name AS Name
FROM COMPANY AS t1
WHERE EXISTS(
	SELECT @p0 AS Col1
	FROM EMPLOYEE AS t2
	WHERE ((t1.CompanyId = t2.CompanyId) AND (t1.DivisionId = t2.DivisionId)) AND (t2.EmployeeName = @p1)
)
";
            ValidateQueryResults(queryResult, expectedResult);
        }

        [TestMethod]
        public void Fluent_CompositeKey_Navigation_Produces_AndAlso_JoinCondition()
        {
            using var dbc = new OrmDbContext();
            // touch the model so OnModelCreating runs
            dbc.CreateQuery<FluentCompany>();

            var company = dbc.GetEntityMetadata<FluentCompany>();
            Assert.IsNotNull(company, "FluentCompany metadata should be registered");

            Assert.IsTrue(company.Navigations.TryGetValue(nameof(FluentCompany.Employees), out var nav));
            Assert.AreEqual(NavigationType.ToChildren, nav.NavigationType);

            // JoinCondition body should be: p.CompanyId == c.CompanyId && p.DivisionId == c.DivisionId
            var body = nav.JoinCondition.Body as BinaryExpression;
            Assert.IsNotNull(body, "Composite join condition should be a binary expression");
            Assert.AreEqual(ExpressionType.AndAlso, body.NodeType, "Composite join condition should be an AndAlso chain");

            var left = body.Left as BinaryExpression;
            var right = body.Right as BinaryExpression;
            Assert.IsNotNull(left);
            Assert.IsNotNull(right);
            Assert.AreEqual(ExpressionType.Equal, left.NodeType);
            Assert.AreEqual(ExpressionType.Equal, right.NodeType);
            Assert.AreEqual(nameof(FluentCompany.CompanyId), ((MemberExpression)left.Left).Member.Name);
            Assert.AreEqual(nameof(FluentEmployee.CompanyId), ((MemberExpression)left.Right).Member.Name);
            Assert.AreEqual(nameof(FluentCompany.DivisionId), ((MemberExpression)right.Left).Member.Name);
            Assert.AreEqual(nameof(FluentEmployee.DivisionId), ((MemberExpression)right.Right).Member.Name);
        }

        [TestMethod]
        public void Fluent_HasOneRow_Produces_OuterApply_Navigation_Metadata()
        {
            using var dbc = new OrmDbContext();
            // touch the model so OnModelCreating runs
            dbc.CreateQuery<FluentAuthor>();

            var author = dbc.GetEntityMetadata<FluentAuthor>();
            Assert.IsNotNull(author, "FluentAuthor metadata should be registered");

            Assert.IsTrue(author.Navigations.TryGetValue(nameof(FluentAuthor.LatestBook), out var nav), "LatestBook navigation should exist");
            // HasOneRow registers a single-valued navigation; the engine maps a null join condition to OUTER APPLY
            Assert.AreEqual(NavigationType.ToSingleChild, nav.NavigationType);
            Assert.IsNull(nav.JoinCondition, "OUTER APPLY navigation carries no separate join condition; the correlation lives in the subquery");

            // JoinedSource is the correlated subquery: (FluentAuthor) => IQueryable<FluentBook>
            Assert.IsNotNull(nav.JoinedSource, "JoinedSource should be set");
            Assert.AreEqual(1, nav.JoinedSource.Parameters.Count);
            Assert.AreEqual(typeof(FluentAuthor), nav.JoinedSource.Parameters[0].Type);
            Assert.AreEqual(typeof(IQueryable<FluentBook>), nav.JoinedSource.Body.Type);
        }

        [TestMethod]
        public void Fluent_HasOneRow_Navigation_Translates_To_OuterApply()
        {
            using var dbc = new OrmDbContext();
            var authors = dbc.CreateQuery<FluentAuthor>();
            // navigate the OUTER APPLY single-row navigation
            var q = authors.Select(a => new { a.Id, LatestBookTitle = a.LatestBook.Title });
            var queryResult = dbc.TranslateToSql(q);
            Console.WriteLine(queryResult);
            string expectedResult = @"
SELECT t1.Id AS Id, t2.Title AS LatestBookTitle
FROM dbo.AUTHOR AS t1
OUTER APPLY (
	SELECT TOP (1) t3.Id AS Id, t3.BOOK_TITLE AS Title, t3.AuthorId AS AuthorId, t3.Year AS Year
	FROM BOOK AS t3
	WHERE (t3.AuthorId = t1.Id)
	ORDER BY t3.Year DESC
) AS t2
";
            ValidateQueryResults(queryResult, expectedResult);
        }

        [TestMethod]
        public void Fluent_CompositeKey_CountMismatch_Throws_AtModelBuild()
        {
            // mismatched key counts: parent has 2 keys, child selector provides 1
            var ex = Assert.ThrowsException<InvalidOperationException>(ConfigureMismatchedCompositeKey);
            StringAssert.Contains(ex.Message, "Composite key mismatch");
        }

        private static void ConfigureMismatchedCompositeKey()
        {
            var metadataBuilder = new ComponentAnnotationMetadataBuilder(new OrmReflectionService());
            var builder = new ModelBuilder(metadataBuilder, new OrmModel(metadataBuilder));
            builder.Entity<FluentCompany>(e =>
                e.HasMany(x => x.Employees,
                    parentKey: c => new { c.CompanyId, c.DivisionId },
                    childKey: emp => emp.CompanyId));
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

        // ---------------------------------------------------------------------------------------------
        // Key-based reads: DataContext.GetEntity / GetRequiredEntity and their asynchronous counterparts.
        //
        // The read tests seed the database first and then Assert.Inconclusive if the table is still empty,
        // rather than returning quietly. A silent `return` reports green for a test that exercised nothing,
        // which is exactly how an emptied database would hide a real regression here.
        // ---------------------------------------------------------------------------------------------

        private static async Task<OrmDbContext> SeededContextAsync()
        {
            await new TestDatabaseSetup("Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True")
                    .SetupAsync();
            return new OrmDbContext();
        }

        [TestMethod]
        public async Task GetEntity_single_primary_key()
        {
            using var dbc = await SeededContextAsync();
            var employeeId = dbc.CreateQuery<TestEntities.Employee>().Select(x => (int?)x.EmployeeId).FirstOrDefault();
            if (employeeId == null)
                Assert.Inconclusive("No Employee rows to read; the key-based read was not exercised.");

            // A single-column key takes the bare value, and the named form means the same thing.
            var bare = dbc.GetEntity<TestEntities.Employee>(employeeId.Value);
            var named = dbc.GetEntity<TestEntities.Employee>(new { EmployeeId = employeeId.Value });

            Assert.IsNotNull(bare);
            Assert.IsNotNull(named);
            Assert.AreEqual(employeeId.Value, bare.EmployeeId);
            Assert.AreEqual(employeeId.Value, named.EmployeeId);
        }

        [TestMethod]
        public async Task GetEntityAsync_single_primary_key()
        {
            using var dbc = await SeededContextAsync();
            var employeeId = await dbc.CreateQuery<TestEntities.Employee>().Select(x => (int?)x.EmployeeId).FirstOrDefaultAsync();
            if (employeeId == null)
                Assert.Inconclusive("No Employee rows to read; the key-based read was not exercised.");

            var employee = await dbc.GetEntityAsync<TestEntities.Employee>(employeeId.Value);

            Assert.IsNotNull(employee);
            Assert.AreEqual(employeeId.Value, employee.EmployeeId);
        }

        [TestMethod]
        public async Task GetEntity_composite_primary_key()
        {
            using var dbc = await SeededContextAsync();
            var skill = dbc.CreateQuery<TestEntities.EmployeeSkill>().FirstOrDefault();
            if (skill == null)
                Assert.Inconclusive("No EmployeeSkill rows to read; the composite-key read was not exercised.");

            var retrieved = dbc.GetEntity<TestEntities.EmployeeSkill>(
                                new { skill.SkillId, skill.EmployeeId });

            Assert.IsNotNull(retrieved);
            Assert.AreEqual(skill.SkillId, retrieved.SkillId);
            Assert.AreEqual(skill.EmployeeId, retrieved.EmployeeId);
        }

        [TestMethod]
        public async Task GetEntityAsync_composite_primary_key()
        {
            using var dbc = await SeededContextAsync();
            var skill = await dbc.CreateQuery<TestEntities.EmployeeSkill>().FirstOrDefaultAsync();
            if (skill == null)
                Assert.Inconclusive("No EmployeeSkill rows to read; the composite-key read was not exercised.");

            var retrieved = await dbc.GetEntityAsync<TestEntities.EmployeeSkill>(
                                    new { skill.SkillId, skill.EmployeeId });

            Assert.IsNotNull(retrieved);
            Assert.AreEqual(skill.SkillId, retrieved.SkillId);
            Assert.AreEqual(skill.EmployeeId, retrieved.EmployeeId);
        }

        [TestMethod]
        public async Task GetEntity_composite_key_ignores_the_order_the_key_is_written_in()
        {
            using var dbc = await SeededContextAsync();
            var skill = dbc.CreateQuery<TestEntities.EmployeeSkill>().FirstOrDefault();
            if (skill == null)
                Assert.Inconclusive("No EmployeeSkill rows to read; the composite-key read was not exercised.");

            // This is the whole point of binding by name. SkillId and EmployeeId are both int, so a
            // positional API could not tell these two calls apart — one of them would silently read the
            // wrong row, or no row at all.
            var declaredOrder = dbc.GetEntity<TestEntities.EmployeeSkill>(
                                    new { skill.SkillId, skill.EmployeeId });
            var reversedOrder = dbc.GetEntity<TestEntities.EmployeeSkill>(
                                    new { skill.EmployeeId, skill.SkillId });

            Assert.IsNotNull(declaredOrder);
            Assert.IsNotNull(reversedOrder);
            Assert.AreEqual(declaredOrder.SkillId, reversedOrder.SkillId);
            Assert.AreEqual(declaredOrder.EmployeeId, reversedOrder.EmployeeId);
        }

        [TestMethod]
        public async Task GetEntity_accepts_a_whole_entity_as_the_key()
        {
            using var dbc = await SeededContextAsync();
            var skill = dbc.CreateQuery<TestEntities.EmployeeSkill>().FirstOrDefault();
            if (skill == null)
                Assert.Inconclusive("No EmployeeSkill rows to read; the composite-key read was not exercised.");

            // Non-key properties are ignored, so a row can be re-read from the entity it produced.
            var reread = dbc.GetEntity<TestEntities.EmployeeSkill>(skill);

            Assert.IsNotNull(reread);
            Assert.AreEqual(skill.SkillId, reread.SkillId);
            Assert.AreEqual(skill.EmployeeId, reread.EmployeeId);
        }

        [TestMethod]
        public async Task GetEntity_returns_null_when_no_row_matches()
        {
            using var dbc = await SeededContextAsync();

            Assert.IsNull(dbc.GetEntity<TestEntities.Employee>(-1));
            Assert.IsNull(await dbc.GetEntityAsync<TestEntities.Employee>(-1));
            Assert.IsNull(dbc.GetEntity<TestEntities.EmployeeSkill>(new { SkillId = -1, EmployeeId = -1 }));
        }

        [TestMethod]
        public async Task GetRequiredEntity_returns_entity_when_found()
        {
            using var dbc = await SeededContextAsync();
            var employeeId = dbc.CreateQuery<TestEntities.Employee>().Select(x => (int?)x.EmployeeId).FirstOrDefault();
            if (employeeId == null)
                Assert.Inconclusive("No Employee rows to read; the key-based read was not exercised.");

            Assert.AreEqual(employeeId.Value, dbc.GetRequiredEntity<TestEntities.Employee>(employeeId.Value).EmployeeId);

            var async = await dbc.GetRequiredEntityAsync<TestEntities.Employee>(employeeId.Value);
            Assert.AreEqual(employeeId.Value, async.EmployeeId);
        }

        [TestMethod]
        public async Task GetRequiredEntity_should_throw_record_not_found_exception()
        {
            using var ctx = await SeededContextAsync();

            var ex = Assert.ThrowsException<RecordNotFoundException>(
                () => ctx.GetRequiredEntity<TestEntities.Employee>(-1));

            // The type and the key are exposed as data, so callers never have to parse the message.
            Assert.AreEqual(typeof(TestEntities.Employee), ex.EntityType);
            Assert.AreEqual(-1, ex.Key["EmployeeId"]);
            StringAssert.Contains(ex.Message, "EmployeeId = -1");
        }

        [TestMethod]
        public async Task GetRequiredEntityAsync_should_throw_record_not_found_exception()
        {
            using var ctx = await SeededContextAsync();

            var ex = await Assert.ThrowsExceptionAsync<RecordNotFoundException>(
                () => ctx.GetRequiredEntityAsync<TestEntities.EmployeeSkill>(new { SkillId = -1, EmployeeId = -2 }));

            Assert.AreEqual(typeof(TestEntities.EmployeeSkill), ex.EntityType);
            Assert.AreEqual(-1, ex.Key["SkillId"]);
            Assert.AreEqual(-2, ex.Key["EmployeeId"]);
        }

        [TestMethod]
        public void GetEntity_should_reject_a_composite_key_given_as_a_bare_value()
        {
            using var ctx = new OrmDbContext();

            // EmployeeSkill is keyed on (SkillId, EmployeeId); a lone value cannot say which it is.
            var ex = Assert.ThrowsException<ArgumentException>(
                () => ctx.GetEntity<TestEntities.EmployeeSkill>(1));

            StringAssert.Contains(ex.Message, "SkillId");
            StringAssert.Contains(ex.Message, "EmployeeId");
        }

        [TestMethod]
        public void GetEntity_should_reject_a_key_missing_a_column()
        {
            using var ctx = new OrmDbContext();

            // A misspelt key property leaves the column it was meant to supply with nothing supplying it,
            // which is what turns a typo into an error rather than a wrong row.
            var ex = Assert.ThrowsException<ArgumentException>(
                () => ctx.GetEntity<TestEntities.EmployeeSkill>(new { SkilId = 1, EmployeeId = 2 }));

            StringAssert.Contains(ex.Message, "SkillId");
        }

        [TestMethod]
        public void GetEntity_should_reject_a_key_value_of_the_wrong_type()
        {
            using var ctx = new OrmDbContext();

            var ex = Assert.ThrowsException<ArgumentException>(
                () => ctx.GetEntity<TestEntities.Employee>("not-an-int"));

            StringAssert.Contains(ex.Message, "EmployeeId");
            StringAssert.Contains(ex.Message, "String");
        }

        [TestMethod]
        public async Task GetEntity_should_reject_a_null_key()
        {
            using var ctx = new OrmDbContext();

            Assert.ThrowsException<ArgumentNullException>(
                () => ctx.GetEntity<TestEntities.Employee>(null));
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => ctx.GetEntityAsync<TestEntities.Employee>(null));
        }

        [TestMethod]
        public void GetEntity_should_throw_when_entity_has_no_primary_key()
        {
            using var ctx = new OrmDbContext();

            // EmployeeDegree is [DbTable] but declares no [PrimaryKey], so there is nothing to match on.
            Assert.ThrowsException<InvalidOperationException>(
                () => ctx.GetEntity<EmployeeDegree>(Guid.NewGuid()));
        }

        [TestMethod]
        public async Task FirstOrDefaultAsync_pushes_the_row_limit_into_the_expression_tree()
        {
            // The row limit has to reach the translator as a FirstOrDefault node — that is what makes
            // FirstOrDefaultQueryMethodExpressionConverter emit TOP 1. Taking the first row from the
            // materialized sequence instead would leave the statement unbounded, so this asserts on the
            // tree handed to the provider rather than on the row that comes back.
            var provider = new RecordingAsyncQueryProvider();
            var query = new RecordingQueryable<TestEntities.Employee>(
                provider, Expression.Constant(null, typeof(IQueryable<TestEntities.Employee>)));

            await query.FirstOrDefaultAsync();

            var call = provider.LastExpression as MethodCallExpression;
            Assert.IsNotNull(call, "FirstOrDefaultAsync should hand the provider a method call, not the bare query.");
            // Fully qualified: this test project declares its own `Queryable`, which shadows System.Linq's.
            Assert.AreEqual(nameof(System.Linq.Queryable.FirstOrDefault), call.Method.Name);
            Assert.AreEqual(typeof(System.Linq.Queryable), call.Method.DeclaringType);

            // Task<T>, not IAsyncEnumerable<T>: the single row is produced by the database, not by reading
            // one item off a streamed sequence.
            Assert.AreEqual(typeof(Task<TestEntities.Employee>), provider.LastResultType);
        }

        /// <summary>
        ///     Records the expression and result type a terminal operator hands to the provider, so tests can
        ///     assert on the shape of the tree that reaches translation without needing a database.
        /// </summary>
        private sealed class RecordingAsyncQueryProvider : IAsyncQueryProvider
        {
            public Expression LastExpression { get; private set; }
            public Type LastResultType { get; private set; }

            public IQueryable CreateQuery(Expression expression) => throw new NotSupportedException();
            public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
                => new RecordingQueryable<TElement>(this, expression);
            public object Execute(Expression expression) => throw new NotSupportedException();
            public TResult Execute<TResult>(Expression expression) => throw new NotSupportedException();

            public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
            {
                this.LastExpression = expression;
                this.LastResultType = typeof(TResult);
                // Only the expected shape gets a real result. Returning default for anything else lets the
                // assertions report what changed, instead of the test dying on a cast.
                return typeof(TResult) == typeof(Task<TestEntities.Employee>)
                        ? (TResult)(object)Task.FromResult<TestEntities.Employee>(null)
                        : default;
            }
        }

        private sealed class RecordingQueryable<T> : IQueryable<T>
        {
            public RecordingQueryable(IQueryProvider provider, Expression expression)
            {
                this.Provider = provider;
                this.Expression = expression;
            }

            public Type ElementType => typeof(T);
            public Expression Expression { get; }
            public IQueryProvider Provider { get; }
            public IEnumerator<T> GetEnumerator() => throw new NotSupportedException();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => throw new NotSupportedException();
        }
    }
}
