using Atis.Orm;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         DML that actually runs, against a real SQL Server — the counterpart to
    ///         <see cref="DataManipulationQueryTests"/>, which asserts generated SQL and needs no
    ///         database. Kept apart so the translation tests stay runnable without a server, and so it
    ///         is obvious which tests do not.
    ///     </para>
    ///     <para>
    ///         Every test here writes inside <c>TransactionWithRollback</c>. These tables are the shared
    ///         seed that other tests read, and committing to them leaves the fixture changed for the rest
    ///         of the run and for every run afterwards.
    ///     </para>
    /// </summary>
    [TestClass]
    public class DataManipulationExecutionTests
    {
        private const string ServerConnectionString =
            "Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True";

        [TestInitialize]
        public void EnsureDatabase() => new TestDatabaseSetup(ServerConnectionString).Setup();

        [TestMethod]
        public void Insert_fluent_api_execution_with_output()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var rows = db.InsertEntity<TestEntities.Employee>()
                             .Value(x => x.FirstName, () => "Insert_Fluent")
                             .Value(x => x.LastName, () => "Test")
                             .Value(x => x.Email, () => "insert.fluent@example.test")
                             .Value(x => x.Salary, () => 1234m)
                             .Output(x => x.EmployeeId)
                             .Output(x => x.FirstName)
                             .ExecuteDictionary();

                Assert.AreEqual(1, rows.Count);
                Assert.IsTrue((int)rows[0]["EmployeeId"] > 0);
                Assert.AreEqual("Insert_Fluent", rows[0]["FirstName"]);

                var inserted = db.CreateQuery<TestEntities.Employee>()
                                 .Where(x => x.Email == "insert.fluent@example.test")
                                 .Select(x => x.FirstName)
                                 .ToList();
                CollectionAssert.AreEqual(new[] { "Insert_Fluent" }, inserted);
            });
        }

        [TestMethod]
        public async Task Insert_fluent_api_execute_async_reports_the_affected_row_count()
        {
            var db = new OrmDbContext();
            await db.TransactionWithRollbackAsync(async () =>
            {
                var affected = await db.InsertEntity<TestEntities.Employee>()
                                       .Value(x => x.FirstName, () => "Insert_Async")
                                       .Value(x => x.LastName, () => "Test")
                                       .Value(x => x.Email, () => "insert.async@example.test")
                                       .Value(x => x.Salary, () => 2345m)
                                       .ExecuteAsync();

                Assert.AreEqual(1, affected);
                var inserted = await db.CreateQuery<TestEntities.Employee>()
                                       .Where(x => x.Email == "insert.async@example.test")
                                       .Select(x => x.FirstName)
                                       .ToListAsync();
                CollectionAssert.AreEqual(new[] { "Insert_Async" }, inserted);
            });
        }

        [TestMethod]
        public void Insert_fluent_api_rebinds_captured_variables_on_cache_hit()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var firstName = "Insert_Rebind_First";
                var email = "insert.rebind.first@example.test";

                Insert();
                firstName = "Insert_Rebind_Second";
                email = "insert.rebind.second@example.test";
                Insert();

                var insertedNames = db.CreateQuery<TestEntities.Employee>()
                                      .Where(x => x.Email == "insert.rebind.first@example.test"
                                               || x.Email == "insert.rebind.second@example.test")
                                      .OrderBy(x => x.Email)
                                      .Select(x => x.FirstName)
                                      .ToList();
                CollectionAssert.AreEqual(
                    new[] { "Insert_Rebind_First", "Insert_Rebind_Second" },
                    insertedNames);

                void Insert()
                {
                    db.InsertEntity<TestEntities.Employee>()
                      .Value(x => x.FirstName, () => firstName)
                      .Value(x => x.LastName, () => "Test")
                      .Value(x => x.Email, () => email)
                      .Value(x => x.Salary, () => 3456m)
                      .Execute();
                }
            });
        }

        /// <summary>
        ///     The fluent update end to end, including the OUTPUT clause that feeds the returned rows.
        /// </summary>
        [TestMethod]
        public void Update_fluent_api_execution()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                // Values deliberately unlike the seeded ones, so a no-op update cannot pass.
                var results = db.UpdateEntity<TestEntities.Employee>()
                            .Set(x => x.LastName, () => "Smith_Fluent")
                            .Set(x => x.FirstName, () => "John_Fluent")
                            .Key(x => x.EmployeeId, () => 1)
                            .Output(x => x.EmployeeId)
                            .ExecuteDictionary();

                Assert.AreEqual(1, results.Count, "One employee matches the key, so one row comes back.");
                Assert.AreEqual(1, results[0]["EmployeeId"], "OUTPUT must return the updated row's key.");

                var names = db.CreateQuery<TestEntities.Employee>()
                              .Where(x => x.EmployeeId == 1)
                              .Select(x => x.FirstName)
                              .ToList();
                CollectionAssert.AreEqual(new[] { "John_Fluent" }, names,
                    "The SET clause must actually have been applied.");
            });
        }

        /// <summary>
        ///     <para>
        ///         The reason <c>Set</c> takes <c>Expression&lt;Func&lt;FT&gt;&gt;</c> rather than a
        ///         plain value: the captured variable stays visible in the tree, so a second run of the
        ///         same update hits the compiled-query cache and rebinds the parameter to the new value
        ///         instead of recompiling.
        ///     </para>
        ///     <para>
        ///         Only reachable since the cache started hitting for these updates at all — before that
        ///         the key was an identity hash and every run was a miss, so this path never ran.
        ///     </para>
        /// </summary>
        [TestMethod]
        public void Update_fluent_api_rebinds_captured_variable_on_cache_hit()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var lastName = "Rebind_First";
                db.UpdateEntity<TestEntities.Employee>()
                  .Set(x => x.LastName, () => lastName)
                  .Key(x => x.EmployeeId, () => 1)
                  .Execute();
                CollectionAssert.AreEqual(new[] { "Rebind_First" }, ReadLastName(),
                    "First run compiles and binds the captured value.");

                // Same shape, new value. The cache must hit and rebind, not replay the old parameter.
                lastName = "Rebind_Second";
                db.UpdateEntity<TestEntities.Employee>()
                  .Set(x => x.LastName, () => lastName)
                  .Key(x => x.EmployeeId, () => 1)
                  .Execute();
                CollectionAssert.AreEqual(new[] { "Rebind_Second" }, ReadLastName(),
                    "A cache hit must rebind the captured variable to its current value.");
            });

            List<string> ReadLastName()
                => db.CreateQuery<TestEntities.Employee>()
                     .Where(x => x.EmployeeId == 1)
                     .Select(x => x.LastName)
                     .ToList();
        }

        /// <summary>
        ///     The async output terminal end to end. It bypasses <c>CreateQuery</c> and drains the
        ///     provider's async sequence directly, so the OUTPUT rows have to survive a different
        ///     materialization path than <see cref="Update_fluent_api_execution"/> exercises.
        /// </summary>
        [TestMethod]
        public async Task Update_fluent_api_execution_async()
        {
            var db = new OrmDbContext();
            await db.TransactionWithRollbackAsync(async () =>
            {
                var results = await db.UpdateEntity<TestEntities.Employee>()
                            .Set(x => x.LastName, () => "Smith_Async")
                            .Set(x => x.FirstName, () => "John_Async")
                            .Key(x => x.EmployeeId, () => 1)
                            .Output(x => x.EmployeeId)
                            .Output(x => x.FirstName)
                            .ExecuteDictionaryAsync();

                Assert.AreEqual(1, results.Count, "One employee matches the key, so one row comes back.");
                Assert.AreEqual(1, results[0]["EmployeeId"], "OUTPUT must return the updated row's key.");
                Assert.AreEqual("John_Async", results[0]["FirstName"],
                    "OUTPUT reports the row as it now stands, not as it was.");

                var names = await db.CreateQuery<TestEntities.Employee>()
                                    .Where(x => x.EmployeeId == 1)
                                    .Select(x => x.FirstName)
                                    .ToListAsync();
                CollectionAssert.AreEqual(new[] { "John_Async" }, names,
                    "The SET clause must actually have been applied.");
            });
        }

        /// <summary>
        ///     The async non-query terminal end to end. This is the branch <c>QueryExecutor</c> routes to
        ///     <c>ExecuteNonQueryAsync</c>, which returns the count rather than materializing rows.
        /// </summary>
        [TestMethod]
        public async Task Update_fluent_api_execute_async_reports_the_affected_row_count()
        {
            var db = new OrmDbContext();
            await db.TransactionWithRollbackAsync(async () =>
            {
                var affected = await db.UpdateEntity<TestEntities.Employee>()
                                       .Set(x => x.LastName, () => "Smith_AsyncCount")
                                       .Key(x => x.EmployeeId, () => 1)
                                       .ExecuteAsync();

                Assert.AreEqual(1, affected, "Exactly the one employee named by the key is updated.");

                var lastNames = await db.CreateQuery<TestEntities.Employee>()
                                        .Where(x => x.EmployeeId == 1)
                                        .Select(x => x.LastName)
                                        .ToListAsync();
                CollectionAssert.AreEqual(new[] { "Smith_AsyncCount" }, lastNames,
                    "A reported count of 1 must mean the row really was written.");
            });
        }

        /// <summary>The query-based UPDATE, whose values are expressions over the row itself.</summary>
        [TestMethod]
        public void Update_query_execution()
        {
            var db = new OrmDbContext();
            var employees = db.CreateQuery<TestEntities.Employee>();
            db.TransactionWithRollback(() =>
            {
                var affected = employees.Update(
                    x => new TestEntities.Employee { FirstName = x.FirstName + "_Updated" },
                    x => x.EmployeeId < 5);
                Assert.AreEqual(4, affected, "Employees 1-4 must be updated.");
            });
        }

        /// <summary>The query-based DELETE.</summary>
        [TestMethod]
        public void Delete_query_execution()
        {
            var db = new OrmDbContext();
            var auditLogs = db.CreateQuery<TestEntities.AuditLog>();
            db.TransactionWithRollback(() =>
            {
                var affected = auditLogs.Delete(x => x.AuditId < 5);
                Assert.AreEqual(4, affected, "Audit rows 1-4 must be deleted.");
            });
        }
    }
}
