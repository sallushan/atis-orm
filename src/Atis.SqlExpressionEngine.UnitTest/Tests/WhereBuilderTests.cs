using System.Linq.Expressions;

using Atis.Expressions;
using Atis.Orm;
using Atis.Orm.Abstractions;
using Atis.Orm.Preprocessing;
using Atis.Orm.Querying;
using Atis.Orm.Services;
using Atis.Orm.SqlServer;
using Atis.Orm.Translation;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.Exceptions;
using Atis.SqlExpressionEngine.ExpressionConverters;
using Atis.SqlExpressionEngine.Services;
using Atis.SqlExpressionEngine.UnitTest.Converters;
using Atis.SqlExpressionEngine.UnitTest.Preprocessors;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         Exercises optional WHERE terms: <see cref="WhereBuilder.Equal{T}"/> emits a term that is dropped
    ///         from the statement when its value is null at execution time.
    ///     </para>
    ///     <para>
    ///         The properties the whole design rests on are asserted here, because each of them degrades
    ///         <em>silently</em>: one compiled query must serve every combination of supplied and omitted
    ///         values, and a cache hit must rebind the value rather than reuse the first execution's.
    ///     </para>
    /// </summary>
    [TestClass]
    public class WhereBuilderTests : TestBase
    {
        private static ISqlCommandRenderer CreateRenderer()
        {
            var nameGenerator = new SqlDbParameterNameGenerator();
            return new SqlCommandRenderer(new SqlDbParameterFactory(nameGenerator));
        }

        private SqlTranslationResult TranslateWithSqlServer(Expression queryExpression)
        {
            var sqlExpression = ConvertExpressionToSqlExpression(queryExpression, out _);
            Assert.IsNotNull(sqlExpression, "Expression should convert to a SQL expression.");
            return new SqlServerSqlExpressionTranslator().Translate(sqlExpression);
        }

        [TestMethod]
        public void Equal_renders_an_anchored_group_holding_the_predicate()
        {
            var department = "IT";
            var employees = new Queryable<Employee>(this.queryProvider);
            var q = employees.Where(x => WhereBuilder.Equal(x.Department, department));

            var translation = this.TranslateWithSqlServer(q.Expression);
            var rendered = CreateRenderer().Render(translation.Fragments, p => p.InitialValue);

            // The `1 = 1` anchor is load-bearing: it makes the term valid boolean SQL in both states, so the
            // surrounding predicate needs no separator bookkeeping when the term disappears.
            StringAssert.Contains(rendered.Sql, "(1 = 1 AND (t1.Department = @p0))");
            Assert.AreEqual(1, rendered.DbParameters.Count);
        }

        [TestMethod]
        public void A_query_with_an_optional_term_re_renders_per_execution()
        {
            var wiring = new Wiring();
            var compiled = wiring.Compiler.Compile(wiring.BuildOptionalEqualQuery("IT"));

            // Rendering once at compile time would freeze whichever combination the first caller supplied,
            // and SimpleCompiledQuery's parameter list stops being a 1:1 placeholder plan once a span can vanish.
            Assert.IsInstanceOfType(compiled, typeof(ExpandableCompiledQuery),
                "An optional term makes the SQL text value-dependent, so it must re-render per execution.");
        }

        [TestMethod]
        public void The_term_is_present_when_the_value_is_supplied_and_gone_when_it_is_null()
        {
            // The feature, and the reason the guard is resolved at render time rather than at preprocess time:
            // ONE compiled query serves both executions.
            var wiring = new Wiring();
            var compiled = wiring.Compiler.Compile(wiring.BuildOptionalEqualQuery("IT"));

            var withValue = compiled.GetExecutionContext(wiring.ValuesByIdentity("Finance"), useInitialValues: false);
            StringAssert.Contains(withValue.Sql, "t1.Department = @p0", "A supplied value keeps the term.");
            Assert.AreEqual(1, withValue.DbParameters.Count);
            Assert.AreEqual("Finance", withValue.DbParameters[0].Value, "The term binds this execution's value.");

            var withoutValue = compiled.GetExecutionContext(wiring.ValuesByIdentity(null), useInitialValues: false);
            // The column still appears in the SELECT list, so the comparison is what must be gone.
            Assert.IsFalse(withoutValue.Sql.Contains("t1.Department ="), "A null value drops the term entirely.");
            StringAssert.Contains(withoutValue.Sql, "1 = 1", "The anchor stays, so the WHERE clause is still valid.");
            Assert.AreEqual(0, withoutValue.DbParameters.Count, "A dropped term binds no parameters.");
        }

        [TestMethod]
        public void Two_different_values_do_not_reuse_the_first_execution_s_value()
        {
            // Guards against the naive port: building the predicate around Expression.Constant(value) at
            // preprocess time makes it a literal, and a literal is frozen to its initial value on every cache
            // hit - so the first caller's search value would silently serve every later caller.
            var wiring = new Wiring();
            var compiled = wiring.Compiler.Compile(wiring.BuildOptionalEqualQuery("IT"));

            var first = compiled.GetExecutionContext(wiring.ValuesByIdentity("Finance"), useInitialValues: false);
            var second = compiled.GetExecutionContext(wiring.ValuesByIdentity("Legal"), useInitialValues: false);

            Assert.AreEqual("Finance", first.DbParameters[0].Value);
            Assert.AreEqual("Legal", second.DbParameters[0].Value);
        }

        [TestMethod]
        public void An_empty_string_is_a_value_not_an_absent_one()
        {
            // "No value" means null and only null. The alternative (treating whitespace as absent, which the
            // old library's eager bodies did) cannot express "find the rows with an empty department".
            var wiring = new Wiring();
            var compiled = wiring.Compiler.Compile(wiring.BuildOptionalEqualQuery("IT"));

            var ctx = compiled.GetExecutionContext(wiring.ValuesByIdentity(string.Empty), useInitialValues: false);

            StringAssert.Contains(ctx.Sql, "t1.Department = @p0");
            Assert.AreEqual(string.Empty, ctx.DbParameters[0].Value);
        }

        [TestMethod]
        public void Two_optional_terms_drop_independently()
        {
            var wiring = new Wiring();
            var compiled = wiring.Compiler.Compile(wiring.BuildTwoOptionalTermsQuery("IT", "E1"));

            var bothDropped = compiled.GetExecutionContext(wiring.TwoValuesByIdentity(null, null), useInitialValues: false);
            Assert.AreEqual(0, bothDropped.DbParameters.Count, "Both terms drop, leaving only the anchors.");

            var onlySecond = compiled.GetExecutionContext(wiring.TwoValuesByIdentity(null, "E2"), useInitialValues: false);
            Assert.AreEqual(1, onlySecond.DbParameters.Count, "Only the term whose value survives is emitted.");
            Assert.AreEqual("E2", onlySecond.DbParameters[0].Value);
            Assert.IsFalse(onlySecond.Sql.Contains("t1.Department ="), "The first term is gone.");
            StringAssert.Contains(onlySecond.Sql, "t1.EmployeeId =", "The second term survives.");
        }

        [TestMethod]
        public void The_four_like_forms_each_decorate_the_pattern_differently()
        {
            var pattern = "abc";
            var employees = new Queryable<Employee>(this.queryProvider);

            AssertRenders(employees.Where(x => WhereBuilder.Contains(x.Department, pattern)),
                          "(t1.Department LIKE '%' + @p0 + '%')");
            AssertRenders(employees.Where(x => WhereBuilder.StartsWith(x.Department, pattern)),
                          "(t1.Department LIKE @p0 + '%')");
            AssertRenders(employees.Where(x => WhereBuilder.EndsWith(x.Department, pattern)),
                          "(t1.Department LIKE '%' + @p0)");
            // LikePattern is the only form with no BCL spelling: the caller's wildcards are used as-is, so
            // this is the one that behaves like SQL's own LIKE.
            AssertRenders(employees.Where(x => WhereBuilder.LikePattern(x.Department, pattern)),
                          "(t1.Department LIKE @p0)");

            void AssertRenders(IQueryable<Employee> query, string expectedPredicate)
            {
                var translation = this.TranslateWithSqlServer(query.Expression);
                var rendered = CreateRenderer().Render(translation.Fragments, p => p.InitialValue);
                StringAssert.Contains(rendered.Sql, "(1 = 1 AND " + expectedPredicate + ")");
            }
        }

        [TestMethod]
        public void In_drops_on_an_empty_collection_not_just_on_null()
        {
            // An empty list means "no filter", not "match nothing". This is the one place the guard needs to
            // look past null - and it is opted into per term, never inferred from the value.
            var wiring = new Wiring();
            var compiled = wiring.Compiler.Compile(wiring.BuildInQuery(new[] { "IT" }));

            var withValues = compiled.GetExecutionContext(wiring.InValuesByIdentity(new[] { "IT", "HR" }), useInitialValues: false);
            StringAssert.Contains(withValues.Sql, "t1.Department IN");
            Assert.AreEqual(2, withValues.DbParameters.Count);

            var empty = compiled.GetExecutionContext(wiring.InValuesByIdentity(new string[0]), useInitialValues: false);
            Assert.IsFalse(empty.Sql.Contains("t1.Department IN"), "An empty collection drops the term.");
            Assert.AreEqual(0, empty.DbParameters.Count);

            var nullList = compiled.GetExecutionContext(wiring.InValuesByIdentity(null), useInitialValues: false);
            Assert.IsFalse(nullList.Sql.Contains("t1.Department IN"), "A null collection drops the term.");
        }

        [TestMethod]
        public void NotIn_negates_and_still_drops_when_absent()
        {
            var wiring = new Wiring();
            var compiled = wiring.Compiler.Compile(wiring.BuildNotInQuery(new[] { "IT" }));

            var withValues = compiled.GetExecutionContext(wiring.NotInValuesByIdentity(new[] { "IT" }), useInitialValues: false);
            StringAssert.Contains(withValues.Sql, "NOT ");
            StringAssert.Contains(withValues.Sql, "t1.Department IN");

            // The old library's eager NotStringIn(x, null) returned false (match nothing) while its expression
            // form emitted 1 = 1 (match everything). Here absent means no filter, like every other method.
            var empty = compiled.GetExecutionContext(wiring.NotInValuesByIdentity(new string[0]), useInitialValues: false);
            Assert.IsFalse(empty.Sql.Contains("t1.Department IN"), "An empty collection drops the term.");
        }

        [TestMethod]
        public void DateRange_shifts_its_upper_bound_in_SQL_and_drops_each_bound_independently()
        {
            var wiring = new Wiring();
            var compiled = wiring.Compiler.Compile(wiring.BuildDateRangeQuery(new DateTime(2020, 1, 1), new DateTime(2020, 12, 31)));

            var both = compiled.GetExecutionContext(
                wiring.DateRangeValuesByIdentity(new DateTime(2021, 1, 1), new DateTime(2021, 6, 30)), useInitialValues: false);

            // The day arithmetic is SQL, not C#, so the bound rebinds instead of freezing to the first date.
            // (DATEADD's interval is a bound literal too, hence three parameters rather than two.)
            StringAssert.Contains(both.Sql, "DATEADD(", "The upper bound is shifted in SQL.");
            var boundValues = both.DbParameters.Select(p => p.Value).ToList();
            CollectionAssert.Contains(boundValues, (object)new DateTime(2021, 1, 1), "The lower bound is the caller's value, untransformed.");
            CollectionAssert.Contains(boundValues, (object)new DateTime(2021, 6, 30), "The upper bound is the caller's value, untransformed.");

            var onlyFrom = compiled.GetExecutionContext(
                wiring.DateRangeValuesByIdentity(new DateTime(2021, 1, 1), null), useInitialValues: false);
            Assert.AreEqual(1, onlyFrom.DbParameters.Count, "Only the lower bound survives - the shift went with the dropped term.");
            Assert.IsFalse(onlyFrom.Sql.Contains("DATEADD("), "The upper bound is gone entirely.");

            var neither = compiled.GetExecutionContext(
                wiring.DateRangeValuesByIdentity(null, null), useInitialValues: false);
            Assert.AreEqual(0, neither.DbParameters.Count, "With no bounds the whole range disappears.");
        }

        [TestMethod]
        public void The_column_side_can_be_any_translatable_expression()
        {
            // "column" is shorthand: the left side is an ordinary expression and gets no special treatment,
            // so a coalesce, a concatenation or a calculated property all work.
            var department = "IT";
            var employees = new Queryable<Employee>(this.queryProvider);
            var q = employees.Where(x => WhereBuilder.Equal(x.Department ?? x.Name, department));

            var translation = this.TranslateWithSqlServer(q.Expression);
            var rendered = CreateRenderer().Render(translation.Fragments, p => p.InitialValue);

            StringAssert.Contains(rendered.Sql, "1 = 1 AND (COALESCE(t1.Department, t1.Name) = @p0)");
            Assert.AreEqual(1, rendered.DbParameters.Count);
        }

        [TestMethod]
        public void Compiling_with_a_null_value_does_not_bake_IS_NULL_into_the_cached_query()
        {
            // The natural first execution of a search screen is "field left blank", so this is the likely
            // path, not an edge case. TranslateBinary folds `col == <parameter that is null right now>` to
            // `col IS NULL` and emits no placeholder; inside an optional term that would be doubly wrong,
            // because the span is only ever rendered when the guard HAS a value.
            var wiring = new Wiring();
            var compiled = wiring.Compiler.Compile(wiring.BuildOptionalEqualQuery(null));

            var withValue = compiled.GetExecutionContext(wiring.ValuesByIdentity("Finance"), useInitialValues: false);

            Assert.IsFalse(withValue.Sql.Contains("IS NULL"), "The comparison must not have folded to IS NULL.");
            StringAssert.Contains(withValue.Sql, "t1.Department = @p0");
            Assert.AreEqual(1, withValue.DbParameters.Count, "The value must still be bound.");
            Assert.AreEqual("Finance", withValue.DbParameters[0].Value);
        }

        [TestMethod]
        public void An_optional_term_nested_inside_another_is_rejected()
        {
            // Optional terms are joined with AND and sit beside each other; one inside another's predicate has
            // no meaning. It is barely reachable - the outer call has to be over bool for the type arguments to
            // unify at all, so a WhereBuilder call lands in the *column* position - but it compiles, so it gets
            // a named error rather than odd SQL.
            var flag = true;
            var department = "IT";
            var employees = new Queryable<Employee>(this.queryProvider);
            var q = employees.Where(x => WhereBuilder.Equal(WhereBuilder.Equal(x.Department, department), flag));

            var ex = Assert.ThrowsException<InvalidOperationException>(() => this.TranslateWithSqlServer(q.Expression));

            StringAssert.Contains(ex.Message, "cannot contain another optional term");
        }

        [TestMethod]
        public void Calling_a_marker_method_directly_throws()
        {
            // The methods are markers rewritten before they run. Calling one in memory - which is exactly what
            // list.AsQueryable().Where(sameLambda) does - reaches the body, and it must say so rather than
            // silently returning a value that disagrees with the SQL path.
            var ex = Assert.ThrowsException<DirectCallNotSupportedException>(() => WhereBuilder.Equal("a", "b"));

            Assert.AreEqual($"{nameof(WhereBuilder)}.{nameof(WhereBuilder.Equal)}", ex.MethodName);
            StringAssert.Contains(ex.Message, "marker method");
        }

        [TestMethod]
        public async Task Optional_term_executes_and_drops_against_a_real_database()
        {
            // Asserts ROWS, not SQL, and in both directions from a single query shape - the verification that
            // matters most, because a wrongly-dropped term still produces perfectly valid SQL.
            var setup = new TestDatabaseSetup("Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True");
            await setup.SetupAsync();

            using var db = new OrmDbContext();

            // Supplied value: employees 3, 9 and 16 are the seeded rows in department 3.
            int? departmentId = 3;
            var filtered = await db.CreateQuery<TestEntities.Employee>()
                                   .Where(x => WhereBuilder.Equal(x.DepartmentId, departmentId))
                                   .ToListAsync();
            Assert.AreEqual(3, filtered.Count, "Three seeded employees are in department 3.");
            Assert.IsTrue(filtered.All(x => x.DepartmentId == 3));

            // Same query shape, cache hit, no value: the term disappears and every row comes back.
            departmentId = null;
            var unfiltered = await db.CreateQuery<TestEntities.Employee>()
                                     .Where(x => WhereBuilder.Equal(x.DepartmentId, departmentId))
                                     .ToListAsync();
            Assert.AreEqual(25, unfiltered.Count, "With no value the term is omitted, so nothing is filtered out.");

            // And back again, with a different value, to prove the drop did not poison the cached query and
            // that the value is genuinely rebound rather than replayed from the first execution.
            departmentId = 9;
            var refiltered = await db.CreateQuery<TestEntities.Employee>()
                                     .Where(x => WhereBuilder.Equal(x.DepartmentId, departmentId))
                                     .ToListAsync();
            Assert.AreEqual(1, refiltered.Count, "One seeded employee is in department 9.");
            Assert.AreEqual(9, refiltered[0].DepartmentId);
        }

        // Wires the ORM pipeline (SQL Server dialect) without a database, so the compile -> cache-hit rebind
        // path can be driven directly. Mirrors InValuesExpansionTests.Wiring.
        private sealed class Wiring
        {
            private readonly ExpressionVariableValuesExtractor extractor;
            private readonly QueryProvider probeProvider = new QueryProvider();

            public QueryCompiler Compiler { get; }

            public Wiring()
            {
                var expressionEvaluator = new ExpressionEvaluator();
                var reflectionService = new OrmReflectionService();
                var identityProvider = new VariableIdentityProvider();
                this.extractor = new ExpressionVariableValuesExtractor(expressionEvaluator, identityProvider);
                var sqlDataTypeFactory = new SqlDataTypeFactory();
                var parameterMapper = new LambdaParameterToDataSourceMapper();
                var sqlFactory = new SqlExpressionFactory();
                var logger = new Services.Logger();
                var model = new Services.Model(reflectionService);
                var serviceCollection = new object[] { sqlDataTypeFactory, sqlFactory, model, parameterMapper, reflectionService, logger, expressionEvaluator };
                var converterServiceProvider = new ExpressionConverterDependencyProviderByCollection(serviceCollection);
                var factoryProvider = new LinqToSqlConverterFactoryProvider(reflectionService, expressionEvaluator, new VariableIdentityProvider(), userProvidedFactories: [new SqlFunctionConverterFactory()]);
                var treeConverter = new LinqToSqlExpressionTreeConverter(converterServiceProvider, factoryProvider);
                var preprocessor = new OrmExpressionPreprocessorProvider(model, reflectionService, expressionEvaluator, plugins: new[] { new CustomBusinessMethodPreprocessor() });
                var linqToSqlConverter = new LinqToSqlConverter(treeConverter, new SqlExpressionPostprocessorProvider(postprocessors: []));
                var sqlExpressionTranslator = new SqlServerSqlExpressionTranslator();
                var nameGenerator = new SqlDbParameterNameGenerator();
                var dbParameterFactory = new SqlDbParameterFactory(nameGenerator);
                var commandRenderer = new SqlCommandRenderer(dbParameterFactory);
                var elementFactoryBuilder = new ElementFactoryBuilder();
                var queryTranslator = new QueryTranslator(preprocessor, linqToSqlConverter, sqlExpressionTranslator, logger);
                this.Compiler = new QueryCompiler(queryTranslator, commandRenderer, dbParameterFactory, elementFactoryBuilder);
            }

            // Identical shape (and captured-variable identity) across calls, differing only in the value -
            // exactly the cache-key-equal / value-different case.
            public Expression BuildOptionalEqualQuery(string department)
            {
                var employees = new Queryable<Employee>(this.probeProvider);
                return employees.Where(x => WhereBuilder.Equal(x.Department, department)).Expression;
            }

            public Expression BuildTwoOptionalTermsQuery(string department, string employeeId)
            {
                var employees = new Queryable<Employee>(this.probeProvider);
                return employees.Where(x => WhereBuilder.Equal(x.Department, department)
                                         && WhereBuilder.Equal(x.EmployeeId, employeeId)).Expression;
            }

            public Expression BuildInQuery(string[] departments)
            {
                var employees = new Queryable<Employee>(this.probeProvider);
                return employees.Where(x => WhereBuilder.In(x.Department, departments)).Expression;
            }

            public Expression BuildNotInQuery(string[] departments)
            {
                var employees = new Queryable<Employee>(this.probeProvider);
                return employees.Where(x => WhereBuilder.NotIn(x.Department, departments)).Expression;
            }

            public Expression BuildDateRangeQuery(DateTime? from, DateTime? to)
            {
                var students = new Queryable<Student>(this.probeProvider);
                return students.Where(x => WhereBuilder.DateRange(x.RecordUpdateDate, from, to)).Expression;
            }

            // Re-extracts the variable values keyed by identity, as the executor does on a cache hit: straight
            // off the original tree, with no preprocessing in between.
            public IReadOnlyDictionary<string, object> ValuesByIdentity(string department)
                => this.extractor.ExtractVariableValuesByIdentity(this.BuildOptionalEqualQuery(department));

            public IReadOnlyDictionary<string, object> InValuesByIdentity(string[] departments)
                => this.extractor.ExtractVariableValuesByIdentity(this.BuildInQuery(departments));

            public IReadOnlyDictionary<string, object> NotInValuesByIdentity(string[] departments)
                => this.extractor.ExtractVariableValuesByIdentity(this.BuildNotInQuery(departments));

            public IReadOnlyDictionary<string, object> DateRangeValuesByIdentity(DateTime? from, DateTime? to)
                => this.extractor.ExtractVariableValuesByIdentity(this.BuildDateRangeQuery(from, to));

            public IReadOnlyDictionary<string, object> TwoValuesByIdentity(string department, string employeeId)
                => this.extractor.ExtractVariableValuesByIdentity(this.BuildTwoOptionalTermsQuery(department, employeeId));
        }
    }
}
