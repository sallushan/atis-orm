using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

using Atis.Orm.Abstractions;
using Atis.Orm.Annotations;
using Atis.Orm.Querying;
using Atis.Orm.Services;
using Atis.SqlExpressionEngine.ExpressionExtensions;
using Atis.SqlExpressionEngine.Services;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         Covers <see cref="ContextualExpression"/>: a value the execution context supplies, asked for
    ///         on every run, never frozen into the compiled query.
    ///     </para>
    ///     <para>
    ///         The SQL text is the same whichever value the context holds, so the end-to-end tests assert
    ///         rows, not SQL.
    ///     </para>
    /// </summary>
    [TestClass]
    public class ContextualNodeTests : TestBase
    {
        private const string MasterConnectionString = "Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True";

        private static int CacheKeyOf(Expression expression)
            => ExpressionEqualityComparer.Instance.GetHashCode(expression);

        private static Expression MinIdPredicate(string key)
        {
            var x = Expression.Parameter(typeof(TestEntities.Employee), "x");
            return Expression.Lambda(
                        Expression.GreaterThanOrEqual(
                            Expression.Property(x, nameof(TestEntities.Employee.EmployeeId)),
                            SqlContext.Create<int>(key)),
                        x);
        }

        // --- Node ------------------------------------------------------------------------------------------

        [TestMethod]
        public void Cache_key_is_stable_across_instances()
        {
            Assert.AreEqual(CacheKeyOf(MinIdPredicate("CurrentUserId")), CacheKeyOf(MinIdPredicate("CurrentUserId")));
        }

        [TestMethod]
        public void Cache_key_separates_different_keys()
        {
            Assert.AreNotEqual(CacheKeyOf(MinIdPredicate("CurrentUserId")), CacheKeyOf(MinIdPredicate("TenantId")));
        }

        [TestMethod]
        public void Node_rejects_a_blank_key()
        {
            Assert.ThrowsException<ArgumentException>(() => SqlContext.Create<int>("  "));
        }

        [TestMethod]
        public void Identity_is_kept_clear_of_named_parameters()
        {
            Assert.AreEqual("context:CurrentUserId", SqlContext.Create<int>("CurrentUserId").Identity);
            Assert.AreNotEqual(SqlContext.Create<int>("a").Identity, SqlParam.Create("a", 1).Identity);
        }

        [TestMethod]
        public void Extractor_does_not_collect_contextual_nodes()
        {
            // The value is not in the tree, so a cache hit has nothing to extract; the context is asked
            // separately, by the compiled query.
            var extractor = new ExpressionVariableValuesExtractor(new ExpressionEvaluator(), new VariableIdentityProvider());
            Assert.AreEqual(0, extractor.ExtractVariableValuesByIdentity(MinIdPredicate("CurrentUserId")).Count);
        }

        [TestMethod]
        public void QueryContext_answers_false_for_an_unknown_key_and_allows_null()
        {
            var context = new QueryContext().Set("Nothing", null);
            Assert.IsTrue(context.TryGetValue("Nothing", out var value));
            Assert.IsNull(value);
            Assert.IsFalse(context.TryGetValue("Missing", out _));
        }

        // --- End-to-end (requires the test SQL Server) -----------------------------------------------------

        [TestMethod]
        public async Task First_run_asks_the_context_too()
        {
            await new TestDatabaseSetup(MasterConnectionString).SetupAsync();

            using var dbc = new OrmDbContext();
            ContextOf(dbc).Set("MinEmployeeId", 24);

            // A cache MISS. A contextual parameter has no translation-time value, so if the first run used
            // InitialValues it would bind null and return nothing.
            Assert.AreEqual(2, EmployeeIdsAtLeast(dbc, "MinEmployeeId").Count);
        }

        [TestMethod]
        public async Task Cache_hit_takes_the_current_value_and_shares_one_entry()
        {
            await new TestDatabaseSetup(MasterConnectionString).SetupAsync();

            using var dbc = new OrmDbContext();
            var context = ContextOf(dbc);

            context.Set("MinEmployeeId", 1);
            Assert.AreEqual(25, EmployeeIdsAtLeast(dbc, "MinEmployeeId").Count);
            var afterCompile = CachedQueryCount(dbc);

            // A different "user" on the same compiled query. Frozen at first compile, this would return 25.
            context.Set("MinEmployeeId", 24);
            Assert.AreEqual(2, EmployeeIdsAtLeast(dbc, "MinEmployeeId").Count);
            Assert.AreEqual(afterCompile, CachedQueryCount(dbc), "Must be a cache hit, not a recompile.");
        }

        [TestMethod]
        public async Task Contextual_value_survives_alongside_a_named_parameter()
        {
            await new TestDatabaseSetup(MasterConnectionString).SetupAsync();

            using var dbc = new OrmDbContext();
            var context = ContextOf(dbc);

            // Two same-typed values from two different sources, bound by identity: context -> lower bound,
            // named -> upper bound. Swapped on the cache hit would give an empty or wrong range.
            context.Set("Lo", 2);
            Assert.AreEqual(3, EmployeeIdsBetween(dbc, hi: 4).Count);
            context.Set("Lo", 20);
            Assert.AreEqual(2, EmployeeIdsBetween(dbc, hi: 21).Count);
        }

        [TestMethod]
        public async Task Missing_context_value_throws_and_names_the_key()
        {
            await new TestDatabaseSetup(MasterConnectionString).SetupAsync();

            using var dbc = new OrmDbContext();

            var ex = Assert.ThrowsException<InvalidOperationException>(() => EmployeeIdsAtLeast(dbc, "NeverSet"));
            StringAssert.Contains(ex.Message, "'NeverSet'");
        }

        // --- The way queries are actually written ----------------------------------------------------------

        /// <summary>A static holder for context values, as an application would declare one.</summary>
        public static class Current
        {
            [ContextualValue("MinEmployeeId")]
            public static int MinEmployeeId => throw new NotSupportedException("Marker only; the query context supplies the value.");
        }

        /// <summary>An instance-based spelling, e.g. a per-request object handed around by the application.</summary>
        public class RequestInfo
        {
            [ContextualValue("MinEmployeeId")]
            public int MinEmployeeId => throw new NotSupportedException("Marker only; the query context supplies the value.");
        }

        /// <summary>No annotation: marked in OnModelCreating, as for a class the application cannot edit.</summary>
        public static class FluentCurrent
        {
            public static int MinEmployeeId => throw new NotSupportedException("Marker only; the query context supplies the value.");
        }

        /// <summary>No annotation: marked in OnModelCreating.</summary>
        public class FluentRequest
        {
            public int MinEmployeeId => throw new NotSupportedException("Marker only; the query context supplies the value.");
        }

        [TestMethod]
        public async Task Fluent_static_and_instance_marks_need_no_attribute()
        {
            await new TestDatabaseSetup(MasterConnectionString).SetupAsync();

            using var dbc = new OrmDbContext();
            var request = new FluentRequest();

            ContextOf(dbc).Set("MinEmployeeId", 24);
            Assert.AreEqual(2, dbc.CreateQuery<TestEntities.Employee>().Where(x => x.EmployeeId >= FluentCurrent.MinEmployeeId).ToList().Count);
            Assert.AreEqual(2, dbc.CreateQuery<TestEntities.Employee>().Where(x => x.EmployeeId >= request.MinEmployeeId).ToList().Count);

            ContextOf(dbc).Set("MinEmployeeId", 1);
            Assert.AreEqual(25, dbc.CreateQuery<TestEntities.Employee>().Where(x => x.EmployeeId >= FluentCurrent.MinEmployeeId).ToList().Count);
        }

        [TestMethod]
        public void Model_answers_from_fluent_first_then_annotation()
        {
            using var dbc = new OrmDbContext();
            var model = dbc.GetOrmModel();

            Assert.IsTrue(model.TryGetContextualKey(typeof(FluentCurrent).GetProperty(nameof(FluentCurrent.MinEmployeeId)), out var fluentKey));
            Assert.AreEqual("MinEmployeeId", fluentKey);
            Assert.IsTrue(model.TryGetContextualKey(typeof(Current).GetProperty(nameof(Current.MinEmployeeId)), out var annotatedKey));
            Assert.AreEqual("MinEmployeeId", annotatedKey);
            Assert.IsFalse(model.TryGetContextualKey(typeof(TestEntities.Employee).GetProperty(nameof(TestEntities.Employee.EmployeeId)), out _));
        }

        [TestMethod]
        public async Task Static_property_in_a_lambda()
        {
            await new TestDatabaseSetup(MasterConnectionString).SetupAsync();

            using var dbc = new OrmDbContext();
            var context = ContextOf(dbc);

            // The equivalent of  dbc.Invoices.Where(x => x.CreatedBy == Current.UserId)
            context.Set("MinEmployeeId", 1);
            var everyone = dbc.CreateQuery<TestEntities.Employee>().Where(x => x.EmployeeId >= Current.MinEmployeeId).Select(x => x.EmployeeId).ToList();
            Assert.AreEqual(25, everyone.Count);
            var afterCompile = CachedQueryCount(dbc);

            // Same lambda, different "logged-in user". Reading the property would have thrown; freezing the
            // first value would have returned 25 again.
            context.Set("MinEmployeeId", 24);
            var last = dbc.CreateQuery<TestEntities.Employee>().Where(x => x.EmployeeId >= Current.MinEmployeeId).Select(x => x.EmployeeId).ToList();
            Assert.AreEqual(2, last.Count);
            Assert.AreEqual(afterCompile, CachedQueryCount(dbc), "Second run must be a cache hit.");
        }

        [TestMethod]
        public async Task Instance_property_on_a_captured_object()
        {
            await new TestDatabaseSetup(MasterConnectionString).SetupAsync();

            using var dbc = new OrmDbContext();
            var request = new RequestInfo();

            ContextOf(dbc).Set("MinEmployeeId", 24);
            Assert.AreEqual(2, dbc.CreateQuery<TestEntities.Employee>().Where(x => x.EmployeeId >= request.MinEmployeeId).ToList().Count);

            ContextOf(dbc).Set("MinEmployeeId", 1);
            Assert.AreEqual(25, dbc.CreateQuery<TestEntities.Employee>().Where(x => x.EmployeeId >= request.MinEmployeeId).ToList().Count);
        }

        [TestMethod]
        public async Task SqlContext_Get_marker_in_a_lambda()
        {
            await new TestDatabaseSetup(MasterConnectionString).SetupAsync();

            using var dbc = new OrmDbContext();
            var context = ContextOf(dbc);

            context.Set("MinEmployeeId", 24);
            Assert.AreEqual(2, dbc.CreateQuery<TestEntities.Employee>().Where(x => x.EmployeeId >= SqlContext.Get<int>("MinEmployeeId")).ToList().Count);

            context.Set("MinEmployeeId", 1);
            Assert.AreEqual(25, dbc.CreateQuery<TestEntities.Employee>().Where(x => x.EmployeeId >= SqlContext.Get<int>("MinEmployeeId")).ToList().Count);
        }

        [TestMethod]
        public async Task Context_value_mixed_with_an_ordinary_captured_variable()
        {
            await new TestDatabaseSetup(MasterConnectionString).SetupAsync();

            using var dbc = new OrmDbContext();
            var context = ContextOf(dbc);

            // Two int sources of different kinds in one lambda; each must bind to its own place.
            context.Set("MinEmployeeId", 2);
            var upTo = 4;
            Assert.AreEqual(3, dbc.CreateQuery<TestEntities.Employee>().Where(x => x.EmployeeId >= Current.MinEmployeeId && x.EmployeeId <= upTo).ToList().Count);

            context.Set("MinEmployeeId", 20);
            upTo = 21;
            Assert.AreEqual(2, dbc.CreateQuery<TestEntities.Employee>().Where(x => x.EmployeeId >= Current.MinEmployeeId && x.EmployeeId <= upTo).ToList().Count);
        }

        [TestMethod]
        public void SqlContext_Get_rejects_a_non_literal_key()
        {
            var key = "MinEmployeeId";
            Expression<Func<TestEntities.Employee, bool>> predicate = x => x.EmployeeId >= SqlContext.Get<int>(key);

            var ex = Assert.ThrowsException<InvalidOperationException>(
                        () => new Atis.SqlExpressionEngine.Preprocessors.ContextualValueRewriterPreprocessor(new NoContextualMembers()).Preprocess(predicate));
            StringAssert.Contains(ex.Message, "literal");
        }

        // --- Helpers ---------------------------------------------------------------------------------------

        private static QueryContext ContextOf(OrmDbContext dbc)
            => (QueryContext)dbc.GetService<IQueryContext>();

        private static List<int> EmployeeIdsAtLeast(OrmDbContext dbc, string key)
        {
            var x = Expression.Parameter(typeof(TestEntities.Employee), "x");
            var predicate = Expression.Lambda<Func<TestEntities.Employee, bool>>(
                                Expression.GreaterThanOrEqual(
                                    Expression.Property(x, nameof(TestEntities.Employee.EmployeeId)),
                                    SqlContext.Create<int>(key)),
                                x);

            return dbc.CreateQuery<TestEntities.Employee>()
                      .Where(predicate)
                      .Select(e => e.EmployeeId)
                      .ToList();
        }

        private static List<int> EmployeeIdsBetween(OrmDbContext dbc, int hi)
        {
            var x = Expression.Parameter(typeof(TestEntities.Employee), "x");
            var id = Expression.Property(x, nameof(TestEntities.Employee.EmployeeId));
            var predicate = Expression.Lambda<Func<TestEntities.Employee, bool>>(
                                Expression.AndAlso(
                                    Expression.GreaterThanOrEqual(id, SqlContext.Create<int>("Lo")),
                                    Expression.LessThanOrEqual(id, SqlParam.Create("Hi", hi))),
                                x);

            return dbc.CreateQuery<TestEntities.Employee>()
                      .Where(predicate)
                      .Select(e => e.EmployeeId)
                      .ToList();
        }

        private static int CachedQueryCount(OrmDbContext dbc)
        {
            var provider = dbc.GetService<ICompiledQueryCacheProvider>();
            var field = typeof(CompiledQueryCacheProvider).GetField("_cache", BindingFlags.Instance | BindingFlags.NonPublic);
            var cache = (ConcurrentDictionary<object, ICompiledQuery>)field.GetValue(provider);
            return cache.Count;
        }
    }
}
