using Atis.Orm.Abstractions;
using Atis.Orm.Metadata;
using Atis.Orm.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <see cref="NavigationInitializer"/> behavior: per-type compiled initialization (only
    ///     <c>IQueryable&lt;TChild&gt;</c> and <c>Func&lt;TOther&gt;</c> shapes), and the timed
    ///     single-entity cache with FK stale-checking. These tests run DB-free: the initializer is
    ///     constructed directly with an in-memory <see cref="IQueryableFactory"/> that counts query
    ///     executions.
    /// </summary>
    [TestClass]
    public class NavigationInitializerTests : TestBase
    {
        /// <summary>
        ///     In-memory <see cref="IQueryableFactory"/>. <see cref="ExecutionCount"/> increments only
        ///     when a produced query is actually enumerated (lazy queries assigned to IQueryable
        ///     navigation properties don't count until executed).
        /// </summary>
        private sealed class CountingQueryableFactory : IQueryableFactory
        {
            public List<object> Items { get; } = new();
            public int ExecutionCount { get; private set; }

            public IQueryable<T> CreateQueryable<T>() => this.Enumerate<T>().AsQueryable();

            public IQueryable<T> CreateQueryable<T>(Expression expression)
                => this.Enumerate<T>().AsQueryable().Provider.CreateQuery<T>(expression);

            private IEnumerable<T> Enumerate<T>()
            {
                this.ExecutionCount++;
                foreach (var item in this.Items.OfType<T>())
                    yield return item;
            }
        }

        private static (NavigationInitializer initializer, CountingQueryableFactory factory) CreateInitializer(OrmDbContext dbc)
        {
            var factory = new CountingQueryableFactory();
            var initializer = new NavigationInitializer(factory, dbc.GetOrmModel(), new OrmReflectionService());
            return (initializer, factory);
        }

        [TestMethod]
        public void Func_navigation_second_call_within_expiration_is_served_from_cache()
        {
            using var dbc = new OrmDbContext();
            dbc.CreateQuery<Employee>();
            var (initializer, factory) = CreateInitializer(dbc);
            using (initializer)
            {
                var manager = new Employee { EmployeeId = "M1", Name = "Boss" };
                factory.Items.Add(manager);
                var employee = new Employee { EmployeeId = "E1", ManagerId = "M1" };

                initializer.Initialize(employee);
                Assert.IsNotNull(employee.NavManager, "Func<> navigation should be initialized to a delegate");
                Assert.AreEqual(0, factory.ExecutionCount, "initialization must not execute any query");

                Assert.AreSame(manager, employee.NavManager());
                Assert.AreSame(manager, employee.NavManager());
                Assert.AreEqual(1, factory.ExecutionCount, "second call within the expiration window must be served from cache");
            }
        }

        [TestMethod]
        public void Func_navigation_reloads_when_foreign_key_changes()
        {
            using var dbc = new OrmDbContext();
            dbc.CreateQuery<Employee>();
            var (initializer, factory) = CreateInitializer(dbc);
            using (initializer)
            {
                var manager1 = new Employee { EmployeeId = "M1", Name = "Boss 1" };
                var manager2 = new Employee { EmployeeId = "M2", Name = "Boss 2" };
                factory.Items.Add(manager1);
                factory.Items.Add(manager2);
                var employee = new Employee { EmployeeId = "E1", ManagerId = "M1" };

                initializer.Initialize(employee);
                Assert.AreSame(manager1, employee.NavManager());
                Assert.AreEqual(1, factory.ExecutionCount);

                // although the manager entity is cached, the FK no longer matches it, so it must reload
                employee.ManagerId = "M2";
                Assert.AreSame(manager2, employee.NavManager());
                Assert.AreEqual(2, factory.ExecutionCount, "FK change must invalidate the cached entity");

                Assert.AreSame(manager2, employee.NavManager());
                Assert.AreEqual(2, factory.ExecutionCount, "reloaded entity must be cached again");
            }
        }

        [TestMethod]
        public void Func_navigation_reloads_after_cache_expiration()
        {
            using var dbc = new OrmDbContext();
            dbc.CreateQuery<Employee>();
            var (initializer, factory) = CreateInitializer(dbc);
            using (initializer)
            {
                initializer.CacheExpiration = TimeSpan.FromMilliseconds(100);
                var manager = new Employee { EmployeeId = "M1", Name = "Boss" };
                factory.Items.Add(manager);
                var employee = new Employee { EmployeeId = "E1", ManagerId = "M1" };

                initializer.Initialize(employee);
                Assert.AreSame(manager, employee.NavManager());
                Assert.AreEqual(1, factory.ExecutionCount);

                Thread.Sleep(250);

                Assert.AreSame(manager, employee.NavManager());
                Assert.AreEqual(2, factory.ExecutionCount, "an expired cache entry must not be reused");
            }
        }

        [TestMethod]
        public void Func_navigation_null_result_is_not_cached()
        {
            using var dbc = new OrmDbContext();
            dbc.CreateQuery<Employee>();
            var (initializer, factory) = CreateInitializer(dbc);
            using (initializer)
            {
                var employee = new Employee { EmployeeId = "E1", ManagerId = "MISSING" };

                initializer.Initialize(employee);
                Assert.IsNull(employee.NavManager());
                Assert.IsNull(employee.NavManager());
                Assert.AreEqual(2, factory.ExecutionCount, "a null (not found) result must not be served from cache");
            }
        }

        [TestMethod]
        public void JoinedSource_only_navigation_loads_through_source_and_is_not_cached()
        {
            using var dbc = new OrmDbContext();
            dbc.CreateQuery<ItemExtension>();
            var (initializer, factory) = CreateInitializer(dbc);
            using (initializer)
            {
                var part1 = new ItemPart { ItemId = "I1", PartNumber = "P1" };
                var part2 = new ItemPart { ItemId = "I2", PartNumber = "P2" };
                factory.Items.Add(part1);
                factory.Items.Add(part2);
                var item = new ItemExtension { ItemId = "I1" };

                initializer.Initialize(item);
                Assert.IsNotNull(item.NavFirstPart, "JoinedSource-only Func<> navigation should be initialized to a delegate");

                // JoinedSource is `parent => parent.NavParts.Take(1)`; NavParts was initialized above,
                // so the load must go through it and pick the item's own part.
                Assert.AreSame(part1, item.NavFirstPart());
                Assert.AreSame(part1, item.NavFirstPart());
                Assert.AreEqual(2, factory.ExecutionCount, "without a join condition there is no FK check, so the result must not be cached");
            }
        }

        [TestMethod]
        public void Already_populated_navigation_is_not_overwritten()
        {
            using var dbc = new OrmDbContext();
            dbc.CreateQuery<Employee>();
            var (initializer, factory) = CreateInitializer(dbc);
            using (initializer)
            {
                var existingDelegate = new Func<Employee>(() => null);
                var existingQuery = factory.CreateQueryable<EmployeeDegree>();
                var employee = new Employee { EmployeeId = "E1", NavManager = existingDelegate, NavDegrees = existingQuery };

                initializer.Initialize(employee);

                Assert.AreSame(existingDelegate, employee.NavManager, "an already-populated Func<> navigation must be kept");
                Assert.AreSame(existingQuery, employee.NavDegrees, "an already-populated IQueryable<> navigation must be kept");
                Assert.IsNotNull(employee.NavSubOrdinates, "untouched navigations must still be initialized");
            }
        }

        [TestMethod]
        public void Initialize_is_noop_for_null_scalar_and_unmapped_shapes()
        {
            using var dbc = new OrmDbContext();
            dbc.CreateQuery<Employee>();
            var (initializer, _) = CreateInitializer(dbc);
            using (initializer)
            {
                initializer.Initialize(null);
                initializer.Initialize(new { Id = 1, Name = "x" });
                initializer.Initialize(42);
            }
        }

        [TestMethod]
        public void Type_without_supported_navigations_is_skipped_repeatedly()
        {
            using var dbc = new OrmDbContext();
            dbc.CreateQuery<FluentCountry>();
            var (initializer, factory) = CreateInitializer(dbc);
            using (initializer)
            {
                var country = new FluentCountry { Id = 1, Name = "X" };
                // first call compiles-and-caches the "no navigations" verdict; second call is the
                // static-cache fast path.
                initializer.Initialize(country);
                initializer.Initialize(country);
                Assert.AreEqual(0, factory.ExecutionCount);
            }
        }

        [TestMethod]
        public void Registered_initializer_produces_translatable_lazy_query()
        {
            using var dbc = new OrmDbContext();
            dbc.CreateQuery<FluentAuthor>();

            var author = new FluentAuthor { Id = 42 };
            dbc.GetNavigationInitializer().Initialize(author);

            Assert.IsNotNull(author.Books, "IQueryable<> children navigation should be initialized to a lazy query");
            // plain single-reference navigations are not a supported shape and must stay null
            Assert.IsNull(author.PrimaryBook);
            Assert.IsNull(author.Country);
            Assert.IsNull(author.LatestBook);

            var sql = dbc.TranslateToSql(author.Books);
            Console.WriteLine(sql);
            string expected = @"
SELECT t1.Id AS Id, t1.BOOK_TITLE AS Title, t1.AuthorId AS AuthorId, t1.Year AS Year
FROM BOOK AS t1
WHERE (@p0 = t1.AuthorId)
";
            ValidateQueryResults(sql, expected);
        }
    }
}
