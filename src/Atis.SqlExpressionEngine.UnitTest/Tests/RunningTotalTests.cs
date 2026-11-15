using System;
using System.Data;
using System.Data.Common;
using System.Linq;

using Atis.Orm;
using Atis.Orm.Annotations;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     Runs running totals (a windowed <c>SUM</c> with a frame) against a real database and checks the numbers.
    ///     The text tests in <c>SqlFunctionTests</c> show the SQL is shaped right; these show it adds up right.
    /// </summary>
    [TestClass]
    public class RunningTotalTests : TestBase
    {
        private const string ServerConnectionString =
            "Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True";

        [TestInitialize]
        public void EnsureDatabase() => new TestDatabaseSetup(ServerConnectionString).Setup();

        [DbTable("RtLedger", "dbo")]
        public class LedgerRow
        {
            public int Id { get; set; }
            public string Account { get; set; }
            public int Day { get; set; }
            public decimal Amount { get; set; }
        }

        public class Line
        {
            public int Id { get; set; }
            public decimal? Running { get; set; }
        }

        private static void Seed(OrmDbContext db)
        {
            void Run(string sql) => db.GetDbCommunication().ExecuteNonQueryCommand(sql, Array.Empty<DbParameter>(), CommandType.Text);

            Run("CREATE TABLE dbo.RtLedger (Id INT NOT NULL, Account NVARCHAR(10) NOT NULL, Day INT NOT NULL, Amount DECIMAL(18,2) NOT NULL)");
            // Account A: 10, 20, 30 on days 1, 2, 3.
            // Account B: 5, -2, 7 on days 1, 2, 3 (a negative amount, so the total goes down).
            // Account C: two rows on the SAME day (tied order key): 4 and 6.
            Run(@"INSERT INTO dbo.RtLedger (Id, Account, Day, Amount) VALUES
                    (1, 'A', 1, 10), (2, 'A', 2, 20), (3, 'A', 3, 30),
                    (4, 'B', 1, 5), (5, 'B', 2, -2), (6, 'B', 3, 7),
                    (7, 'C', 1, 4), (8, 'C', 1, 6)");
        }

        [TestMethod]
        public void Running_total_per_account_runs_and_adds_up()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                Seed(db);

                var rows = db.CreateQuery<LedgerRow>()
                    .Select(x => new Line
                    {
                        Id = x.Id,
                        Running = Sql.Sum(x.Amount, Sql.Over().PartitionBy(x.Account).OrderBy(x.Id).Rows(WindowFrameBound.UnboundedPreceding)),
                    })
                    .OrderBy(x => x.Id)
                    .ToList();

                // The total restarts for each account and grows row by row inside it.
                CollectionAssert.AreEqual(
                    new decimal?[] { 10, 30, 60, 5, 3, 10, 4, 10 },
                    rows.Select(r => r.Running).ToArray());
            });
        }

        [TestMethod]
        public void Running_total_over_the_whole_table_has_no_partition()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                Seed(db);

                var rows = db.CreateQuery<LedgerRow>()
                    .Select(x => new Line
                    {
                        Id = x.Id,
                        Running = Sql.Sum(x.Amount, Sql.Over().OrderBy(x.Id).Rows(WindowFrameBound.UnboundedPreceding)),
                    })
                    .OrderBy(x => x.Id)
                    .ToList();

                // 10, 30, 60, 65, 63, 70, 74, 80
                CollectionAssert.AreEqual(
                    new decimal?[] { 10, 30, 60, 65, 63, 70, 74, 80 },
                    rows.Select(r => r.Running).ToArray());
            });
        }

        [TestMethod]
        public void Rows_gives_tied_rows_their_own_total_but_range_gives_them_the_same()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                Seed(db);

                // Account C has two rows on the same Day. Ordering by Day only, they tie.
                var byRows = db.CreateQuery<LedgerRow>()
                    .Where(x => x.Account == "C")
                    .Select(x => new Line
                    {
                        Id = x.Id,
                        Running = Sql.Sum(x.Amount, Sql.Over().OrderBy(x.Day).Rows(WindowFrameBound.UnboundedPreceding)),
                    })
                    .ToList();

                var byRange = db.CreateQuery<LedgerRow>()
                    .Where(x => x.Account == "C")
                    .Select(x => new Line
                    {
                        Id = x.Id,
                        Running = Sql.Sum(x.Amount, Sql.Over().OrderBy(x.Day).Range(WindowFrameBound.UnboundedPreceding)),
                    })
                    .ToList();

                // ROWS: the totals are 4 then 10 or 6 then 10 (the DB picks the order of tied rows), but the two are never equal
                // and the last one is the full sum.
                Assert.AreEqual(2, byRows.Count);
                Assert.AreNotEqual(byRows[0].Running, byRows[1].Running);
                Assert.AreEqual(10m, byRows.Max(r => r.Running));

                // RANGE: both tied rows see the full sum.
                CollectionAssert.AreEqual(new decimal?[] { 10, 10 }, byRange.Select(r => r.Running).ToArray());
            });
        }

        [TestMethod]
        public void Running_total_over_a_group_total_runs_and_adds_up()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                Seed(db);

                var rows = db.CreateQuery<LedgerRow>()
                    .GroupBy(x => x.Day)
                    .Select(g => new
                    {
                        Day = g.Key,
                        Total = g.Sum(e => e.Amount),
                        Running = Sql.Sum(g.Sum(e => e.Amount), Sql.Over().OrderBy(g.Key).Rows(WindowFrameBound.UnboundedPreceding)),
                    })
                    .OrderBy(x => x.Day)
                    .ToList();

                // Day 1: 10 + 5 + 4 + 6 = 25; day 2: 20 - 2 = 18; day 3: 30 + 7 = 37.
                CollectionAssert.AreEqual(new[] { 1, 2, 3 }, rows.Select(r => r.Day).ToArray());
                CollectionAssert.AreEqual(new decimal?[] { 25, 18, 37 }, rows.Select(r => (decimal?)r.Total).ToArray());
                CollectionAssert.AreEqual(new decimal?[] { 25, 43, 80 }, rows.Select(r => (decimal?)r.Running).ToArray());
            });
        }
    }
}
