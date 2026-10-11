using Atis.Orm;
using Atis.Orm.SqlServer;
using System;
using System.Linq;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         The neutral <see cref="Sql"/> window functions and <see cref="SqlServerFunctions"/> actually
    ///         running against a real SQL Server - the counterpart to <see cref="SqlFunctionTests"/>, which
    ///         checks the generated text only.
    ///     </para>
    ///     <para>
    ///         The text alone proves little here: whether <c>NTILE(@p)</c> or <c>LAG(x, @p)</c> accept a
    ///         parameter, and what type comes back for a null <c>LAG</c>, are answers only the server gives.
    ///         Each test seeds its own rows inside <c>TransactionWithRollback</c> and filters to them, so
    ///         the shared fixture rows cannot change the numbers.
    ///     </para>
    /// </summary>
    [TestClass]
    public class SqlFunctionExecutionTests
    {
        private const string ServerConnectionString =
            "Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True";

        private const string Marker = "SqlFunctions";

        [TestInitialize]
        public void EnsureDatabase() => new TestDatabaseSetup(ServerConnectionString).Setup();

        private static void Seed(OrmDbContext db, string firstName, decimal salary, DateTime hireDate)
        {
            db.InsertEntity<TestEntities.Employee>()
              .Value(x => x.FirstName, () => firstName)
              .Value(x => x.LastName, () => Marker)
              .Value(x => x.Email, () => firstName.ToLower() + "@sqlfunctions.test")
              .Value(x => x.Salary, () => salary)
              .Value(x => x.HireDate, () => (DateTime?)hireDate)
              .Execute();
        }

        private static void SeedFour(OrmDbContext db)
        {
            // Two people share the top salary, so Rank and RowNumber differ.
            Seed(db, "Ann", 300m, new DateTime(2020, 1, 1));
            Seed(db, "Bob", 300m, new DateTime(2020, 1, 11));
            Seed(db, "Cid", 200m, new DateTime(2020, 2, 1));
            Seed(db, "Dee", 100m, new DateTime(2021, 1, 1));
        }

        [TestMethod]
        public void RowNumber_Rank_and_DenseRank_number_the_rows()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                SeedFour(db);

                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(x => x.LastName == Marker)
                             .Select(x => new
                             {
                                 x.FirstName,
                                 Rn = Sql.RowNumber(Sql.Over().OrderByDescending(x.Salary).OrderBy(x.FirstName)),
                                 Rk = Sql.Rank(Sql.Over().OrderByDescending(x.Salary)),
                                 Dr = Sql.DenseRank(Sql.Over().OrderByDescending(x.Salary)),
                             })
                             .ToList()
                             .OrderBy(r => r.Rn)
                             .ToList();

                CollectionAssert.AreEqual(new[] { "Ann", "Bob", "Cid", "Dee" }, rows.Select(r => r.FirstName).ToArray());
                CollectionAssert.AreEqual(new long[] { 1, 2, 3, 4 }, rows.Select(r => r.Rn).ToArray());
                CollectionAssert.AreEqual(new long[] { 1, 1, 3, 4 }, rows.Select(r => r.Rk).ToArray());
                CollectionAssert.AreEqual(new long[] { 1, 1, 2, 3 }, rows.Select(r => r.Dr).ToArray());
            });
        }

        [TestMethod]
        public void Result_can_be_filtered_in_a_following_where()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                SeedFour(db);

                var top = db.CreateQuery<TestEntities.Employee>()
                            .Where(x => x.LastName == Marker)
                            .Select(x => new { x.FirstName, Rn = Sql.RowNumber(Sql.Over().OrderByDescending(x.Salary).OrderBy(x.FirstName)) })
                            .Where(x => x.Rn <= 2)
                            .ToList();

                CollectionAssert.AreEquivalent(new[] { "Ann", "Bob" }, top.Select(r => r.FirstName).ToArray());
            });
        }

        [TestMethod]
        public void Ntile_and_Lag_accept_parameters_and_return_null_when_there_is_no_row()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                SeedFour(db);

                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(x => x.LastName == Marker)
                             .Select(x => new
                             {
                                 x.FirstName,
                                 Half = Sql.Ntile(2, Sql.Over().OrderBy(x.HireDate)),
                                 Prev = Sql.Lag((decimal?)x.Salary, Sql.Over().OrderBy(x.HireDate)),
                                 Prev2 = Sql.Lag((decimal?)x.Salary, 2, Sql.Over().OrderBy(x.HireDate)),
                                 Next = Sql.Lead((decimal?)x.Salary, 1, 0m, Sql.Over().OrderBy(x.HireDate)),
                                 First = Sql.FirstValue(x.FirstName, Sql.Over().OrderBy(x.HireDate)),
                             })
                             .ToList()
                             .OrderBy(r => r.FirstName)
                             .ToList();

                // Hire order is Ann, Bob, Cid, Dee.
                CollectionAssert.AreEqual(new long[] { 1, 1, 2, 2 }, rows.Select(r => r.Half).ToArray());
                CollectionAssert.AreEqual(new decimal?[] { null, 300m, 300m, 200m }, rows.Select(r => r.Prev).ToArray());
                CollectionAssert.AreEqual(new decimal?[] { null, null, 300m, 300m }, rows.Select(r => r.Prev2).ToArray());
                CollectionAssert.AreEqual(new decimal?[] { 300m, 200m, 100m, 0m }, rows.Select(r => r.Next).ToArray());
                Assert.IsTrue(rows.All(r => r.First == "Ann"));
            });
        }

        [TestMethod]
        public void Partition_restarts_the_numbering()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                SeedFour(db);

                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(x => x.LastName == Marker)
                             .Select(x => new
                             {
                                 x.FirstName,
                                 Rn = Sql.RowNumber(Sql.Over().PartitionBy(x.Salary).OrderBy(x.FirstName)),
                             })
                             .ToList()
                             .OrderBy(r => r.FirstName)
                             .ToList();

                // Ann and Bob share a salary, so they number 1 and 2; Cid and Dee are alone.
                CollectionAssert.AreEqual(new long[] { 1, 2, 1, 1 }, rows.Select(r => r.Rn).ToArray());
            });
        }

        [TestMethod]
        public void SqlServerFunctions_run_on_the_server()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                SeedFour(db);

                var start = new DateTime(2020, 1, 1);
                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(x => x.LastName == Marker)
                             .Select(x => new
                             {
                                 x.FirstName,
                                 Pos = SqlServerFunctions.CharIndex("n", x.FirstName),
                                 Len = SqlServerFunctions.Len(x.FirstName),
                                 Days = SqlServerFunctions.DateDiff(DatePart.day, start, x.HireDate),
                                 Month = SqlServerFunctions.DatePart(DatePart.month, x.HireDate),
                                 Plus = SqlServerFunctions.DateAdd(DatePart.day, 5, x.HireDate),
                                 Text = SqlServerFunctions.CastToVarchar(x.Salary, 10),
                             })
                             .ToList()
                             .OrderBy(r => r.FirstName)
                             .ToList();

                CollectionAssert.AreEqual(new int?[] { 2, 0, 0, 0 }, rows.Select(r => r.Pos).ToArray());
                CollectionAssert.AreEqual(new int?[] { 3, 3, 3, 3 }, rows.Select(r => r.Len).ToArray());
                CollectionAssert.AreEqual(new int?[] { 0, 10, 31, 366 }, rows.Select(r => r.Days).ToArray());
                CollectionAssert.AreEqual(new int?[] { 1, 1, 2, 1 }, rows.Select(r => r.Month).ToArray());
                Assert.AreEqual(new DateTime(2020, 1, 6), rows[0].Plus);
                StringAssert.StartsWith(rows[0].Text, "300");
            });
        }

        [TestMethod]
        public void DataLength_and_Rand_materialize_whatever_type_the_server_returns()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                SeedFour(db);

                // DATALENGTH returns int for an nvarchar(50) column but bigint for a (max) type (the template casts both to bigint), and RAND
                // returns float. Both are read into long? / double?, so a strict reader would throw here.
                var row = db.CreateQuery<TestEntities.Employee>()
                            .Where(x => x.LastName == Marker && x.FirstName == "Ann")
                            .Select(x => new
                            {
                                Narrow = SqlServerFunctions.DataLength(x.FirstName),
                                Wide = SqlServerFunctions.DataLength(SqlServerFunctions.CastToVarchar(x.FirstName)),
                                Random = SqlServerFunctions.Rand(42),
                            })
                            .ToList()
                            .Single();

                Assert.AreEqual(6L, row.Narrow, "'Ann' is 3 nvarchar characters, 2 bytes each.");
                Assert.AreEqual(3L, row.Wide, "'Ann' is 3 varchar characters, 1 byte each.");
                Assert.IsTrue(row.Random >= 0 && row.Random < 1);
            });
        }

        [TestMethod]
        public void A_captured_variable_in_a_brace_argument_rebinds_on_the_second_run()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                SeedFour(db);

                DateTime?[] Run(int days)
                    => db.CreateQuery<TestEntities.Employee>()
                         .Where(x => x.LastName == Marker && x.FirstName == "Ann")
                         .Select(x => SqlServerFunctions.DateAdd(DatePart.day, days, x.HireDate))
                         .ToList()
                         .ToArray();

                Assert.AreEqual(new DateTime(2020, 1, 4), Run(3)[0]);
                Assert.AreEqual(new DateTime(2020, 1, 11), Run(10)[0], "The second run must use its own value, not the first run's.");
            });
        }
    }
}
