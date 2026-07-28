using Atis.Orm;
using Atis.Orm.DataAccess;
using Atis.Orm.SqlServer;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         End to end against a real SQL Server, through <see cref="DataContext"/>. Where
    ///         <see cref="TransactionOrchestrationTests"/> pins down the control flow, these prove the SQL
    ///         actually does what the control flow assumes: that a rollback really discards the rows, and
    ///         that <c>SAVE TRANSACTION</c> / <c>ROLLBACK TRANSACTION</c> really undo only part of the work.
    ///     </para>
    ///     <para>
    ///         Every assertion reads through a separate connection outside the transaction, so a test can
    ///         never be fooled by uncommitted state it can see only because it is the writer.
    ///     </para>
    /// </summary>
    [TestClass]
    public class TransactionExecutionTests
    {
        private const string ConnectionString =
            "server=localhost;database=TestDb;integrated security=true;TrustServerCertificate=True";

        [TestInitialize]
        public void ResetTable()
        {
            Execute(@"
if object_id('dbo.TranTest') is null
    create table dbo.TranTest (Id int not null primary key, Tag varchar(50) not null);
delete from dbo.TranTest;");
        }

        // ---------- transaction ----------

        [TestMethod]
        public void Transaction_CommitsItsWork()
        {
            using var db = new TranTestContext();

            db.Transaction(() => db.ExecuteNonQuery("insert into dbo.TranTest (Id, Tag) values (1, 'a')"));

            CollectionAssert.AreEqual(new[] { 1 }, ReadIds());
        }

        [TestMethod]
        public void Transaction_DiscardsEverything_WhenItThrows()
        {
            using var db = new TranTestContext();

            Assert.ThrowsException<InvalidOperationException>(() => db.Transaction(() =>
            {
                db.ExecuteNonQuery("insert into dbo.TranTest (Id, Tag) values (1, 'a')");
                db.ExecuteNonQuery("insert into dbo.TranTest (Id, Tag) values (2, 'b')");
                throw new InvalidOperationException("boom");
            }));

            Assert.AreEqual(0, ReadIds().Length, "Both inserts must be gone.");
        }

        /// <summary>
        ///     A nested call joins the outer transaction, so its work is subject to the outer rollback.
        /// </summary>
        [TestMethod]
        public void NestedTransaction_IsRolledBackWithTheOuterOne()
        {
            using var db = new TranTestContext();

            Assert.ThrowsException<InvalidOperationException>(() => db.Transaction(() =>
            {
                db.Transaction(() => db.ExecuteNonQuery("insert into dbo.TranTest (Id, Tag) values (1, 'inner')"));
                throw new InvalidOperationException("boom");
            }));

            Assert.AreEqual(0, ReadIds().Length,
                "The inner call must not have committed independently.");
        }

        /// <summary>
        ///     Nothing is visible outside the transaction until it commits — proves the commands really did
        ///     enlist rather than auto-committing one by one, which is what a missing
        ///     <c>DbCommand.Transaction</c> would look like.
        /// </summary>
        [TestMethod]
        public void WorkIsNotVisibleOutside_UntilCommit()
        {
            using var db = new TranTestContext();

            db.Transaction(() =>
            {
                db.ExecuteNonQuery("insert into dbo.TranTest (Id, Tag) values (1, 'a')");

                // Probing with a plain read would simply block for the command timeout, so ask whether the
                // row is locked instead. It can only be locked if the insert enlisted in a transaction that
                // is still open — which is exactly what a missing DbCommand.Transaction would fail to do.
                Assert.IsTrue(RowsAreLocked(), "Uncommitted work must still be held by the transaction.");
            });

            Assert.IsFalse(RowsAreLocked(), "The locks must be released once the transaction commits.");
            CollectionAssert.AreEqual(new[] { 1 }, ReadIds());
        }

        [TestMethod]
        public void Query_InsideATransaction_SeesTheTransactionsOwnWork()
        {
            using var db = new TranTestContext();

            db.Transaction(() =>
            {
                db.ExecuteNonQuery("insert into dbo.TranTest (Id, Tag) values (1, 'a')");

                var tags = db.ExecuteQuery("select Tag from dbo.TranTest order by Id", r => r.GetString(0)).ToList();

                CollectionAssert.AreEqual(new[] { "a" }, tags,
                    "A read inside the transaction must run on the same connection and see its uncommitted work.");
            });
        }

        [TestMethod]
        public void Transaction_RunsAgain_OnTheSameContext()
        {
            using var db = new TranTestContext();

            db.Transaction(() => db.ExecuteNonQuery("insert into dbo.TranTest (Id, Tag) values (1, 'a')"));
            db.Transaction(() => db.ExecuteNonQuery("insert into dbo.TranTest (Id, Tag) values (2, 'b')"));

            CollectionAssert.AreEqual(new[] { 1, 2 }, ReadIds());
        }

        /// <summary>
        ///     After a transaction, ordinary work must still run — this is what broke when the transaction
        ///     connection was left behind, disposed, in the ambient field.
        /// </summary>
        [TestMethod]
        public void NonTransactionalWork_StillRuns_AfterATransaction()
        {
            using var db = new TranTestContext();
            db.Transaction(() => db.ExecuteNonQuery("insert into dbo.TranTest (Id, Tag) values (1, 'a')"));

            db.ExecuteNonQuery("insert into dbo.TranTest (Id, Tag) values (2, 'b')");
            var tags = db.ExecuteQuery("select Tag from dbo.TranTest order by Id", r => r.GetString(0)).ToList();

            CollectionAssert.AreEqual(new[] { "a", "b" }, tags);
        }

        // ---------- savepoint ----------

        /// <summary>
        ///     The point of the whole feature: one item fails, its work is undone, and the loop carries on.
        /// </summary>
        [TestMethod]
        public void Savepoint_UndoesOnlyItsOwnWork_AndTheRestCommits()
        {
            using var db = new TranTestContext();

            db.Transaction(() =>
            {
                db.ExecuteNonQuery("insert into dbo.TranTest (Id, Tag) values (1, 'before')");

                try
                {
                    db.TransactionWithSavepoint(() =>
                    {
                        db.ExecuteNonQuery("insert into dbo.TranTest (Id, Tag) values (2, 'inside')");
                        throw new InvalidOperationException("this item failed");
                    });
                }
                catch (InvalidOperationException)
                {
                    // Safe to swallow here: the savepoint undid the partial work.
                }

                db.ExecuteNonQuery("insert into dbo.TranTest (Id, Tag) values (3, 'after')");
            });

            CollectionAssert.AreEqual(new[] { 1, 3 }, ReadIds(),
                "Only the work inside the failed savepoint should be missing.");
        }

        [TestMethod]
        public void Savepoint_KeepsItsWork_WhenItSucceeds()
        {
            using var db = new TranTestContext();

            db.Transaction(() =>
            {
                db.ExecuteNonQuery("insert into dbo.TranTest (Id, Tag) values (1, 'before')");
                db.TransactionWithSavepoint(
                    () => db.ExecuteNonQuery("insert into dbo.TranTest (Id, Tag) values (2, 'inside')"));
            });

            CollectionAssert.AreEqual(new[] { 1, 2 }, ReadIds());
        }

        /// <summary>
        ///     Rolling back to an outer savepoint must also undo an inner one that already succeeded.
        /// </summary>
        [TestMethod]
        public void NestedSavepoints_OuterRollbackUndoesTheInnerWork()
        {
            using var db = new TranTestContext();

            db.Transaction(() =>
            {
                db.ExecuteNonQuery("insert into dbo.TranTest (Id, Tag) values (1, 'before')");

                try
                {
                    db.TransactionWithSavepoint(() =>
                    {
                        db.TransactionWithSavepoint(
                            () => db.ExecuteNonQuery("insert into dbo.TranTest (Id, Tag) values (2, 'inner')"));
                        db.ExecuteNonQuery("insert into dbo.TranTest (Id, Tag) values (3, 'outer')");
                        throw new InvalidOperationException("outer savepoint failed");
                    });
                }
                catch (InvalidOperationException)
                {
                }
            });

            CollectionAssert.AreEqual(new[] { 1 }, ReadIds(),
                "Both the inner and outer savepoint work should be gone.");
        }

        /// <summary>
        ///     A savepoint failure that nobody catches still fails the whole transaction.
        /// </summary>
        [TestMethod]
        public void Savepoint_FailureUncaught_RollsBackEverything()
        {
            using var db = new TranTestContext();

            Assert.ThrowsException<InvalidOperationException>(() => db.Transaction(() =>
            {
                db.ExecuteNonQuery("insert into dbo.TranTest (Id, Tag) values (1, 'before')");
                db.TransactionWithSavepoint(() => throw new InvalidOperationException("boom"));
            }));

            Assert.AreEqual(0, ReadIds().Length);
        }

        [TestMethod]
        public void Savepoint_WithoutATransaction_Throws()
        {
            using var db = new TranTestContext();

            var thrown = Assert.ThrowsException<InvalidOperationException>(
                () => db.TransactionWithSavepoint(() => { }));

            StringAssert.Contains(thrown.Message, "outer transaction");
        }

        // ---------- helpers ----------

        private sealed class TranTestContext : Atis.Orm.DataContext
        {
            protected override void OnConfiguring(DataContextConfiguration config)
                => config.UseSqlServer(ConnectionString);
        }

        /// <summary>Reads through its own connection, so it never sees uncommitted work.</summary>
        private static int[] ReadIds()
        {
            using var connection = new SqlConnection(ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "select Id from dbo.TranTest order by Id";
            using var reader = command.ExecuteReader();
            var ids = new List<int>();
            while (reader.Read())
                ids.Add(reader.GetInt32(0));
            return ids.ToArray();
        }

        /// <summary>
        ///     Whether the table's rows are held by an open transaction, decided with a short lock timeout
        ///     rather than by waiting out the command timeout. SQL Server error 1222 is
        ///     "Lock request time out period exceeded".
        /// </summary>
        private static bool RowsAreLocked()
        {
            using var connection = new SqlConnection(ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "set lock_timeout 250; select count(*) from dbo.TranTest;";
            try
            {
                command.ExecuteScalar();
                return false;
            }
            catch (SqlException ex) when (ex.Number == 1222)
            {
                return true;
            }
        }

        private static void Execute(string sql)
        {
            using var connection = new SqlConnection(ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
    }
}
