using System.Linq.Expressions;

using Atis.Expressions;
using Atis.Orm.Abstractions;
using Atis.Orm.Preprocessing;
using Atis.Orm.Querying;
using Atis.Orm.Services;
using Atis.Orm.SqlServer;
using Atis.Orm.Translation;
using Atis.SqlExpressionEngine.ExpressionConverters;
using Atis.SqlExpressionEngine.Services;
using Atis.SqlExpressionEngine.UnitTest.Converters;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         Exercises <c>x.Col == value</c> where <c>value</c> may be null.
    ///     </para>
    ///     <para>
    ///         C# and SQL disagree here - C# <c>==</c> treats two nulls as equal, SQL <c>=</c> never does - so
    ///         the comparison needs one of two spellings, and which one is only knowable from the value bound at
    ///         execution time. A compiled query is cached by expression shape and re-run with whatever the
    ///         caller supplies next, so choosing at translation time silently serves one caller's answer to
    ///         every later caller. The choice therefore belongs to the renderer; these tests pin that down,
    ///         because every way of getting it wrong returns wrong rows without failing.
    ///     </para>
    /// </summary>
    [TestClass]
    public class NullComparisonTests : TestBase
    {
        private static ICommandRenderer CreateRenderer()
            => new CommandRenderer(new SqlDbParameterFactory(new SqlDbParameterNameGenerator()));

        private SqlTranslationResult TranslateWithSqlServer(Expression queryExpression)
        {
            var sqlExpression = ConvertExpressionToSqlExpression(queryExpression, out _);
            Assert.IsNotNull(sqlExpression, "Expression should convert to a SQL expression.");
            return new SqlServerSqlExpressionTranslator().Translate(sqlExpression);
        }

        // Whether both spellings were emitted is a decision the TRANSLATOR made from the parameter's declared
        // type, so it is read off the fragments rather than off any one execution's rendered text.
        private static bool HasNullSwitch(SqlTranslationResult translation)
            => translation.Fragments.OfType<NullSwitchCommandFragment>().Any();

        [TestMethod]
        public void Compiling_with_a_null_value_does_not_bake_IS_NULL_into_the_cached_query()
        {
            // The bug this whole change exists for. "Field left blank" is the natural FIRST search on any
            // search screen, so the query that gets compiled - and cached forever - is the one whose value
            // happened to be null. Folding it to `IS NULL` there emits no placeholder either, so every later
            // execution that supplies a real value silently runs `Department IS NULL` instead.
            var wiring = new Wiring();
            var compiled = wiring.Compiler.Compile(wiring.BuildEqualQuery(null));

            var withValue = compiled.GetExecutionContext(wiring.ValuesByIdentity("IT"), useInitialValues: false);

            Assert.IsFalse(withValue.Sql.Contains("IS NULL"), "The comparison must not have folded to IS NULL.");
            StringAssert.Contains(withValue.Sql, "t1.Department = @p0");
            Assert.AreEqual(1, withValue.DbParameters.Count, "The value must still be bound.");
            Assert.AreEqual("IT", withValue.DbParameters[0].Value);
        }

        [TestMethod]
        public void One_compiled_query_serves_both_a_null_and_a_supplied_value()
        {
            // The property the whole design rests on, and the one that separates this from putting the value's
            // nullness into the cache key: ONE entry per call site, whichever way the value goes.
            var wiring = new Wiring();
            var compiled = wiring.Compiler.Compile(wiring.BuildEqualQuery("IT"));

            var withValue = compiled.GetExecutionContext(wiring.ValuesByIdentity("Finance"), useInitialValues: false);
            var withNull = compiled.GetExecutionContext(wiring.ValuesByIdentity(null), useInitialValues: false);

            StringAssert.Contains(withValue.Sql, "t1.Department = @p0");
            Assert.AreEqual(1, withValue.DbParameters.Count);
            Assert.AreEqual("Finance", withValue.DbParameters[0].Value);

            StringAssert.Contains(withNull.Sql, "t1.Department IS NULL");
            Assert.AreEqual(0, withNull.DbParameters.Count, "A null test binds nothing - there is no value to send.");
        }

        [TestMethod]
        public void Not_equal_becomes_IS_NOT_NULL_when_the_value_is_null()
        {
            var wiring = new Wiring();
            var compiled = wiring.Compiler.Compile(wiring.BuildNotEqualQuery("IT"));

            var withNull = compiled.GetExecutionContext(wiring.NotEqualValuesByIdentity(null), useInitialValues: false);
            var withValue = compiled.GetExecutionContext(wiring.NotEqualValuesByIdentity("IT"), useInitialValues: false);

            StringAssert.Contains(withNull.Sql, "t1.Department IS NOT NULL");
            Assert.AreEqual(0, withNull.DbParameters.Count);
            StringAssert.Contains(withValue.Sql, "t1.Department <> @p0");
            Assert.AreEqual(1, withValue.DbParameters.Count);
        }

        [TestMethod]
        public void A_null_written_into_the_query_still_folds_and_stays_on_the_render_once_path()
        {
            // A literal null is part of the expression's shape, so it is part of the cache key and cannot
            // differ between executions of this compiled query. Nothing to decide per execution - fold it, and
            // keep the query on the cheap path.
            var employees = new Queryable<Employee>(this.queryProvider);
            var q = employees.Where(x => x.Department == null);

            var translation = this.TranslateWithSqlServer(q.Expression);
            var rendered = CreateRenderer().Render(translation.Fragments, p => p.InitialValue);

            StringAssert.Contains(rendered.Sql, "t1.Department IS NULL");
            Assert.AreEqual(0, rendered.DbParameters.Count);
            Assert.IsFalse(HasNullSwitch(translation));
            Assert.IsFalse(translation.RequiresPerExecutionRendering,
                "A literal null cannot change between executions, so the SQL text is not value-dependent.");
        }

        [TestMethod]
        public void A_non_nullable_value_type_comparison_stays_on_the_render_once_path()
        {
            // The reason the parameter carries its DECLARED type. A Guid variable can never be null on any
            // execution, so the comparison has one spelling forever and needs no switch - which keeps integer
            // and Guid key lookups, the hot path of most applications, rendering once at compile time.
            var rowId = System.Guid.NewGuid();
            var employees = new Queryable<Employee>(this.queryProvider);
            var q = employees.Where(x => x.RowId == rowId);

            var translation = this.TranslateWithSqlServer(q.Expression);
            var rendered = CreateRenderer().Render(translation.Fragments, p => p.InitialValue);

            StringAssert.Contains(rendered.Sql, "t1.RowId = @p0");
            Assert.IsFalse(HasNullSwitch(translation));
            Assert.IsFalse(translation.RequiresPerExecutionRendering);
        }

        [TestMethod]
        public void A_nullable_column_compared_to_a_non_nullable_variable_stays_on_the_render_once_path()
        {
            // The shape the benchmarks use - a key lookup, `nullable column == non-nullable variable`. What
            // decides is the VARIABLE's type, not the column's: nothing can ever bind null here, so there is
            // nothing to choose between and the query keeps rendering its SQL once.
            var age = 30;
            var students = new Queryable<Student>(this.queryProvider);
            var q = students.Where(x => x.Age == age);

            var translation = this.TranslateWithSqlServer(q.Expression);
            var rendered = CreateRenderer().Render(translation.Fragments, p => p.InitialValue);

            StringAssert.Contains(rendered.Sql, "t1.Age = @p0");
            Assert.IsFalse(HasNullSwitch(translation));
            Assert.IsFalse(translation.RequiresPerExecutionRendering);
        }

        [TestMethod]
        public void A_nullable_value_type_comparison_switches_even_while_it_holds_a_value()
        {
            // The case runtime types cannot answer: a `Guid?` holding a value boxes to a plain Guid, so only
            // the declared type reveals that a later execution of this same cached query could bind null.
            System.Guid? rowId = System.Guid.NewGuid();
            var employees = new Queryable<Employee>(this.queryProvider);
            var q = employees.Where(x => x.RowId == rowId);

            var translation = this.TranslateWithSqlServer(q.Expression);

            Assert.IsTrue(HasNullSwitch(translation),
                "A nullable value type can be null on a later execution, so both spellings must be emitted.");
        }

        [TestMethod]
        public void A_comparison_in_a_projection_switches_too()
        {
            // The switch is just text plus positions, so it works wherever a comparison is legal - not only in
            // a WHERE clause. Here it lands inside the CASE the select list wraps a boolean in.
            var department = "IT";
            var employees = new Queryable<Employee>(this.queryProvider);
            var q = employees.Select(x => new { x.Name, IsMatch = x.Department == department });

            var translation = this.TranslateWithSqlServer(q.Expression);
            var withValue = CreateRenderer().Render(translation.Fragments, p => p.InitialValue);
            var withNull = CreateRenderer().Render(translation.Fragments, p => null);

            StringAssert.Contains(withValue.Sql, "CASE WHEN (t1.Department = @p0) THEN 1 ELSE 0 END");
            StringAssert.Contains(withNull.Sql, "CASE WHEN (t1.Department IS NULL) THEN 1 ELSE 0 END");
        }

        [TestMethod]
        public void A_comparison_written_with_the_value_first_is_handled_the_same_way()
        {
            var wiring = new Wiring();
            var compiled = wiring.Compiler.Compile(wiring.BuildReversedEqualQuery("IT"));

            var withNull = compiled.GetExecutionContext(wiring.ReversedValuesByIdentity(null), useInitialValues: false);

            // Which operand the developer wrote first is not a semantic choice, so it must not decide whether
            // the null handling happens at all.
            StringAssert.Contains(withNull.Sql, "t1.Department IS NULL");
            Assert.AreEqual(0, withNull.DbParameters.Count);
        }

        /// <summary>
        ///     A full compile-and-execute stack, so the tests exercise the same path an application does:
        ///     compile once, then re-run with values re-extracted from the original tree as a cache hit does.
        /// </summary>
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
                var preprocessor = new OrmExpressionPreprocessorProvider(model, reflectionService, expressionEvaluator, plugins: []);
                var linqToSqlConverter = new LinqToSqlConverter(treeConverter, new SqlExpressionPostprocessorProvider(postprocessors: []));
                var sqlExpressionTranslator = new SqlServerSqlExpressionTranslator();
                var dbParameterFactory = new SqlDbParameterFactory(new SqlDbParameterNameGenerator());
                var commandRenderer = new CommandRenderer(dbParameterFactory);
                var queryTranslator = new QueryTranslator(preprocessor, linqToSqlConverter, sqlExpressionTranslator, logger);
                this.Compiler = new QueryCompiler(queryTranslator, commandRenderer, dbParameterFactory, new ElementFactoryBuilder());
            }

            // Identical shape (and captured-variable identity) across calls, differing only in the value -
            // exactly the cache-key-equal / value-different case.
            public Expression BuildEqualQuery(string department)
            {
                var employees = new Queryable<Employee>(this.probeProvider);
                return employees.Where(x => x.Department == department).Expression;
            }

            public Expression BuildNotEqualQuery(string department)
            {
                var employees = new Queryable<Employee>(this.probeProvider);
                return employees.Where(x => x.Department != department).Expression;
            }

            public Expression BuildReversedEqualQuery(string department)
            {
                var employees = new Queryable<Employee>(this.probeProvider);
                return employees.Where(x => department == x.Department).Expression;
            }

            // Re-extracts the variable values keyed by identity, as the executor does on a cache hit: straight
            // off the original tree, with no preprocessing in between.
            public IReadOnlyDictionary<string, object> ValuesByIdentity(string department)
                => this.extractor.ExtractVariableValuesByIdentity(this.BuildEqualQuery(department));

            public IReadOnlyDictionary<string, object> NotEqualValuesByIdentity(string department)
                => this.extractor.ExtractVariableValuesByIdentity(this.BuildNotEqualQuery(department));

            public IReadOnlyDictionary<string, object> ReversedValuesByIdentity(string department)
                => this.extractor.ExtractVariableValuesByIdentity(this.BuildReversedEqualQuery(department));
        }
    }
}
