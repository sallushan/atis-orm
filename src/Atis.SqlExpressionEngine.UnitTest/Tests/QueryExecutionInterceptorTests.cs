using Atis.Orm;
using Atis.Orm.Abstractions;
using Atis.Orm.Annotations;
using Atis.Orm.DataAccess;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Atzonix.DependencyInjection;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     The consumer-side validation hook: <see cref="IQueryExecutionInterceptor"/> sees the LINQ
    ///     expression of every execution, cache hit or miss, before anything is compiled or sent.
    /// </summary>
    [TestClass]
    public class QueryExecutionInterceptorTests
    {
        private const string ServerConnectionString =
            "Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True";

        [DbTable("PricedItem", "dbo")]
        public class InterceptedItem
        {
            [PrimaryKey]
            public int Id { get; set; }
            public string Name { get; set; }
        }

        // Interceptors are singletons of the provider, so what they saw is kept in statics.
        private static readonly List<string> Seen = new List<string>();

        /// <summary>A realistic validator: refuse a query that has no Where at all.</summary>
        public class RequireWhereInterceptor : IQueryExecutionInterceptor
        {
            public void QueryExecuting(Expression expression)
            {
                lock (Seen) Seen.Add("where-check");
                var finder = new WhereFinder();
                finder.Visit(expression);
                if (!finder.Found)
                    throw new InvalidOperationException("Every query must be filtered.");
            }

            private sealed class WhereFinder : ExpressionVisitor
            {
                public bool Found;
                protected override Expression VisitMethodCall(MethodCallExpression node)
                {
                    // Delete and Update carry their own predicate.
                    if (node.Method.Name == "Where" || node.Method.Name == "Delete" || node.Method.Name == "Update")
                        this.Found = true;
                    return base.VisitMethodCall(node);
                }
            }
        }

        public class SecondInterceptor : IQueryExecutionInterceptor
        {
            public void QueryExecuting(Expression expression)
            {
                lock (Seen) Seen.Add("second");
            }
        }

        public class InterceptorExtension : IServiceContextExtension, IServiceProviderCacheKeyContributor
        {
            public void AddServices(IServiceCollection services)
            {
                new OrmServiceBuilder(services)
                    .AddQueryExecutionInterceptor<RequireWhereInterceptor>()
                    .AddQueryExecutionInterceptor<SecondInterceptor>();
            }

            // A singleton changes the shape of the service graph, so it must split the provider cache;
            // otherwise this context would reuse another test's provider, which has no interceptors.
            public object GetServiceProviderCacheKey() => typeof(InterceptorExtension);
        }

        private sealed class InterceptedDbContext : OrmDbContext
        {
            protected override void OnConfiguring(DataContextConfiguration config)
            {
                base.OnConfiguring(config);
                config.AddOrUpdateExtension(new InterceptorExtension());
            }
        }

        [TestInitialize]
        public void Init()
        {
            new TestDatabaseSetup(ServerConnectionString).Setup();
            lock (Seen) Seen.Clear();
        }

        [TestMethod]
        public void Interceptor_runs_on_every_execution_including_cache_hits()
        {
            using var db = new InterceptedDbContext();

            // Same shape twice with different captured values: the second one is a cache hit.
            var id1 = 1;
            var id2 = 2;
            db.CreateQuery<InterceptedItem>().Where(x => x.Id == id1).ToList();
            db.CreateQuery<InterceptedItem>().Where(x => x.Id == id2).ToList();

            Assert.AreEqual(2, Seen.Count(x => x == "where-check"), "It must run again on the cache hit.");
        }

        [TestMethod]
        public void Interceptor_that_throws_stops_the_query_and_the_context_stays_usable()
        {
            using var db = new InterceptedDbContext();

            var ex = Assert.ThrowsException<InvalidOperationException>(
                () => db.CreateQuery<InterceptedItem>().ToList());
            Assert.AreEqual("Every query must be filtered.", ex.Message);

            // The refusal is per query, not a poisoned context.
            var id = 1;
            db.CreateQuery<InterceptedItem>().Where(x => x.Id == id).ToList();
        }

        [TestMethod]
        public void A_refused_query_is_refused_every_time_not_just_the_first()
        {
            using var db = new InterceptedDbContext();

            for (var i = 0; i < 3; i++)
            {
                Assert.ThrowsException<InvalidOperationException>(
                    () => db.CreateQuery<InterceptedItem>().ToList());
            }
        }

        [TestMethod]
        public async Task Interceptor_runs_on_the_asynchronous_path_too()
        {
            using var db = new InterceptedDbContext();

            var id = 1;
            await db.CreateQuery<InterceptedItem>().Where(x => x.Id == id).ToListAsync();
            Assert.AreEqual(1, Seen.Count(x => x == "where-check"));

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => db.CreateQuery<InterceptedItem>().ToListAsync());
        }

        [TestMethod]
        public void Interceptors_run_in_registration_order_and_the_first_throw_stops_the_rest()
        {
            using var db = new InterceptedDbContext();

            var id = 1;
            db.CreateQuery<InterceptedItem>().Where(x => x.Id == id).ToList();
            CollectionAssert.AreEqual(new[] { "where-check", "second" }, Seen.ToArray());

            lock (Seen) Seen.Clear();
            Assert.ThrowsException<InvalidOperationException>(() => db.CreateQuery<InterceptedItem>().ToList());
            CollectionAssert.AreEqual(new[] { "where-check" }, Seen.ToArray(), "The second must not run once the first refused.");
        }

        [TestMethod]
        public void Context_without_interceptors_is_unaffected()
        {
            using var db = new OrmDbContext();
            db.CreateQuery<InterceptedItem>().ToList();
            Assert.AreEqual(0, Seen.Count);
        }

        [TestMethod]
        public void Linq_based_delete_also_passes_through_the_interceptor()
        {
            using var db = new InterceptedDbContext();
            db.TransactionWithRollback(() =>
            {
                var affected = db.CreateQuery<InterceptedItem>().Delete(x => x.Id == -1);

                Assert.AreEqual(0, affected);
                Assert.AreEqual(1, Seen.Count(x => x == "where-check"), "A non-query execution is an execution too.");
            });
        }
    }
}
