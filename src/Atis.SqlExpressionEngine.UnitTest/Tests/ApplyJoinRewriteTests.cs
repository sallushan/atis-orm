using System;
using System.Linq;

using Atis.Orm;
using Atis.Orm.DataAccess;
using Atis.Orm.Metadata;
using Atis.Orm.SqlServer;
using Atzonix.DependencyInjection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <c>RewriteApplyAsJoin</c> writes a correlated APPLY as a join, with <c>TOP n</c> turned into
    ///     <c>ROW_NUMBER() ... &lt;= n</c>. It is off unless asked for, because the SQL then differs from what
    ///     the LINQ spells out; with it on or off the rows must be the same.
    /// </summary>
    [TestClass]
    public class ApplyJoinRewriteTests
    {
        private const string ServerConnectionString =
            "Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True";

        public class ApplyOwner
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public ApplyItem TopItem { get; set; }
        }

        public class ApplyItem
        {
            public int Id { get; set; }
            public int OwnerId { get; set; }
            public int Score { get; set; }
            public string Label { get; set; }
        }

        /// <summary>
        ///     Adds nothing. Its type alone gives these contexts a service provider, and so a model, of their own:
        ///     the provider is shared by every context with the same extension types, so without it whichever of
        ///     this test's contexts and <c>OrmDbContext</c> built first would impose its entity mapping on the other.
        /// </summary>
        private class ApplyJoinTestMarker : IServiceContextExtension
        {
            public void AddServices(IServiceCollection services) { }
        }

        private class PlainContext : Atis.Orm.DataContext
        {
            protected override void OnConfiguring(DataContextConfiguration config)
            {
                config.AddOrUpdateExtension(new ApplyJoinTestMarker());
                config.UseSqlServer($"Server=.;Database={TestDatabaseSetup.DatabaseName};Integrated Security=true;Encrypt=True;TrustServerCertificate=True");
                config.UseUnitTestCustomization();
            }

            protected override void OnModelCreating(ModelBuilder mb)
            {
                mb.Entity<ApplyOwner>(e =>
                {
                    e.ToTable("APPLYJOIN_OWNER");
                    e.HasKey(x => x.Id);
                    e.HasOneRow(x => x.TopItem,
                        (o, items) => items.Where(i => i.OwnerId == o.Id).OrderByDescending(i => i.Score).Take(1));
                });
                mb.Entity<ApplyItem>(e =>
                {
                    e.ToTable("APPLYJOIN_ITEM");
                    e.HasKey(x => x.Id);
                });
            }
        }

        private class RewritingContext : PlainContext
        {
            protected override void OnConfiguring(DataContextConfiguration config)
            {
                base.OnConfiguring(config);
                config.RewriteApplyAsJoin();
            }
        }

        [TestInitialize]
        public void CreateTables()
        {
            new TestDatabaseSetup(ServerConnectionString).Setup();
            Run(@"
drop table if exists dbo.APPLYJOIN_ITEM;
drop table if exists dbo.APPLYJOIN_OWNER;
create table dbo.APPLYJOIN_OWNER (Id int not null primary key, Name nvarchar(50) not null);
create table dbo.APPLYJOIN_ITEM (Id int not null primary key, OwnerId int not null, Score int not null, Label nvarchar(50) not null);
insert into dbo.APPLYJOIN_OWNER values (1, 'many'), (2, 'none'), (3, 'one'), (4, 'tied');
insert into dbo.APPLYJOIN_ITEM values
    (1, 1, 5, 'm5'), (2, 1, 9, 'm9'), (3, 1, 7, 'm7'),
    (4, 3, 4, 'o4'),
    (5, 4, 6, 't6a'), (6, 4, 2, 't2');");
        }

        [TestCleanup]
        public void DropTables() => Run(@"
drop table if exists dbo.APPLYJOIN_ITEM;
drop table if exists dbo.APPLYJOIN_OWNER;");

        private static void Run(string sql)
        {
            using var connection = new SqlConnection($"Server=.;Database={TestDatabaseSetup.DatabaseName};Integrated Security=true;Encrypt=True;TrustServerCertificate=True");
            connection.Open();
            using var command = new SqlCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        private static string TopItemSql(Atis.Orm.DataContext db)
            => db.TranslateToSql(db.CreateQuery<ApplyOwner>().Select(o => new { o.Id, o.TopItem.Label }));

        [TestMethod]
        public void Off_by_default_the_apply_is_kept()
        {
            using var db = new PlainContext();
            var sql = TopItemSql(db);
            Console.WriteLine(sql);
            StringAssert.Contains(sql, "OUTER APPLY");
            Assert.IsFalse(sql.Contains("ROW_NUMBER"), sql);
        }

        [TestMethod]
        public void On_the_apply_becomes_a_left_join_over_row_number()
        {
            using var db = new RewritingContext();
            var sql = TopItemSql(db);
            Console.WriteLine(sql);
            Assert.IsFalse(sql.Contains("OUTER APPLY") || sql.Contains("CROSS APPLY"), sql);
            StringAssert.Contains(sql, "LEFT JOIN");
            StringAssert.Contains(sql, "ROW_NUMBER() OVER (PARTITION BY");
            StringAssert.Contains(sql, "ORDER BY");
            StringAssert.Contains(sql, "<= @p");
            Assert.IsFalse(sql.Contains("TOP ("), sql);
        }

        [TestMethod]
        public void Contexts_that_disagree_do_not_share_compiled_queries()
        {
            // Same LINQ, built twice in each order: a shared cache entry would hand one context the other's SQL.
            using (var plain = new PlainContext())
            using (var rewriting = new RewritingContext())
            {
                Assert.IsTrue(TopItemSql(plain).Contains("OUTER APPLY"));
                Assert.IsTrue(TopItemSql(rewriting).Contains("LEFT JOIN"));
            }

            using (var rewriting = new RewritingContext())
            using (var plain = new PlainContext())
            {
                Assert.IsTrue(TopItemSql(rewriting).Contains("LEFT JOIN"));
                Assert.IsTrue(TopItemSql(plain).Contains("OUTER APPLY"));
            }
        }

        [TestMethod]
        public void Both_forms_return_the_same_rows()
        {
            using var plain = new PlainContext();
            using var rewriting = new RewritingContext();

            var expected = plain.CreateQuery<ApplyOwner>()
                                .OrderBy(o => o.Id)
                                .Select(o => new { o.Id, o.TopItem.Label })
                                .ToList();
            var actual = rewriting.CreateQuery<ApplyOwner>()
                                  .OrderBy(o => o.Id)
                                  .Select(o => new { o.Id, o.TopItem.Label })
                                  .ToList();

            // The owner with no items keeps its row (OUTER APPLY -> LEFT JOIN); the others get their highest score.
            CollectionAssert.AreEqual(new[] { "m9", null, "o4", "t6a" }, expected.Select(x => x.Label).ToArray());
            CollectionAssert.AreEqual(expected.Select(x => (x.Id, x.Label)).ToArray(), actual.Select(x => (x.Id, x.Label)).ToArray());
        }

        [TestMethod]
        public void A_cross_apply_with_more_than_one_row_per_key_returns_the_same_rows()
        {
            using var plain = new PlainContext();
            using var rewriting = new RewritingContext();

            var expected = Query(plain);
            var actual = Query(rewriting);

            // owner 1 -> its top two, owner 3 -> its only one, owner 4 -> both, owner 2 -> no row at all.
            CollectionAssert.AreEqual(new[] { "1:m9", "1:m7", "3:o4", "4:t6a", "4:t2" }, expected);
            CollectionAssert.AreEqual(expected, actual);

            string[] Query(Atis.Orm.DataContext db)
            {
                var items = db.CreateQuery<ApplyItem>();
                var q = db.CreateQuery<ApplyOwner>()
                          .SelectMany(o => items.Where(i => i.OwnerId == o.Id).OrderByDescending(i => i.Score).Take(2),
                                      (o, i) => new { o.Id, i.Label, i.Score })
                          .OrderBy(r => r.Id).ThenByDescending(r => r.Score);
                Console.WriteLine(db.TranslateToSql(q));
                return q.ToList().Select(r => r.Id + ":" + r.Label).ToArray();
            }
        }
    }
}
