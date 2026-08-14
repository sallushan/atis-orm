using Atis.Orm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         <see cref="QueryExtensions.SelectFields{T}"/> actually running, against a real SQL
    ///         Server — the counterpart to <see cref="SelectFieldsQueryTests"/>, which asserts generated
    ///         SQL and needs no database.
    ///     </para>
    ///     <para>
    ///         A real server is what makes these worth writing: the CLR type behind each value, the
    ///         <c>DBNull</c> for a null column and the casing of the returned names are all the
    ///         provider's choice, and a fake reader handing back canned rows would assert only what the
    ///         test already assumed.
    ///     </para>
    ///     <para>
    ///         Every test seeds its own rows inside <c>TransactionWithRollback</c> rather than reading
    ///         the shared fixture, so each one knows exactly which rows and which nulls it is looking
    ///         at, and nothing is left behind for the next run.
    ///     </para>
    /// </summary>
    [TestClass]
    public class SelectFieldsExecutionTests
    {
        private const string ServerConnectionString =
            "Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True";

        [TestInitialize]
        public void EnsureDatabase() => new TestDatabaseSetup(ServerConnectionString).Setup();

        /// <summary>Seeds one employee and returns its generated key.</summary>
        private static int Seed(OrmDbContext db, string email, string firstName, DateTime? hireDate)
        {
            var rows = db.InsertEntity<TestEntities.Employee>()
                         .Value(x => x.FirstName, () => firstName)
                         .Value(x => x.LastName, () => "SelectFields")
                         .Value(x => x.Email, () => email)
                         .Value(x => x.Salary, () => 4321m)
                         .Value(x => x.HireDate, () => hireDate)
                         .Output(x => x.EmployeeId)
                         .ExecuteDictionary();
            return (int)rows[0]["EmployeeId"];
        }

        /// <summary>
        ///     The whole reason the feature exists: two fields asked for, two keys returned, and the
        ///     nine other columns of <c>Employee</c> never fetched. Asserting the <em>absence</em> of
        ///     <c>Salary</c> is the assertion that matters — a full-entity select would still satisfy
        ///     everything else here.
        /// </summary>
        [TestMethod]
        public void Returns_only_the_named_fields_keyed_by_member_name()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                Seed(db, "fields.only@example.test", "FieldsOnly", new DateTime(2020, 1, 15));

                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(x => x.Email == "fields.only@example.test")
                             .SelectFields(x => new object[] { x.FirstName, x.HireDate })
                             .ToList();

                Assert.AreEqual(1, rows.Count);
                Assert.AreEqual(2, rows[0].Count, "Only the selected fields become keys.");
                Assert.AreEqual("FieldsOnly", rows[0]["FirstName"]);
                Assert.AreEqual(new DateTime(2020, 1, 15), rows[0]["HireDate"]);
                Assert.IsFalse(rows[0].ContainsKey(nameof(TestEntities.Employee.Salary)),
                    "A column that was not asked for must not be fetched.");
                Assert.IsFalse(rows[0].ContainsKey(nameof(TestEntities.Employee.EmployeeId)));
            });
        }

        [TestMethod]
        public void Returns_every_matching_row()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                Seed(db, "rows.a@example.test", "RowA", null);
                Seed(db, "rows.b@example.test", "RowB", null);

                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(x => x.LastName == "SelectFields")
                             .SelectFields(x => new object[] { x.FirstName })
                             .ToList();

                CollectionAssert.AreEquivalent(
                    new[] { "RowA", "RowB" },
                    rows.Select(x => (string)x["FirstName"]).ToArray());
            });
        }

        /// <summary>
        ///     Keys are the member names, so a caller writing one from memory should not have to match
        ///     the casing — the same rule every other dictionary path in the ORM follows, because they
        ///     all go through <c>DictionaryRow</c>.
        /// </summary>
        [TestMethod]
        public void Looks_up_keys_case_insensitively()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                Seed(db, "case.insensitive@example.test", "CaseTest", null);

                var row = db.CreateQuery<TestEntities.Employee>()
                            .Where(x => x.Email == "case.insensitive@example.test")
                            .SelectFields(x => new object[] { x.FirstName })
                            .ToList()
                            .Single();

                Assert.AreEqual("CaseTest", row["firstname"]);
                Assert.AreEqual("CaseTest", row["FIRSTNAME"]);
                Assert.IsTrue(row.ContainsKey("fIrStNaMe"));
            });
        }

        /// <summary>
        ///     A null column is <c>DBNull</c> on the wire. It must arrive as a plain <c>null</c> with
        ///     the key still present, so "the column was null" stays distinguishable from "that field
        ///     was never selected".
        /// </summary>
        [TestMethod]
        public void A_null_column_arrives_as_null_with_its_key_present()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                Seed(db, "null.column@example.test", "NullTest", hireDate: null);

                var row = db.CreateQuery<TestEntities.Employee>()
                            .Where(x => x.Email == "null.column@example.test")
                            .SelectFields(x => new object[] { x.FirstName, x.HireDate })
                            .ToList()
                            .Single();

                Assert.IsTrue(row.ContainsKey("HireDate"));
                Assert.IsNull(row["HireDate"]);
            });
        }

        /// <summary>
        ///     The async terminal is the ORM's existing <c>ToListAsync</c>. Nothing async was written
        ///     for this feature — that it works at all is the point of returning an
        ///     <see cref="IQueryable{T}"/> rather than a materialized list.
        /// </summary>
        [TestMethod]
        public async Task ToListAsync_returns_the_same_rows()
        {
            var db = new OrmDbContext();
            await db.TransactionWithRollbackAsync(async () =>
            {
                Seed(db, "async.fields@example.test", "AsyncFields", new DateTime(2021, 6, 1));

                var rows = await db.CreateQuery<TestEntities.Employee>()
                                   .Where(x => x.Email == "async.fields@example.test")
                                   .SelectFields(x => new object[] { x.FirstName, x.HireDate })
                                   .ToListAsync();

                Assert.AreEqual(1, rows.Count);
                Assert.AreEqual(2, rows[0].Count);
                Assert.AreEqual("AsyncFields", rows[0]["FirstName"]);
                Assert.AreEqual(new DateTime(2021, 6, 1), rows[0]["HireDate"]);
            });
        }

        /// <summary>
        ///     <para>
        ///         Two field lists over the same entity are two different queries and must not share a
        ///         compiled one. The field list lives in the expression tree, so the cache key already
        ///         separates them — but the two calls are identical in every other respect, which is
        ///         exactly the shape a key that ignored the projection would collapse.
        ///     </para>
        /// </summary>
        [TestMethod]
        public void Two_different_field_lists_do_not_share_a_compiled_query()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                Seed(db, "two.lists@example.test", "TwoLists", new DateTime(2019, 3, 9));

                var byName = db.CreateQuery<TestEntities.Employee>()
                               .Where(x => x.Email == "two.lists@example.test")
                               .SelectFields(x => new object[] { x.FirstName })
                               .ToList()
                               .Single();

                var bySalary = db.CreateQuery<TestEntities.Employee>()
                                 .Where(x => x.Email == "two.lists@example.test")
                                 .SelectFields(x => new object[] { x.Salary, x.LastName })
                                 .ToList()
                                 .Single();

                CollectionAssert.AreEqual(new[] { "FirstName" }, byName.Keys.ToArray());
                CollectionAssert.AreEquivalent(new[] { "Salary", "LastName" }, bySalary.Keys.ToArray());
                Assert.AreEqual(4321m, bySalary["Salary"]);
            });
        }
    }
}
