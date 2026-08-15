using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

using Atis.Orm.Abstractions;
using Atis.Orm.Querying;
using Atis.Orm.Services;
using Atis.SqlExpressionEngine.ExpressionExtensions;
using Atis.SqlExpressionEngine.Services;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         Covers <see cref="NamedParameterExpression"/>: a value written straight into a hand-built
    ///         expression tree that behaves like a captured local rather than like a constant — one compiled
    ///         query per shape, the value rebound on every execution.
    ///     </para>
    ///     <para>
    ///         Every end-to-end test here would still pass on SQL text alone if the value were frozen at the
    ///         first compile, because the SQL is identical either way — only the bound value differs. So they
    ///         assert rows.
    ///     </para>
    /// </summary>
    [TestClass]
    public class NamedParameterTests : TestBase
    {
        private const string MasterConnectionString = "Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True";

        private static ExpressionVariableValuesExtractor NewExtractor()
            => new ExpressionVariableValuesExtractor(new ExpressionEvaluator(), new VariableIdentityProvider());

        private static int CacheKeyOf(Expression expression)
            => ExpressionEqualityComparer.Instance.GetHashCode(expression);

        // --- Cache key ------------------------------------------------------------------------------------
        // The compiled-query cache is keyed on this hash alone (ExpressionCacheKeyProvider returns it, and
        // CompiledQueryCacheProvider looks up on it), so these three assertions ARE the caching behaviour.

        [TestMethod]
        public void Cache_key_ignores_the_value()
        {
            // The whole point: same shape, different value, one cache entry. A ConstantExpression would fail
            // this — ExpressionEqualityComparer folds a constant's value into the hash.
            Assert.AreEqual(
                CacheKeyOf(WherePredicate(SqlParam.Create("minId", 1))),
                CacheKeyOf(WherePredicate(SqlParam.Create("minId", 999))),
                "Two trees differing only in a named parameter's value must share a cache entry.");
        }

        [TestMethod]
        public void Cache_key_separates_different_names()
        {
            // Without this, two shapes whose names sit at different positions would share one compiled query
            // and rebind to each other's values. Equals cannot rescue it — the cache never calls Equals.
            Assert.AreNotEqual(
                CacheKeyOf(WherePredicate(SqlParam.Create("minId", 1))),
                CacheKeyOf(WherePredicate(SqlParam.Create("maxId", 1))),
                "The parameter name must reach the cache key hash.");
        }

        [TestMethod]
        public void Cache_key_separates_different_declared_types()
        {
            // Free: ExpressionEqualityComparer hashes obj.Type for every node, extensions included.
            Assert.AreNotEqual(
                CacheKeyOf(SqlParam.Create<string>("p", null)),
                CacheKeyOf(SqlParam.Create<int>("p", 0)),
                "The declared type must separate two same-named parameters.");
        }

        /// <summary>A predicate tree of the shape a dynamic filter builder produces.</summary>
        private static Expression WherePredicate(NamedParameterExpression parameter)
        {
            var x = Expression.Parameter(typeof(TestEntities.Employee), "x");
            return Expression.Lambda(
                        Expression.GreaterThanOrEqual(
                            Expression.Property(x, nameof(TestEntities.Employee.EmployeeId)),
                            parameter),
                        x);
        }

        // --- Value extraction -----------------------------------------------------------------------------

        [TestMethod]
        public void Extractor_collects_the_node_under_its_own_identity()
        {
            var tree = WherePredicate(SqlParam.Create("minId", 7));

            var nodes = NewExtractor().ExtractParameterNodes(tree);
            var byIdentity = NewExtractor().ExtractVariableValuesByIdentity(tree);

            Assert.AreEqual(1, nodes.Count, "The named parameter must be collected like a captured variable.");
            Assert.AreEqual(1, byIdentity.Count);
            Assert.AreEqual(7, byIdentity["named:minId"], "The identity is the name, prefixed to keep it clear of closure paths.");
        }

        [TestMethod]
        public void Extractor_reads_the_value_off_the_node_it_was_given()
        {
            // This is the cache-hit contract: a second tree of the same shape carries a different node
            // instance, and its value is what must be bound.
            Assert.AreEqual(1, NewExtractor().ExtractVariableValuesByIdentity(WherePredicate(SqlParam.Create("minId", 1)))["named:minId"]);
            Assert.AreEqual(2, NewExtractor().ExtractVariableValuesByIdentity(WherePredicate(SqlParam.Create("minId", 2)))["named:minId"]);
        }

        [TestMethod]
        public void Extractor_rejects_one_name_carrying_two_values()
        {
            var x = Expression.Parameter(typeof(TestEntities.Employee), "x");
            var id = Expression.Property(x, nameof(TestEntities.Employee.EmployeeId));
            var tree = Expression.Lambda(
                            Expression.AndAlso(
                                Expression.GreaterThanOrEqual(id, SqlParam.Create("bound", 1)),
                                Expression.LessThanOrEqual(id, SqlParam.Create("bound", 9))),
                            x);

            var ex = Assert.ThrowsException<InvalidOperationException>(
                        () => NewExtractor().ExtractVariableValuesByIdentity(tree));
            StringAssert.Contains(ex.Message, "'bound'", "The message must name the duplicated parameter.");
        }

        [TestMethod]
        public void Extractor_accepts_one_name_repeated_with_the_same_value()
        {
            var x = Expression.Parameter(typeof(TestEntities.Employee), "x");
            var id = Expression.Property(x, nameof(TestEntities.Employee.EmployeeId));
            var tree = Expression.Lambda(
                            Expression.AndAlso(
                                Expression.GreaterThanOrEqual(id, SqlParam.Create("bound", 5)),
                                Expression.LessThanOrEqual(id, SqlParam.Create("bound", 5))),
                            x);

            var byIdentity = NewExtractor().ExtractVariableValuesByIdentity(tree);

            Assert.AreEqual(1, byIdentity.Count, "One name referenced twice with one value is a single binding.");
            Assert.AreEqual(5, byIdentity["named:bound"]);
        }

        // --- Compile-time validation ----------------------------------------------------------------------

        [TestMethod]
        public void Validator_rejects_one_name_carrying_two_values()
        {
            var ex = Assert.ThrowsException<InvalidOperationException>(
                        () => NamedParameterValidator.Validate(BetweenPredicate(5, 9)));

            StringAssert.Contains(ex.Message, "'bound'", "The message must name the duplicated parameter.");
            StringAssert.Contains(ex.Message, "'5'");
            StringAssert.Contains(ex.Message, "'9'");
        }

        [TestMethod]
        public void Validator_accepts_one_name_repeated_with_the_same_value()
        {
            // One binding referenced twice is legitimate, so this must not throw.
            NamedParameterValidator.Validate(BetweenPredicate(5, 5));
        }

        [TestMethod]
        public void Validator_accepts_distinct_names()
        {
            var x = Expression.Parameter(typeof(TestEntities.Employee), "x");
            var id = Expression.Property(x, nameof(TestEntities.Employee.EmployeeId));
            NamedParameterValidator.Validate(
                Expression.Lambda(
                    Expression.AndAlso(
                        Expression.GreaterThanOrEqual(id, SqlParam.Create("lo", 5)),
                        Expression.LessThanOrEqual(id, SqlParam.Create("hi", 9))),
                    x));
        }

        [TestMethod]
        public async Task Duplicate_named_parameter_throws_on_the_first_run()
        {
            await new TestDatabaseSetup(MasterConnectionString).SetupAsync();

            using var dbc = new OrmDbContext();

            // The point of validating at compile time. Before this check the first run SUCCEEDED — each node
            // kept its own translation-time value, so the SQL was right and the rows were right — and only
            // the second, cache-served run threw. A bug that passes every test and fails after warm-up.
            var first = Assert.ThrowsException<InvalidOperationException>(() => EmployeeIdsBetween(dbc, 5, 9));
            StringAssert.Contains(first.Message, "'bound'");

            // And it stays broken rather than becoming a one-off: nothing was cached, so the next run fails
            // identically.
            var second = Assert.ThrowsException<InvalidOperationException>(() => EmployeeIdsBetween(dbc, 5, 9));
            StringAssert.Contains(second.Message, "'bound'");
        }

        /// <summary>A predicate using one name for both bounds — legitimate only when they are equal.</summary>
        private static Expression<Func<TestEntities.Employee, bool>> BetweenPredicate(int lo, int hi)
        {
            var x = Expression.Parameter(typeof(TestEntities.Employee), "x");
            var id = Expression.Property(x, nameof(TestEntities.Employee.EmployeeId));
            return Expression.Lambda<Func<TestEntities.Employee, bool>>(
                        Expression.AndAlso(
                            Expression.GreaterThanOrEqual(id, SqlParam.Create("bound", lo)),
                            Expression.LessThanOrEqual(id, SqlParam.Create("bound", hi))),
                        x);
        }

        [TestMethod]
        public void Node_rejects_a_blank_name()
        {
            // The name is the identity; without one the value could only be bound positionally.
            Assert.ThrowsException<ArgumentException>(() => SqlParam.Create("   ", 1));
        }

        [TestMethod]
        public void Node_never_renders_its_value()
        {
            // VariableIdentityProvider falls back to ToString() for shapes it does not recognise, so a value
            // leaking in here would produce a fresh identity per execution and silently freeze the parameter.
            Assert.AreEqual("@minId", SqlParam.Create("minId", 12345).ToString());
        }

        // --- End-to-end (requires the test SQL Server) -----------------------------------------------------

        [TestMethod]
        public async Task Cache_hit_rebinds_a_named_parameter()
        {
            await new TestDatabaseSetup(MasterConnectionString).SetupAsync();

            using var dbc = new OrmDbContext();

            // First run compiles and caches; second run hits the cache with a new node instance. Written with
            // Expression.Constant instead, the second run would return the first run's 25 rows.
            Assert.AreEqual(25, EmployeeIdsAtLeast(dbc, 1).Count, "All 25 employees have EmployeeId >= 1.");
            var afterCompile = CachedQueryCount(dbc);

            Assert.AreEqual(2, EmployeeIdsAtLeast(dbc, 24).Count, "Only EmployeeId 24 and 25 remain on the cached re-run.");
            // Without this the test would also pass on a cache MISS, which returns correct rows by recompiling
            // — and recompiling per value is exactly the failure this feature exists to prevent.
            Assert.AreEqual(afterCompile, CachedQueryCount(dbc), "The second run must be a cache hit, not a recompile.");
        }

        [TestMethod]
        public async Task Named_parameters_bind_by_identity_not_position()
        {
            await new TestDatabaseSetup(MasterConnectionString).SetupAsync();

            using var dbc = new OrmDbContext();

            // Two same-typed named parameters: 'tag' in the SELECT list, 'minId' in the WHERE. SQL emits
            // SELECT before WHERE, while a LINQ visitor reaches the inner Where first — so the two orders are
            // reverses of each other and positional binding would swap the values on the cache hit.
            var firstRun = TaggedEmployees(dbc, minId: 1, tag: 100);
            Assert.AreEqual(25, firstRun.Count);
            Assert.IsTrue(firstRun.All(r => r.Tag == 100), "First run projects the tag it was given.");

            var secondRun = TaggedEmployees(dbc, minId: 24, tag: 200);
            Assert.AreEqual(2, secondRun.Count, "'minId' must reach the WHERE, not the projection.");
            Assert.IsTrue(secondRun.All(r => r.Tag == 200), "'tag' must reach the projection, not the WHERE.");
        }

        [TestMethod]
        public async Task Repeated_executions_add_one_cache_entry()
        {
            await new TestDatabaseSetup(MasterConnectionString).SetupAsync();

            using var dbc = new OrmDbContext();

            // Prime the entry, then confirm four more executions with four more values add nothing. A frozen
            // constant would add one entry per value, which is the cost this feature exists to avoid.
            EmployeeIdsAtLeast(dbc, 1);
            var afterFirst = CachedQueryCount(dbc);

            foreach (var minId in new[] { 5, 10, 15, 20 })
                EmployeeIdsAtLeast(dbc, minId);

            Assert.AreEqual(afterFirst, CachedQueryCount(dbc), "Every value must share the one compiled query.");
        }

        [TestMethod]
        public async Task Collection_valued_named_parameter_expands_per_execution()
        {
            await new TestDatabaseSetup(MasterConnectionString).SetupAsync();

            using var dbc = new OrmDbContext();

            // The IN list length is part of the SQL text, so this run goes through ExpandableCompiledQuery,
            // which re-renders the fragments every execution rather than reusing one rendered string.
            Assert.AreEqual(3, EmployeeIdsIn(dbc, new[] { 1, 2, 3 }).Count);
            var afterCompile = CachedQueryCount(dbc);

            Assert.AreEqual(2, EmployeeIdsIn(dbc, new[] { 4, 5 }).Count, "A shorter list on the cached re-run must re-expand.");
            Assert.AreEqual(afterCompile, CachedQueryCount(dbc), "A different list length must not compile a second query.");
        }

        // --- Dynamic tree builders ------------------------------------------------------------------------
        // Each returns a freshly built tree, as a caller assembling a query at runtime would.

        private static List<int> EmployeeIdsAtLeast(OrmDbContext dbc, int minId)
        {
            var x = Expression.Parameter(typeof(TestEntities.Employee), "x");
            var predicate = Expression.Lambda<Func<TestEntities.Employee, bool>>(
                                Expression.GreaterThanOrEqual(
                                    Expression.Property(x, nameof(TestEntities.Employee.EmployeeId)),
                                    SqlParam.Create("minId", minId)),
                                x);

            return dbc.CreateQuery<TestEntities.Employee>()
                      .Where(predicate)
                      .Select(e => e.EmployeeId)
                      .ToList();
        }

        private static List<int> EmployeeIdsIn(OrmDbContext dbc, int[] ids)
        {
            var x = Expression.Parameter(typeof(TestEntities.Employee), "x");
            var predicate = Expression.Lambda<Func<TestEntities.Employee, bool>>(
                                new InValuesExpression(
                                    Expression.Property(x, nameof(TestEntities.Employee.EmployeeId)),
                                    SqlParam.Create("ids", ids)),
                                x);

            return dbc.CreateQuery<TestEntities.Employee>()
                      .Where(predicate)
                      .Select(e => e.EmployeeId)
                      .ToList();
        }

        private static List<int> EmployeeIdsBetween(OrmDbContext dbc, int lo, int hi)
        {
            return dbc.CreateQuery<TestEntities.Employee>()
                      .Where(BetweenPredicate(lo, hi))
                      .Select(e => e.EmployeeId)
                      .ToList();
        }

        private static List<TaggedRow> TaggedEmployees(OrmDbContext dbc, int minId, int tag)
        {
            // A parameter instance per lambda, as the C# compiler emits.
            var w = Expression.Parameter(typeof(TestEntities.Employee), "w");
            var predicate = Expression.Lambda<Func<TestEntities.Employee, bool>>(
                                Expression.GreaterThanOrEqual(
                                    Expression.Property(w, nameof(TestEntities.Employee.EmployeeId)),
                                    SqlParam.Create("minId", minId)),
                                w);

            var s = Expression.Parameter(typeof(TestEntities.Employee), "s");
            var projection = Expression.Lambda<Func<TestEntities.Employee, TaggedRow>>(
                                Expression.MemberInit(
                                    Expression.New(typeof(TaggedRow)),
                                    Expression.Bind(typeof(TaggedRow).GetProperty(nameof(TaggedRow.Tag)), SqlParam.Create("tag", tag)),
                                    Expression.Bind(typeof(TaggedRow).GetProperty(nameof(TaggedRow.EmployeeId)), Expression.Property(s, nameof(TestEntities.Employee.EmployeeId)))),
                                s);

            return dbc.CreateQuery<TestEntities.Employee>()
                      .Where(predicate)
                      .Select(projection)
                      .ToList();
        }

        public class TaggedRow
        {
            public int Tag { get; set; }
            public int EmployeeId { get; set; }
        }

        /// <summary>
        ///     Number of entries in the shared compiled-query cache. Read by reflection because the cache
        ///     deliberately exposes only Add/TryGet; the count is a test concern, not an API one.
        /// </summary>
        private static int CachedQueryCount(OrmDbContext dbc)
        {
            var provider = dbc.GetService<ICompiledQueryCacheProvider>();
            var field = typeof(CompiledQueryCacheProvider).GetField("_cache", BindingFlags.Instance | BindingFlags.NonPublic);
            var cache = (ConcurrentDictionary<object, ICompiledQuery>)field.GetValue(provider);
            return cache.Count;
        }
    }
}
