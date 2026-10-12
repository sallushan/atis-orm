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
        public void String_functions_run_on_the_server()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                SeedFour(db);

                var r = db.CreateQuery<TestEntities.Employee>()
                          .Where(x => x.LastName == Marker && x.FirstName == "Ann")
                          .Select(x => new
                          {
                              Up = SqlServerFunctions.Upper(x.FirstName),
                              Lo = SqlServerFunctions.Lower(x.FirstName),
                              L = SqlServerFunctions.Left(x.FirstName, 2),
                              R = SqlServerFunctions.Right(x.FirstName, 2),
                              Sub = SqlServerFunctions.SubString(x.FirstName, 2, 2),
                              Rep = SqlServerFunctions.Replace(x.FirstName, "n", "m"),
                              Rev = SqlServerFunctions.Reverse(x.FirstName),
                              Rpt = SqlServerFunctions.Replicate(x.FirstName, 2),
                              Sp = SqlServerFunctions.Space(2),
                              Cat = SqlServerFunctions.Concat(x.FirstName, "-", x.LastName),
                              CatWs = SqlServerFunctions.Concat_WS("/", x.FirstName, x.LastName),
                              Lt = SqlServerFunctions.LTrim("  a "),
                              Rt = SqlServerFunctions.RTrim(" a  "),
                              Tr = SqlServerFunctions.Trim("  a  "),
                              TrC = SqlServerFunctions.Trim("x", "xxaxx"),
                              Stf = SqlServerFunctions.Stuff(x.FirstName, 1, 1, "Z"),
                              Tl = SqlServerFunctions.Translate(x.FirstName, "A", "B"),
                              Nc = SqlServerFunctions.NChar(65),
                              Un = SqlServerFunctions.Unicode(x.FirstName),
                              Esc = SqlServerFunctions.String_Escape("a\"b", "json"),
                          })
                          .ToList()
                          .Single();

                Assert.AreEqual("ANN", r.Up);
                Assert.AreEqual("ann", r.Lo);
                Assert.AreEqual("An", r.L);
                Assert.AreEqual("nn", r.R);
                Assert.AreEqual("nn", r.Sub);
                Assert.AreEqual("Amm", r.Rep);
                Assert.AreEqual("nnA", r.Rev);
                Assert.AreEqual("AnnAnn", r.Rpt);
                Assert.AreEqual("  ", r.Sp);
                Assert.AreEqual("Ann-SqlFunctions", r.Cat);
                Assert.AreEqual("Ann/SqlFunctions", r.CatWs);
                Assert.AreEqual("a ", r.Lt);
                Assert.AreEqual(" a", r.Rt);
                Assert.AreEqual("a", r.Tr);
                Assert.AreEqual("a", r.TrC);
                Assert.AreEqual("Znn", r.Stf);
                Assert.AreEqual("Bnn", r.Tl);
                Assert.AreEqual("A", r.Nc);
                Assert.AreEqual(65, r.Un);
                Assert.AreEqual("a\\\"b", r.Esc);
            });
        }

        [TestMethod]
        public void Math_functions_run_on_the_server()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                SeedFour(db);

                var r = db.CreateQuery<TestEntities.Employee>()
                          .Where(x => x.LastName == Marker && x.FirstName == "Cid")   // salary 200
                          .Select(x => new
                          {
                              AbsInt = SqlServerFunctions.Abs(-3),
                              AbsDec = SqlServerFunctions.Abs(x.Salary),
                              Ceil = SqlServerFunctions.Ceiling(x.Salary),
                              Floor = SqlServerFunctions.Floor(x.Salary),
                              Sign = SqlServerFunctions.Sign(-5L),
                              Round = SqlServerFunctions.Round(x.Salary, -2),
                              RoundD = SqlServerFunctions.Round(2.567, 1),
                              Pow = SqlServerFunctions.Power(x.Salary, 2m),
                              Sqrt = SqlServerFunctions.Sqrt(16.0),
                              Sq = SqlServerFunctions.Square(3.0),
                              Log10 = SqlServerFunctions.Log10(1000.0),
                              LogB = SqlServerFunctions.Log(8.0, 2),
                              Pi = SqlServerFunctions.Pi(),
                              Atn2 = SqlServerFunctions.Atn2(1.0, 1.0),
                              Deg = SqlServerFunctions.Degrees(3.141592653589793),
                              Rnd = SqlServerFunctions.Rand(),
                          })
                          .ToList()
                          .Single();

                Assert.AreEqual(3, r.AbsInt);
                Assert.AreEqual(200m, r.AbsDec);
                Assert.AreEqual(200m, r.Ceil);
                Assert.AreEqual(200m, r.Floor);
                Assert.AreEqual(-1L, r.Sign);
                Assert.AreEqual(200m, r.Round);
                Assert.AreEqual(2.6, r.RoundD.Value, 1e-9);
                Assert.AreEqual(40000m, r.Pow);
                Assert.AreEqual(4.0, r.Sqrt.Value, 1e-9);
                Assert.AreEqual(9.0, r.Sq.Value, 1e-9);
                Assert.AreEqual(3.0, r.Log10.Value, 1e-9);
                Assert.AreEqual(3.0, r.LogB.Value, 1e-9);
                Assert.AreEqual(Math.PI, r.Pi.Value, 1e-9);
                Assert.AreEqual(Math.PI / 4, r.Atn2.Value, 1e-9);
                Assert.AreEqual(180.0, r.Deg.Value, 1e-9);
                Assert.IsTrue(r.Rnd >= 0 && r.Rnd < 1);
            });
        }

        [TestMethod]
        public void Logical_date_and_system_functions_run_on_the_server()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                SeedFour(db);

                DateTime? fallback = new DateTime(1999, 1, 1);
                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(x => x.LastName == Marker && x.HireDate < SqlServerFunctions.GetDate())
                             .Select(x => new
                             {
                                 x.FirstName,
                                 Big = SqlServerFunctions.Iif(x.Salary > 250m, "high", "low"),
                                 Pick = SqlServerFunctions.Choose(2, "a", "b", "c"),
                                 Nn = SqlServerFunctions.IsNull(x.HireDate, fallback),
                                 Y = SqlServerFunctions.Year(x.HireDate),
                                 M = SqlServerFunctions.Month(x.HireDate),
                                 D = SqlServerFunctions.Day(x.HireDate),
                                 Eom = SqlServerFunctions.EOMonth(x.HireDate),
                                 Eom2 = SqlServerFunctions.EOMonth(x.HireDate, 1),
                                 Df = SqlServerFunctions.DateFromParts(2024, 2, 29),
                                 Dtf = SqlServerFunctions.DateTimeFromParts(2024, 2, 29, 13, 14, 15, 0),
                                 Dt2 = SqlServerFunctions.DateTime2FromParts(2024, 2, 29, 13, 14, 15, 0, 0),
                                 Tf = SqlServerFunctions.TimeFromParts(13, 14, 15, 0, 0),
                                 Now = SqlServerFunctions.GetDate(),
                                 Utc = SqlServerFunctions.SysUTCDateTime(),
                                 Id = SqlServerFunctions.NewID(),
                                 Cu = SqlServerFunctions.Current_User(),
                                 U = SqlServerFunctions.User(),
                                 Un = SqlServerFunctions.User_Name(),
                                 Bit = SqlServerFunctions.CastToBit(1),
                                 Date = SqlServerFunctions.CastToDate(x.HireDate),
                             })
                             .ToList()
                             .OrderBy(r => r.FirstName)
                             .ToList();

                CollectionAssert.AreEqual(new[] { "high", "high", "low", "low" }, rows.Select(r => r.Big).ToArray());
                Assert.AreEqual("b", rows[0].Pick);
                Assert.AreEqual(new DateTime(2020, 1, 1), rows[0].Nn);
                Assert.AreEqual(2020, rows[0].Y);
                Assert.AreEqual(1, rows[0].M);
                Assert.AreEqual(1, rows[0].D);
                Assert.AreEqual(new DateTime(2020, 1, 31), rows[0].Eom);
                Assert.AreEqual(new DateTime(2020, 2, 29), rows[0].Eom2);
                Assert.AreEqual(new DateTime(2024, 2, 29), rows[0].Df);
                Assert.AreEqual(new DateTime(2024, 2, 29, 13, 14, 15), rows[0].Dtf);
                Assert.AreEqual(new DateTime(2024, 2, 29, 13, 14, 15), rows[0].Dt2);
                Assert.AreEqual(new TimeSpan(13, 14, 15), rows[0].Tf);
                Assert.IsTrue(rows[0].Now.Year >= 2026);
                Assert.IsTrue(rows[0].Utc.Year >= 2026);
                Assert.AreNotEqual(rows[0].Id, rows[1].Id, "NEWID must be evaluated per row, not once.");
                Assert.AreEqual(rows[0].Cu, rows[0].U);
                Assert.AreEqual(rows[0].Cu, rows[0].Un);
                Assert.AreEqual(true, rows[0].Bit);
                Assert.AreEqual(new DateTime(2020, 1, 1), rows[0].Date);
            });
        }

        [TestMethod]
        public void Aggregate_functions_run_on_the_server_without_a_group_by()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                SeedFour(db);   // salaries 300, 300, 200, 100

                // Every column is an aggregate, so the whole filtered set is one group.
                var r = db.CreateQuery<TestEntities.Employee>()
                          .Where(x => x.LastName == Marker)
                          .Select(x => new
                          {
                              Rows = SqlServerFunctions.Count(),
                              Named = SqlServerFunctions.Count(x.FirstName),
                              DistinctCount = SqlServerFunctions.CountDistinct(x.Salary),
                              Big = SqlServerFunctions.Count_Big(),
                              BigDistinct = SqlServerFunctions.Count_BigDistinct(x.Salary),
                              Approx = SqlServerFunctions.Approx_Count_Distinct(x.Salary),
                              Sum = SqlServerFunctions.Sum(x.Salary),
                              SumDistinct = SqlServerFunctions.SumDistinct(x.Salary),
                              Avg = SqlServerFunctions.Avg(x.Salary),
                              AvgDistinct = SqlServerFunctions.AvgDistinct(x.Salary),
                              AvgInt = SqlServerFunctions.Avg(x.EmployeeId),
                              SumInt = SqlServerFunctions.Sum(x.EmployeeId),
                              Min = SqlServerFunctions.Min(x.Salary),
                              Max = SqlServerFunctions.Max(x.Salary),
                              MaxName = SqlServerFunctions.Max(x.FirstName),
                              MinDistinct = SqlServerFunctions.MinDistinct(x.Salary),
                              Stdev = SqlServerFunctions.Stdev(x.Salary),
                              Stdevp = SqlServerFunctions.Stdevp(x.Salary),
                              Var = SqlServerFunctions.Var(x.Salary),
                              Varp = SqlServerFunctions.Varp(x.Salary),
                              VarDistinct = SqlServerFunctions.VarDistinct(x.Salary),
                          })
                          .ToList()
                          .Single();

                Assert.AreEqual(4, r.Rows);
                Assert.AreEqual(4, r.Named);
                Assert.AreEqual(3, r.DistinctCount);
                Assert.AreEqual(4L, r.Big);
                Assert.AreEqual(3L, r.BigDistinct);
                Assert.AreEqual(3L, r.Approx);
                Assert.AreEqual(900m, r.Sum);
                Assert.AreEqual(600m, r.SumDistinct);
                Assert.AreEqual(225m, r.Avg);
                Assert.AreEqual(200m, r.AvgDistinct);
                Assert.IsNotNull(r.AvgInt);
                Assert.IsNotNull(r.SumInt);
                Assert.AreEqual(100m, r.Min);
                Assert.AreEqual(300m, r.Max);
                Assert.AreEqual("Dee", r.MaxName);
                Assert.AreEqual(100m, r.MinDistinct);
                Assert.AreEqual(Math.Sqrt(27500.0 / 3), r.Stdev.Value, 1e-6);
                Assert.AreEqual(Math.Sqrt(27500.0 / 4), r.Stdevp.Value, 1e-6);
                Assert.AreEqual(27500.0 / 3, r.Var.Value, 1e-6);
                Assert.AreEqual(27500.0 / 4, r.Varp.Value, 1e-6);
                Assert.AreEqual(10000.0, r.VarDistinct.Value, 1e-6);
            });
        }

        [TestMethod]
        public void String_Agg_and_Percentile_run_on_the_server()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                SeedFour(db);   // Ann 300, Bob 300, Cid 200, Dee 100

                var r = db.CreateQuery<TestEntities.Employee>()
                          .Where(x => x.LastName == Marker)
                          .Select(x => new
                          {
                              Names = SqlServerFunctions.String_Agg(x.FirstName, ", ", Sql.Over().OrderByDescending(x.Salary).OrderBy(x.FirstName)),
                          })
                          .ToList()
                          .Single();
                Assert.AreEqual("Ann, Bob, Cid, Dee", r.Names);

                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(x => x.LastName == Marker)
                             .Select(x => new
                             {
                                 Cont = SqlServerFunctions.Percentile_Cont(0.5, Sql.Over().OrderBy(x.Salary), Sql.Over()),
                                 Disc = SqlServerFunctions.Percentile_Disc<decimal?>(0.5, Sql.Over().OrderBy(x.Salary), Sql.Over()),
                             })
                             .ToList();

                Assert.AreEqual(4, rows.Count, "A window function keeps every row.");
                Assert.AreEqual(250.0, rows[0].Cont.Value, 1e-9);   // between 200 and 300
                Assert.AreEqual(200m, rows[0].Disc);
            });
        }

        [TestMethod]
        public void Ordered_string_Join_on_a_group_runs_on_the_server()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                SeedFour(db);   // Ann 300, Bob 300, Cid 200, Dee 100

                // SQL Server allows only one ordering per query for ordered aggregates, so each is its own query.
                // A second aggregate that needs no ordering (Total) can sit next to it.
                var byPay = db.CreateQuery<TestEntities.Employee>()
                              .Where(x => x.LastName == Marker)
                              .GroupBy(x => x.LastName)
                              .Select(g => new
                              {
                                  LastName = g.Key,
                                  Names = string.Join(", ", g.OrderByDescending(y => y.Salary).ThenBy(y => y.FirstName).Select(y => y.FirstName)),
                                  Total = g.Sum(y => y.Salary),
                              })
                              .ToList()
                              .Single();
                Assert.AreEqual("Ann, Bob, Cid, Dee", byPay.Names);
                Assert.AreEqual(900m, byPay.Total, "Other aggregates on the same group must not be disturbed by the ordering.");

                var byName = db.CreateQuery<TestEntities.Employee>()
                               .Where(x => x.LastName == Marker)
                               .GroupBy(x => x.LastName)
                               .Select(g => new { LastName = g.Key, Names = string.Join("|", g.OrderByDescending(y => y.FirstName).Select(y => y.FirstName)) })
                               .ToList()
                               .Single();
                Assert.AreEqual("Dee|Cid|Bob|Ann", byName.Names);

                var joined = db.CreateQuery<TestEntities.Employee>()
                               .Where(x => x.LastName == Marker)
                               .GroupBy(x => x.LastName)
                               .Select(g => new { LastName = g.Key, Names = string.Concat(g.OrderBy(y => y.Salary).ThenBy(y => y.FirstName).Select(y => y.FirstName)) })
                               .ToList()
                               .Single();
                Assert.AreEqual("DeeCidAnnBob", joined.Names);
            });
        }

        [TestMethod]
        public void Count_with_a_predicate_on_a_group_counts_only_the_matching_rows()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                SeedFour(db);   // Ann 300, Bob 300, Cid 200, Dee 100

                var r = db.CreateQuery<TestEntities.Employee>()
                          .Where(x => x.LastName == Marker)
                          .GroupBy(x => x.LastName)
                          .Select(g => new
                          {
                              Total = g.Count(),
                              Well = g.Count(y => y.Salary >= 200m),
                              Single1 = g.Count(y => y.FirstName == "Dee"),
                              Zero = g.Count(y => y.Salary > 1000m),
                          })
                          .ToList()
                          .Single();

                Assert.AreEqual(4, r.Total);
                Assert.AreEqual(3, r.Well, "Only the matching rows count; a row that fails the test must not be counted as 0.");
                Assert.AreEqual(1, r.Single1);
                Assert.AreEqual(0, r.Zero, "No row matches, so the count is 0, not NULL.");
            });
        }

        [TestMethod]
        public void Math_methods_run_on_the_server_and_round_halves_away_from_zero()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                Seed(db, "Ann", 2.5m, new DateTime(2020, 1, 1));
                Seed(db, "Bob", 7.45m, new DateTime(2020, 1, 1));

                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(x => x.LastName == Marker)
                             .Select(x => new
                             {
                                 x.FirstName,
                                 Abs = Math.Abs(x.Salary - 10m),
                                 Floor = Math.Floor(x.Salary),
                                 Ceil = Math.Ceiling(x.Salary),
                                 Cut = Math.Truncate(x.Salary),
                                 Sign = Math.Sign(x.Salary - 5m),
                                 Away0 = Math.Round(x.Salary, MidpointRounding.AwayFromZero),
                                 Away1 = Math.Round(x.Salary, 1, MidpointRounding.AwayFromZero),
                                 Pow = Math.Pow((double)x.Salary, 2),
                                 Root = Math.Sqrt((double)x.Salary),
                             })
                             .ToList()
                             .OrderBy(r => r.FirstName)
                             .ToList();

                var ann = rows[0];
                Assert.AreEqual(7.5m, ann.Abs);
                Assert.AreEqual(2m, ann.Floor);
                Assert.AreEqual(3m, ann.Ceil);
                Assert.AreEqual(2m, ann.Cut);
                Assert.AreEqual(-1, ann.Sign);
                Assert.AreEqual(3m, ann.Away0, "2.5 rounds to 3 in SQL (C# would give 2 without AwayFromZero).");
                Assert.AreEqual(6.25, ann.Pow, 1e-9);

                var bob = rows[1];
                Assert.AreEqual(2.55m, bob.Abs);
                Assert.AreEqual(7m, bob.Floor);
                Assert.AreEqual(8m, bob.Ceil);
                Assert.AreEqual(7m, bob.Cut);
                Assert.AreEqual(1, bob.Sign);
                Assert.AreEqual(7.5m, bob.Away1, "7.45 to one place rounds away from zero to 7.5 (to even would give 7.4).");
                Assert.AreEqual(Math.Sqrt(7.45), bob.Root, 1e-9);
            });
        }

        [TestMethod]
        public void SelectField_feeds_SQL_functions_the_rows_of_a_group()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                SeedFour(db);   // Ann 300, Bob 300, Cid 200, Dee 100

                var r = db.CreateQuery<TestEntities.Employee>()
                          .Where(x => x.LastName == Marker)
                          .GroupBy(x => x.LastName)
                          .Select(g => new
                          {
                              Sd = SqlServerFunctions.Stdev(g.SelectField(y => y.Salary)),
                              Total = SqlServerFunctions.Sum(g.SelectField(y => y.Salary * y.FirstName.Length)),
                              Names = SqlServerFunctions.String_Agg(g.SelectField(y => y.FirstName), "|", Sql.Over().OrderByDescending(g.SelectField(y => y.Salary)).OrderBy(g.SelectField(y => y.FirstName))),
                          })
                          .ToList()
                          .Single();

                Assert.AreEqual(95.7427, r.Sd.Value, 0.001, "Sample standard deviation of 300, 300, 200, 100.");
                Assert.AreEqual(2700m, r.Total, "Every first name has 3 letters, so the total is 3 x 900.");
                Assert.AreEqual("Ann|Bob|Cid|Dee", r.Names);
            });
        }

        [TestMethod]
        public void Average_LongCount_and_selector_less_aggregates_on_a_group_run()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                SeedFour(db);   // Ann 300, Bob 300, Cid 200, Dee 100

                var r = db.CreateQuery<TestEntities.Employee>()
                          .Where(x => x.LastName == Marker)
                          .GroupBy(x => x.LastName)
                          .Select(g => new
                          {
                              AvgSalary = g.Average(y => y.Salary),
                              AvgFlag = g.Average(y => y.FirstName == "Ann" ? 1 : 0),
                              Big = g.LongCount(),
                              Total = g.Select(y => y.Salary).Sum(),
                              Highest = g.Select(y => y.Salary).Max(),
                          })
                          .ToList()
                          .Single();

                Assert.AreEqual(225m, r.AvgSalary);
                Assert.AreEqual(0.25, r.AvgFlag, 1e-9, "Average of ints is a double in LINQ; SQL AVG of ints would truncate to 0.");
                Assert.AreEqual(4L, r.Big);
                Assert.AreEqual(900m, r.Total);
                Assert.AreEqual(300m, r.Highest);
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
