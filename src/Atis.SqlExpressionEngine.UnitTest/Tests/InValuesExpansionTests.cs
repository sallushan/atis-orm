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
using System.Linq.Expressions;

using Atis.Orm.Abstractions;
using Atis.Orm.Preprocessing;
using Atis.Orm.Querying;
using Atis.Orm.Services;
using Atis.Orm.Translation;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     Exercises collection-parameter expansion: a captured collection used in <c>IN</c> becomes one
    ///     placeholder per element, decided per execution (so a cached query rebinds to a different length),
    ///     and expansion never leaks to a multi-value parameter that sits outside a list position.
    /// </summary>
    [TestClass]
    public class InValuesExpansionTests : TestBase
    {
        private static ICommandRenderer CreateRenderer()
        {
            var nameGenerator = new SqlDbParameterNameGenerator();
            return new CommandRenderer(new SqlDbParameterFactory(nameGenerator));
        }

        // Translates with the SQL Server dialect and renders with the parameters' translation-time values.
        private RenderedCommand RenderWithSqlServer(Expression queryExpression)
            => CreateRenderer().Render(this.TranslateWithSqlServer(queryExpression).Fragments, p => p.InitialValue);

        private SqlTranslationResult TranslateWithSqlServer(Expression queryExpression)
        {
            var sqlExpression = ConvertExpressionToSqlExpression(queryExpression, out _);
            Assert.IsNotNull(sqlExpression, "Expression should convert to a SQL expression.");
            return new SqlServerSqlExpressionTranslator().Translate(sqlExpression);
        }

        // Expansion is a decision the TRANSLATOR records per position, so it is read off the fragments
        // rather than off the rendered text - the value is never what decides it.
        private static bool HasExpandableParameter(SqlTranslationResult translation)
            => translation.Fragments.OfType<ExpandableParameterCommandFragment>().Any();

        [TestMethod]
        public void Captured_collection_in_Contains_expands_to_one_placeholder_per_element()
        {
            var departments = new[] { "HR", "Finance" };
            var employees = new Queryable<Employee>(this.queryProvider);
            var q = employees.Where(x => departments.Contains(x.Department));

            var translation = this.TranslateWithSqlServer(q.Expression);
            var rendered = CreateRenderer().Render(translation.Fragments, p => p.InitialValue);

            StringAssert.Contains(rendered.Sql, "IN (@p0_1, @p0_2)");
            Assert.IsTrue(HasExpandableParameter(translation), "The IN list holds an expandable parameter.");
            Assert.AreEqual(2, rendered.DbParameters.Count, "Two elements -> two DbParameters.");
        }

        [TestMethod]
        public void Empty_captured_collection_renders_empty_list_template()
        {
            var departments = new string[0];
            var employees = new Queryable<Employee>(this.queryProvider);
            var q = employees.Where(x => departments.Contains(x.Department));

            var rendered = this.RenderWithSqlServer(q.Expression);

            // Empty IN list -> a subquery that matches nothing and also negates correctly under NOT IN.
            // An empty collection has no values, so no parameter is bound.
            StringAssert.Contains(rendered.Sql, "IN (SELECT NULL WHERE 1 = 0)");
            Assert.AreEqual(0, rendered.DbParameters.Count, "An empty collection binds no parameters.");
        }

        [TestMethod]
        public void Empty_captured_collection_in_string_Join_renders_two_nulls()
        {
            var parts = new string[0];
            var employees = new Queryable<Employee>(this.queryProvider);
            var q = employees.Select(x => new { Joined = string.Join(", ", parts) });

            var rendered = this.RenderWithSqlServer(q.Expression);

            // CONCAT_WS needs >= 2 value args and ignores NULLs, so two NULLs yield an empty string with no
            // parameter bound.
            StringAssert.Contains(rendered.Sql, "CONCAT_WS(@p0, NULL, NULL)");
            Assert.AreEqual(1, rendered.DbParameters.Count, "Only the separator is a parameter.");
        }

        [TestMethod]
        public void Multi_value_parameter_outside_a_list_position_stays_a_single_placeholder()
        {
            // A byte[] is a (non-string) IEnumerable. Emitted through the ordinary (non-list) path it must
            // remain one placeholder bound to the whole array: expansion is opted into per position by the
            // translator, never inferred from the value.
            var blobParameter = new SqlExpressionFactory().CreateParameter(new byte[] { 1, 2, 3 });

            var translation = new SqlServerSqlExpressionTranslator().Translate(blobParameter);
            var rendered = CreateRenderer().Render(translation.Fragments, p => p.InitialValue);

            Assert.AreEqual("@p0", rendered.Sql, "A non-list multi-value parameter is a single placeholder.");
            Assert.IsFalse(HasExpandableParameter(translation));
            Assert.AreEqual(1, rendered.DbParameters.Count);
        }

        [TestMethod]
        public void Cache_hit_rebinds_collection_to_a_new_length()
        {
            // The point of the feature: one compiled query, re-executed with collections of different lengths.
            var wiring = new Wiring();

            // Cache miss compiles with a 2-element array (initial values).
            var compiled = wiring.Compiler.Compile(wiring.BuildContainsQuery(new[] { 10, 20 }));
            Assert.IsInstanceOfType(compiled, typeof(ExpandableCompiledQuery), "A collection IN filter must re-render per execution.");

            // Cache hit with a 3-element array for the same variable identity.
            var threeCtx = compiled.GetExecutionContext(wiring.ValuesByIdentity(new[] { 1, 2, 3 }), useInitialValues: false);
            StringAssert.Contains(threeCtx.Sql, "IN (@p0_1, @p0_2, @p0_3)");
            Assert.AreEqual(3, threeCtx.DbParameters.Count);
            CollectionAssert.AreEqual(new[] { "@p0_1", "@p0_2", "@p0_3" }, threeCtx.DbParameters.Select(p => p.ParameterName).ToArray());

            // Cache hit with an empty array falls to the empty-list template and binds no parameters.
            var emptyCtx = compiled.GetExecutionContext(wiring.ValuesByIdentity(new int[0]), useInitialValues: false);
            StringAssert.Contains(emptyCtx.Sql, "IN (SELECT NULL WHERE 1 = 0)");
            Assert.AreEqual(0, emptyCtx.DbParameters.Count);
        }

        [TestMethod]
        public void Collection_parameter_expands_even_when_it_was_null_when_the_query_compiled()
        {
            // Expansion follows the position, not the value. If it were read off the value, a query that
            // first compiled while the collection was null would bake in a single placeholder, and the next
            // execution would bind a whole List<T> to it - which the driver rejects. No compile error, no
            // sensible runtime error, just a broken query for every caller after the first.
            var wiring = new Wiring();

            var compiled = wiring.Compiler.Compile(wiring.BuildContainsQuery(null));
            Assert.IsInstanceOfType(compiled, typeof(ExpandableCompiledQuery), "An IN list is expandable regardless of the value it compiled with.");

            // The compiling execution itself has no values, so it matches nothing.
            var nullCtx = compiled.GetExecutionContext(null, useInitialValues: true);
            StringAssert.Contains(nullCtx.Sql, "IN (SELECT NULL WHERE 1 = 0)");
            Assert.AreEqual(0, nullCtx.DbParameters.Count);

            // A later execution supplying values expands normally against the same compiled query.
            var ctx = compiled.GetExecutionContext(wiring.ValuesByIdentity(new[] { 1, 2, 3 }), useInitialValues: false);
            StringAssert.Contains(ctx.Sql, "IN (@p0_1, @p0_2, @p0_3)");
            CollectionAssert.AreEqual(new object[] { 1, 2, 3 }, ctx.DbParameters.Select(p => p.Value).ToArray());
        }

        [TestMethod]
        public void Inline_array_of_variables_rebinds_every_element_on_a_cache_hit()
        {
            // new[] { a, b }.Contains(...) - the array is inline but its elements are captured variables.
            // The array's length is fixed by the expression, so there is nothing to expand; what matters is
            // that each element stays a parameter of its own. Folding the array into one frozen value would
            // burn the first execution's a and b into every later execution - wrong rows, no error anywhere.
            var wiring = new Wiring();

            var compiled = wiring.Compiler.Compile(wiring.BuildInlineArrayQuery(10, 20));
            var ctx = compiled.GetExecutionContext(wiring.InlineArrayValuesByIdentity(30, 40), useInitialValues: false);

            Assert.AreEqual(2, ctx.DbParameters.Count, "Two array elements -> two parameters.");
            CollectionAssert.AreEqual(new object[] { 30, 40 }, ctx.DbParameters.Select(p => p.Value).ToArray(),
                "Both elements must rebind to the cache-hit execution's values.");
        }

        [TestMethod]
        public void Inline_array_mixing_a_constant_and_a_variable_freezes_only_the_constant()
        {
            // new[] { 10, b } - the two elements are genuinely different things and must translate
            // differently: 10 is part of the expression (and so of the cache key) and is frozen; b is a
            // captured variable and rebinds.
            var wiring = new Wiring();

            var compiled = wiring.Compiler.Compile(wiring.BuildMixedArrayQuery(20));
            var ctx = compiled.GetExecutionContext(wiring.MixedArrayValuesByIdentity(40), useInitialValues: false);

            Assert.AreEqual(2, ctx.DbParameters.Count);
            CollectionAssert.AreEqual(new object[] { 10, 40 }, ctx.DbParameters.Select(p => p.Value).ToArray(),
                "The constant keeps its value; the variable takes this execution's.");
        }

        [TestMethod]
        public void Empty_inline_array_is_rejected_where_the_cause_is_visible()
        {
            // `IN ()` is not valid SQL anywhere, and an inline array cannot become non-empty later. The
            // rejection belongs at the converter: without it the failure surfaces deep in a tree walk as
            // "items is empty", with nothing naming the query that caused it.
            var employees = new Queryable<Employee>(this.queryProvider);
            var q = employees.Where(x => new string[] { }.Contains(x.Department));

            var error = Assert.ThrowsException<InvalidOperationException>(() => this.RenderWithSqlServer(q.Expression));
            StringAssert.Contains(error.Message, "IN list needs at least one value");
        }

        [TestMethod]
        public void Array_created_by_length_is_rejected_rather_than_translating_its_bound()
        {
            // `new string[2]` is a NewArrayBounds node: its children are the bounds, not values. Left to the
            // ordinary array converter it would emit the bound as a value - IN ('2') - which is wrong SQL
            // that nothing else would ever catch. Rejected for every bound, not just zero: even translated
            // correctly it would only ever mean "the column equals the element default".
            var employees = new Queryable<Employee>(this.queryProvider);
            var q = employees.Where(x => new string[2].Contains(x.Department));

            var error = Assert.ThrowsException<InvalidOperationException>(() => this.RenderWithSqlServer(q.Expression));
            StringAssert.Contains(error.Message, "creates an array by length");
        }

        [TestMethod]
        public void Non_expandable_query_compiles_to_SimpleCompiledQuery_with_aligned_parameter_names()
        {
            var wiring = new Wiring();
            var compiled = wiring.Compiler.Compile(wiring.BuildScalarQuery(7));

            Assert.IsInstanceOfType(compiled, typeof(SimpleCompiledQuery), "A scalar filter renders its SQL once.");

            var ctx = compiled.GetExecutionContext(null, useInitialValues: true);
            Assert.AreEqual(1, ctx.DbParameters.Count);
            // The fast path rebinds by position; every emitted DbParameter name must appear in the cached SQL,
            // i.e. the placeholder text and the parameter names line up (index == position).
            foreach (var dbParameter in ctx.DbParameters)
                StringAssert.Contains(ctx.Sql, dbParameter.ParameterName);
        }

        [TestMethod]
        public async Task Contains_over_variable_collection_executes_and_rebinds_on_cache_hit()
        {
            var setup = new TestDatabaseSetup("Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True");
            await setup.SetupAsync();

            using var db = new OrmDbContext();

            // First execution: cache miss, compiles with a 3-element collection.
            var firstIds = new[] { 1, 2, 3 };
            var first = await db.CreateQuery<TestEntities.Employee>()
                                .Where(x => firstIds.Contains(x.EmployeeId))
                                .ToListAsync();
            Assert.AreEqual(3, first.Count, "Three seeded employees match the first id set.");

            // Second execution: cache hit (same query shape), a 2-element collection re-renders to two placeholders.
            var secondIds = new[] { 4, 5 };
            var second = await db.CreateQuery<TestEntities.Employee>()
                                 .Where(x => secondIds.Contains(x.EmployeeId))
                                 .ToListAsync();
            Assert.AreEqual(2, second.Count, "Two seeded employees match the second id set.");

            // Cache hit with an empty collection uses the empty-list template and matches nothing.
            var emptyIds = new int[0];
            var none = await db.CreateQuery<TestEntities.Employee>()
                               .Where(x => emptyIds.Contains(x.EmployeeId))
                               .ToListAsync();
            Assert.AreEqual(0, none.Count, "An empty id set matches no rows.");
        }

        // Wires the ORM pipeline (SQL Server dialect) the way ToList_test does, minus the database, so the
        // compile -> cache-hit rebind path can be driven directly.
        private sealed class Wiring
        {
            private readonly IExpressionPreprocessorProvider preprocessor;
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
                this.preprocessor = new OrmExpressionPreprocessorProvider(model, reflectionService, expressionEvaluator, plugins: new[] { new CustomBusinessMethodPreprocessor() });
                var linqToSqlConverter = new LinqToSqlConverter(treeConverter, new SqlExpressionPostprocessorProvider(postprocessors: []));
                var sqlExpressionTranslator = new SqlServerSqlExpressionTranslator();
                var nameGenerator = new SqlDbParameterNameGenerator();
                var dbParameterFactory = new SqlDbParameterFactory(nameGenerator);
                var commandRenderer = new CommandRenderer(dbParameterFactory);
                var elementFactoryBuilder = new ElementFactoryBuilder();
                var queryTranslator = new QueryTranslator(this.preprocessor, linqToSqlConverter, sqlExpressionTranslator, logger);
                this.Compiler = new QueryCompiler(queryTranslator, commandRenderer, dbParameterFactory, elementFactoryBuilder);
            }

            // A fresh query capturing `ids`; identical shape (and captured-variable identity) across calls,
            // differing only in the array value - exactly the cache-key-equal / value-different case.
            public Expression BuildContainsQuery(int[] ids)
            {
                var employees = new Queryable<TestEntities.Employee>(this.probeProvider);
                return employees.Where(x => ids.Contains(x.EmployeeId)).Expression;
            }

            // An inline array whose elements are captured variables. Same shape (and same element
            // identities) across calls, differing only in the values.
            public Expression BuildInlineArrayQuery(int first, int second)
            {
                var employees = new Queryable<TestEntities.Employee>(this.probeProvider);
                return employees.Where(x => new[] { first, second }.Contains(x.EmployeeId)).Expression;
            }

            // An inline array holding one constant and one captured variable.
            public Expression BuildMixedArrayQuery(int second)
            {
                var employees = new Queryable<TestEntities.Employee>(this.probeProvider);
                return employees.Where(x => new[] { 10, second }.Contains(x.EmployeeId)).Expression;
            }

            // A non-expandable query (a scalar variable), so the compiler picks SimpleCompiledQuery.
            public Expression BuildScalarQuery(int id)
            {
                var employees = new Queryable<TestEntities.Employee>(this.probeProvider);
                return employees.Where(x => x.EmployeeId == id).Expression;
            }

            // Re-extracts the variable values keyed by identity, as the executor does on a cache hit:
            // straight off the original tree, with no preprocessing in between.
            public IReadOnlyDictionary<string, object> ValuesByIdentity(int[] ids)
            {
                return this.extractor.ExtractVariableValuesByIdentity(this.BuildContainsQuery(ids));
            }

            public IReadOnlyDictionary<string, object> InlineArrayValuesByIdentity(int first, int second)
            {
                return this.extractor.ExtractVariableValuesByIdentity(this.BuildInlineArrayQuery(first, second));
            }

            public IReadOnlyDictionary<string, object> MixedArrayValuesByIdentity(int second)
            {
                return this.extractor.ExtractVariableValuesByIdentity(this.BuildMixedArrayQuery(second));
            }
        }
    }
}
