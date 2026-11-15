using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;

using Atis.Orm;
using Atis.Orm.Annotations;
using Atis.Orm.SqlServer;
using Atis.Orm.Translation;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         Backlog item 25: a "bin allocation" query. Pick-list rows are numbered inside their
    ///         (DODAAC, NIIN, condition) group, bins are given a running quantity range (a windowed
    ///         <c>SUM</c> with a <c>ROWS</c> frame, in a second derived table), and a LEFT JOIN with a
    ///         range in its ON clause puts each pick-list row into the bin whose range holds its number.
    ///     </para>
    ///     <para>
    ///         The text tests show the shape; the execution test runs it with ties in the bin order, a
    ///         group with no bins and a group where the bins run out - text alone has hidden problems before.
    ///     </para>
    /// </summary>
    [TestClass]
    public class BinAllocationTests : TestBase
    {
        private const string ServerConnectionString =
            "Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True";

        [TestInitialize]
        public void EnsureDatabase() => new TestDatabaseSetup(ServerConnectionString).Setup();

        [DbTable("BinPb", "dbo")]
        public class PbRow
        {
            public int PBN { get; set; }
            public string DODAAC { get; set; }
            public string NIIN { get; set; }
            public string ConditionCode { get; set; }
            public string CurrentLocationStatus { get; set; }
        }

        [DbTable("BinQty", "dbo")]
        public class QtyBin
        {
            public string LOCNCODE { get; set; }
            public string ITEMNMBR { get; set; }
            public string QTY_TYPE { get; set; }
            public string BIN { get; set; }
            public int QTY { get; set; }
        }

        public class Result
        {
            public int PBN { get; set; }
            public string DODAAC { get; set; }
            public string NIIN { get; set; }
            public string ConditionCode { get; set; }
            public string BIN { get; set; }
            public string CurrentLocationStatus { get; set; }
        }

        private static IQueryable<Result> Build(IQueryable<PbRow> pb, IQueryable<QtyBin> qtyBin)
        {
            // p: every pick-list column plus its number inside the group.
            var p = pb.Select(x => new
            {
                Row = x,
                RN = Sql.RowNumber(Sql.Over().PartitionBy(x.DODAAC).PartitionBy(x.NIIN).PartitionBy(x.ConditionCode).OrderBy(x.PBN)),
            });

            // b, inner: every bin column plus the running quantity at the END of the bin.
            // The cast is SQL's (CONVERT(bigint, ..)): a C# (long?) cast would be dropped and the sum could overflow an int.
            var running = qtyBin.Select(x => new
            {
                Row = x,
                EndRN = Sql.Sum(SqlServerFunctions.CastToBigInt(x.QTY), Sql.Over().PartitionBy(x.LOCNCODE).PartitionBy(x.ITEMNMBR).PartitionBy(x.QTY_TYPE).OrderBy(x.BIN).Rows(WindowFrameBound.UnboundedPreceding)),
            });

            // b, outer: the START of the bin's range, from the running quantity (a window function cannot be used in the same SELECT).
            var b = running.Select(x => new { x.Row, x.EndRN, StartRN = x.EndRN - SqlServerFunctions.CastToBigInt(x.Row.QTY) + 1 });

            return p.LeftJoin(b, (pp, bb) => new { pp, bb },
                              j => j.bb.Row.LOCNCODE == j.pp.Row.DODAAC
                                   && j.bb.Row.ITEMNMBR == j.pp.Row.NIIN
                                   && j.bb.Row.QTY_TYPE == j.pp.Row.ConditionCode
                                   && j.pp.RN >= j.bb.StartRN
                                   && j.pp.RN <= j.bb.EndRN)
                    .Select(j => new Result
                    {
                        PBN = j.pp.Row.PBN,
                        DODAAC = j.pp.Row.DODAAC,
                        NIIN = j.pp.Row.NIIN,
                        ConditionCode = j.pp.Row.ConditionCode,
                        BIN = j.bb.Row.BIN,
                        CurrentLocationStatus = j.pp.Row.CurrentLocationStatus,
                    })
                    .OrderBy(x => x.PBN);
        }

        private string TranslateForSqlServer(Expression expression)
        {
            var sqlExpression = ConvertExpressionToSqlExpression(expression, out _);
            var translation = new SqlServerSqlExpressionTranslator().Translate(sqlExpression);
            var renderer = new CommandRenderer(new SqlDbParameterFactory(new SqlDbParameterNameGenerator()));
            var sql = renderer.Render(translation.Fragments, p => p.InitialValue).Sql;
            Console.WriteLine(sql);
            return sql;
        }

        #region let with a window function

        // `Select(x => new { x, extra })` is a LINQ `let`: the engine only records the new member and a later Where / Join
        // writes its expression again. A window function cannot be written in a WHERE or an ON clause, so a let that
        // computes one must become a real Select level (a derived table) first.

        [TestMethod]
        public void Let_with_a_window_function_is_a_derived_table_before_a_where()
        {
            var pb = new Queryable<PbRow>(queryProvider);
            var q = pb.Select(x => new { Row = x, RN = Sql.RowNumber(Sql.Over().OrderBy(x.PBN)) }).Where(x => x.RN <= 2);
            var sql = TranslateForSqlServer(q.Expression);
            StringAssert.Contains(sql, "WHERE (t1.[RN] <= @p0)");
            StringAssert.Contains(sql, "ROW_NUMBER() OVER (ORDER BY t2.[PBN] ASC) AS [RN]");
            Assert.IsFalse(sql.Contains("WHERE (ROW_NUMBER()"), "the window function must not be written in the WHERE");
        }

        [TestMethod]
        public void Let_with_a_window_function_is_a_derived_table_before_a_join()
        {
            var pb = new Queryable<PbRow>(queryProvider);
            var qb = new Queryable<QtyBin>(queryProvider);
            var q = pb.Select(x => new { Row = x, RN = Sql.RowNumber(Sql.Over().OrderBy(x.PBN)) })
                      .LeftJoin(qb, (pp, bb) => new { pp, bb }, j => j.bb.LOCNCODE == j.pp.Row.NIIN && j.pp.RN >= j.bb.QTY);
            var sql = TranslateForSqlServer(q.Expression);
            StringAssert.Contains(sql, "AND (t1.[RN] >= t2.[QTY])");
            Assert.IsFalse(sql.Contains("AND (ROW_NUMBER()"), "the window function must not be written in the ON clause");
        }

        [TestMethod]
        public void Let_with_a_plain_expression_is_still_written_inline()
        {
            var pb = new Queryable<PbRow>(queryProvider);
            var q = pb.Select(x => new { Row = x, Dbl = x.PBN * 2 }).Where(x => x.Dbl > 2);
            var sql = TranslateForSqlServer(q.Expression);
            StringAssert.Contains(sql, "WHERE ((t1.[PBN] * @p1) > @p2)");
            Assert.IsFalse(sql.Contains("FROM (\r\nSELECT") || sql.Contains("FROM (\nSELECT"), "a plain let must not add a derived table");
        }

        #endregion

        [TestMethod]
        public void Bin_allocation_query_text()
        {
            var q = Build(new Queryable<PbRow>(queryProvider), new Queryable<QtyBin>(queryProvider));
            var sql = TranslateForSqlServer(q.Expression);

            // pick-list side: numbered rows in a derived table
            StringAssert.Contains(sql, "ROW_NUMBER() OVER (PARTITION BY t3.[DODAAC], t3.[NIIN], t3.[ConditionCode] ORDER BY t3.[PBN] ASC) AS [RN]");
            // bin side: running quantity in an inner derived table, with the frame and the real cast, then the start of the range
            StringAssert.Contains(sql, "SUM(cast(t5.[QTY] as bigint)) OVER (PARTITION BY t5.[LOCNCODE], t5.[ITEMNMBR], t5.[QTY_TYPE] ORDER BY t5.[BIN] ASC ROWS UNBOUNDED PRECEDING) AS [EndRN]");
            StringAssert.Contains(sql, "((t4.[EndRN] - cast(t4.[QTY] as bigint)) + @p0) AS [StartRN]");
            // the range stays inside the ON clause, next to the key comparisons
            StringAssert.Contains(sql, "AND (t1.[RN] >= t2.[StartRN])) AND (t1.[RN] <= t2.[EndRN]))");
            StringAssert.Contains(sql, "LEFT JOIN (");
            StringAssert.Contains(sql, "ORDER BY [PBN] ASC");
        }

        [TestMethod]
        public void Bin_allocation_runs_with_ties_missing_bins_and_bins_running_out()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                void Run(string sql) => db.GetDbCommunication().ExecuteNonQueryCommand(sql, Array.Empty<DbParameter>(), CommandType.Text);

                Run("CREATE TABLE dbo.BinPb (PBN INT NOT NULL, DODAAC NVARCHAR(10) NOT NULL, NIIN NVARCHAR(10) NOT NULL, ConditionCode NVARCHAR(5) NOT NULL, CurrentLocationStatus NVARCHAR(20) NULL)");
                Run("CREATE TABLE dbo.BinQty (LOCNCODE NVARCHAR(10) NOT NULL, ITEMNMBR NVARCHAR(10) NOT NULL, QTY_TYPE NVARCHAR(5) NOT NULL, BIN NVARCHAR(10) NOT NULL, QTY INT NOT NULL)");

                // Group 1: five pick rows, three bins holding 2, 1 and 1 - the fifth row finds no bin left.
                // Group 2: two pick rows and no bins at all.
                // Group 3: three pick rows, two bins that tie on BIN (same name) holding 1 each. ROWS gives each its own
                //          running total (1, then 2); the default RANGE would give both the total 2 and match rows twice.
                Run(@"INSERT INTO dbo.BinPb (PBN, DODAAC, NIIN, ConditionCode, CurrentLocationStatus) VALUES
                        (1, 'D1', 'N1', 'A', 's1'), (2, 'D1', 'N1', 'A', 's2'), (3, 'D1', 'N1', 'A', 's3'), (4, 'D1', 'N1', 'A', 's4'), (5, 'D1', 'N1', 'A', 's5'),
                        (6, 'D2', 'N2', 'A', 's6'), (7, 'D2', 'N2', 'A', 's7'),
                        (8, 'D3', 'N3', 'A', 's8'), (9, 'D3', 'N3', 'A', 's9'), (10, 'D3', 'N3', 'A', 's10')");
                Run(@"INSERT INTO dbo.BinQty (LOCNCODE, ITEMNMBR, QTY_TYPE, BIN, QTY) VALUES
                        ('D1', 'N1', 'A', 'b1', 2), ('D1', 'N1', 'A', 'b2', 1), ('D1', 'N1', 'A', 'b3', 1),
                        ('D3', 'N3', 'A', 'b4', 1), ('D3', 'N3', 'A', 'b4', 1)");

                var rows = Build(db.CreateQuery<PbRow>(), db.CreateQuery<QtyBin>()).ToList();

                // one result row per pick row: no duplicates, none lost
                CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, rows.Select(r => r.PBN).ToArray());
                CollectionAssert.AreEqual(
                    new[] { "b1", "b1", "b2", "b3", null, null, null, "b4", "b4", null },
                    rows.Select(r => r.BIN).ToArray());
                // the other columns come from the pick row
                Assert.AreEqual("s5", rows[4].CurrentLocationStatus);
                Assert.AreEqual("D3", rows[8].DODAAC);
            });
        }
    }
}
