using Atis.Orm.DataAccess;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         Covers <see cref="DbCommunicationBase"/>'s transaction and savepoint orchestration against a
    ///         fake transaction rather than a database: call ordering, which paths commit, which roll back,
    ///         what gets disposed, and how state is left behind afterwards.
    ///     </para>
    ///     <para>
    ///         A real server is the wrong tool here. The interesting cases are the ones you cannot ask a
    ///         database for on demand — a commit that fails, or a rollback-to-savepoint that fails because
    ///         the transaction is doomed (SQL Server error 3931). Those are exactly the paths that were
    ///         wrong before, so they are the ones worth pinning down.
    ///     </para>
    /// </summary>
    [TestClass]
    public class TransactionOrchestrationTests
    {
        // ---------- transaction ----------

        [TestMethod]
        public void Transaction_Commits_AndClearsState()
        {
            var db = new TestCommunication();

            db.Transaction(() => db.Log.Add("work"));

            CollectionAssert.AreEqual(new[] { "begin", "work", "commit" }, db.Log);
            Assert.IsTrue(db.Transaction1.Committed);
            Assert.IsFalse(db.Transaction1.RolledBack);
            Assert.IsTrue(db.Transaction1.Disposed, "The transaction must be disposed when the scope ends.");
            Assert.IsNull(db.CurrentTransaction, "The ambient transaction must be cleared when the scope ends.");
        }

        [TestMethod]
        public void Transaction_RollsBack_AndRethrowsTheOriginalException()
        {
            var db = new TestCommunication();

            var thrown = Assert.ThrowsException<InvalidOperationException>(
                () => db.Transaction(() => throw new InvalidOperationException("boom")));

            Assert.AreEqual("boom", thrown.Message, "The caller's exception must survive, not be wrapped.");
            CollectionAssert.AreEqual(new[] { "begin", "rollback" }, db.Log);
            Assert.IsFalse(db.Transaction1.Committed);
            Assert.IsTrue(db.Transaction1.Disposed);
            Assert.IsNull(db.CurrentTransaction);
        }

        /// <summary>
        ///     A nested call joins the transaction in progress: one begin, one commit, and the inner call
        ///     neither commits nor rolls back on its own.
        /// </summary>
        [TestMethod]
        public void NestedTransaction_IsAPassThrough()
        {
            var db = new TestCommunication();

            db.Transaction(() =>
            {
                db.Log.Add("outer");
                db.Transaction(() => db.Log.Add("inner"));
            });

            CollectionAssert.AreEqual(new[] { "begin", "outer", "inner", "commit" }, db.Log);
            Assert.AreEqual(1, db.BeginCount, "A nested call must not start a second transaction.");
        }

        /// <summary>
        ///     A failing commit must surface as itself. Attempting a rollback afterwards would fail too —
        ///     the transaction is already resolved — and bury the real cause in an AggregateException.
        /// </summary>
        [TestMethod]
        public void CommitFailure_PropagatesRaw_WithoutAttemptingRollback()
        {
            var db = new TestCommunication();
            db.FailCommit = true;

            var thrown = Assert.ThrowsException<InvalidOperationException>(
                () => db.Transaction(() => db.Log.Add("work")));

            Assert.AreEqual("commit failed", thrown.Message);
            CollectionAssert.AreEqual(new[] { "begin", "work", "commit" }, db.Log);
            Assert.IsFalse(db.Transaction1.RolledBack, "Rollback must not be attempted after a failed commit.");
            Assert.IsTrue(db.Transaction1.Disposed);
            Assert.IsNull(db.CurrentTransaction, "State must be cleared even when the commit failed.");
        }

        /// <summary>
        ///     The failure above must not poison the instance — this is what the old ref-counted version got
        ///     wrong, leaving a counter at -1 that broke every later transaction.
        /// </summary>
        [TestMethod]
        public void Transaction_WorksAgain_AfterAFailedCommit()
        {
            var db = new TestCommunication();
            db.FailCommit = true;
            Assert.ThrowsException<InvalidOperationException>(() => db.Transaction(() => { }));

            db.FailCommit = false;
            db.Log.Clear();
            db.Transaction(() => db.Log.Add("work"));

            CollectionAssert.AreEqual(new[] { "begin", "work", "commit" }, db.Log);
            Assert.IsTrue(db.Transaction1.Committed);
        }

        [TestMethod]
        public void Transaction_RejectsNullWork()
        {
            var db = new TestCommunication();

            Assert.ThrowsException<ArgumentNullException>(() => db.Transaction(null));
            Assert.AreEqual(0, db.BeginCount, "Nothing should have been started.");
        }

        // ---------- savepoint ----------

        [TestMethod]
        public void Savepoint_CreatesAndReleases_OnSuccess()
        {
            var db = new TestCommunication();

            db.Transaction(() => db.TransactionWithSavepoint(() => db.Log.Add("work")));

            CollectionAssert.AreEqual(
                new[] { "begin", "save:at_tran_savepoint_1", "work", "release:at_tran_savepoint_1", "commit" },
                db.Log);
        }

        /// <summary>
        ///     On failure the savepoint is rolled back to and <em>not</em> released — whether a rollback
        ///     discards the savepoint is provider specific, so that is the provider's business.
        /// </summary>
        [TestMethod]
        public void Savepoint_RollsBack_AndDoesNotRelease_OnFailure()
        {
            var db = new TestCommunication();

            db.Transaction(() =>
            {
                try
                {
                    db.TransactionWithSavepoint(() => throw new InvalidOperationException("item failed"));
                }
                catch (InvalidOperationException)
                {
                    db.Log.Add("caught");
                }
            });

            CollectionAssert.AreEqual(
                new[] { "begin", "save:at_tran_savepoint_1", "rollbackto:at_tran_savepoint_1", "caught", "commit" },
                db.Log);
            Assert.IsTrue(db.Transaction1.Committed,
                "Catching around a savepoint is safe — the surrounding transaction still commits.");
        }

        [TestMethod]
        public void Savepoint_RethrowsTheOriginalException()
        {
            var db = new TestCommunication();

            var thrown = Assert.ThrowsException<InvalidOperationException>(
                () => db.Transaction(() => db.TransactionWithSavepoint(() => throw new InvalidOperationException("boom"))));

            Assert.AreEqual("boom", thrown.Message);
            Assert.IsTrue(db.Transaction1.RolledBack, "An uncaught savepoint failure still fails the transaction.");
        }

        [TestMethod]
        public void Savepoint_NamesAreSequential_AndResetPerTransaction()
        {
            var db = new TestCommunication();

            db.Transaction(() =>
            {
                db.TransactionWithSavepoint(() => { });
                db.TransactionWithSavepoint(() => { });
            });
            db.Transaction(() => db.TransactionWithSavepoint(() => { }));

            CollectionAssert.AreEqual(
                new[]
                {
                    "begin",
                    "save:at_tran_savepoint_1", "release:at_tran_savepoint_1",
                    "save:at_tran_savepoint_2", "release:at_tran_savepoint_2",
                    "commit",
                    "begin",
                    "save:at_tran_savepoint_1", "release:at_tran_savepoint_1",
                    "commit",
                },
                db.Log);
        }

        [TestMethod]
        public void NestedSavepoints_GetDistinctNames_AndUnwindInOrder()
        {
            var db = new TestCommunication();

            db.Transaction(() => db.TransactionWithSavepoint(
                () => db.TransactionWithSavepoint(() => db.Log.Add("work"))));

            CollectionAssert.AreEqual(
                new[]
                {
                    "begin",
                    "save:at_tran_savepoint_1",
                    "save:at_tran_savepoint_2",
                    "work",
                    "release:at_tran_savepoint_2",
                    "release:at_tran_savepoint_1",
                    "commit",
                },
                db.Log);
        }

        [TestMethod]
        public void Savepoint_OutsideATransaction_Throws()
        {
            var db = new TestCommunication();

            var thrown = Assert.ThrowsException<InvalidOperationException>(
                () => db.TransactionWithSavepoint(() => { }));

            StringAssert.Contains(thrown.Message, "outer transaction");
            Assert.AreEqual(0, db.Log.Count);
        }

        // ---------- poisoning ----------

        /// <summary>
        ///     SQL Server error 3931: the transaction is doomed and cannot roll back to a savepoint. The
        ///     caller of a savepoint is <em>expected</em> to catch and carry on, so without poisoning they
        ///     would keep working inside a transaction that can never commit.
        /// </summary>
        [TestMethod]
        public void SavepointRollbackFailure_ReportsBothCauses_AndPoisonsTheTransaction()
        {
            var db = new TestCommunication();
            db.FailRollbackToSavepoint = true;

            var thrown = Assert.ThrowsException<InvalidOperationException>(() =>
                db.Transaction(() =>
                {
                    try
                    {
                        db.TransactionWithSavepoint(() => throw new InvalidOperationException("item failed"));
                    }
                    catch (AggregateException ex)
                    {
                        // Both the original failure and the reason it could not be undone.
                        Assert.AreEqual(2, ex.InnerExceptions.Count);
                        Assert.AreEqual("item failed", ex.InnerExceptions[0].Message);
                        Assert.AreEqual("3931", ex.InnerExceptions[1].Message);
                        db.Log.Add("caught");
                    }
                }));

            StringAssert.Contains(thrown.Message, "rolled back");
            Assert.IsTrue(db.Transaction1.RolledBack, "A poisoned transaction must be rolled back, not committed.");
            Assert.IsFalse(db.Transaction1.Committed);
            CollectionAssert.AreEqual(
                new[] { "begin", "save:at_tran_savepoint_1", "rollbackto:at_tran_savepoint_1", "caught", "rollback" },
                db.Log);
        }

        [TestMethod]
        public void PoisonedTransaction_RefusesFurtherSavepoints()
        {
            var db = new TestCommunication();
            db.FailRollbackToSavepoint = true;

            Assert.ThrowsException<InvalidOperationException>(() =>
                db.Transaction(() =>
                {
                    try { db.TransactionWithSavepoint(() => throw new InvalidOperationException("first")); }
                    catch (AggregateException) { }

                    var refused = Assert.ThrowsException<InvalidOperationException>(
                        () => db.TransactionWithSavepoint(() => { }));
                    StringAssert.Contains(refused.Message, "no longer be used");
                }));
        }

        /// <summary>
        ///     Poisoning is per transaction, not per instance — the next transaction starts clean.
        /// </summary>
        [TestMethod]
        public void Poisoning_DoesNotLeakIntoTheNextTransaction()
        {
            var db = new TestCommunication();
            db.FailRollbackToSavepoint = true;
            Assert.ThrowsException<InvalidOperationException>(() =>
                db.Transaction(() =>
                {
                    try { db.TransactionWithSavepoint(() => throw new InvalidOperationException("x")); }
                    catch (AggregateException) { }
                }));

            db.FailRollbackToSavepoint = false;
            db.Log.Clear();
            db.Transaction(() => db.TransactionWithSavepoint(() => db.Log.Add("work")));

            CollectionAssert.AreEqual(
                new[] { "begin", "save:at_tran_savepoint_1", "work", "release:at_tran_savepoint_1", "commit" },
                db.Log);
            Assert.IsTrue(db.Transaction1.Committed);
        }

        // ---------- harness ----------

        /// <summary>
        ///     Overrides the seams that reach the database so the orchestration above runs without one.
        ///     <c>GetTransactionAndConnection</c> returns a null connection, which is the shape the
        ///     external-connection case already produces, so nothing here is a special path.
        /// </summary>
        private sealed class TestCommunication : DbCommunicationBase
        {
            public TestCommunication() : base("fake-connection-string") { }

            public readonly List<string> Log = new List<string>();
            public int BeginCount { get; private set; }
            public FakeTransaction Transaction1 { get; private set; }
            public bool FailCommit { get; set; }
            public bool FailRollbackToSavepoint { get; set; }

            /// <summary>The ambient transaction, so tests can assert it was cleared.</summary>
            public DbTransaction CurrentTransaction => this.GetCurrentTransaction();

            protected override (DbConnection, DbTransaction, bool) GetTransactionAndConnection()
            {
                this.BeginCount++;
                this.Log.Add("begin");
                this.Transaction1 = new FakeTransaction { FailOnCommit = this.FailCommit };
                return (null, this.Transaction1, false);
            }

            protected override void CommitTransaction(DbTransaction tx)
            {
                this.Log.Add("commit");
                base.CommitTransaction(tx);
            }

            protected override void RollbackTransaction(DbTransaction tx)
            {
                this.Log.Add("rollback");
                base.RollbackTransaction(tx);
            }

            protected override void CreateSavepoint(string savepoint) => this.Log.Add("save:" + savepoint);

            protected override void RollbackToSavepoint(string savepoint)
            {
                this.Log.Add("rollbackto:" + savepoint);
                if (this.FailRollbackToSavepoint)
                    throw new InvalidOperationException("3931");
            }

            protected override void ReleaseSavepoint(string savepoint) => this.Log.Add("release:" + savepoint);

            protected override DbConnection CreateConnection()
                => throw new NotSupportedException("These tests never reach the database.");

            protected override DbCommand CreateCommand(string commandText, IEnumerable<DbParameter> dbParameters, CommandType commandType)
                => throw new NotSupportedException("These tests never reach the database.");
        }

        private sealed class FakeTransaction : DbTransaction
        {
            public bool Committed { get; private set; }
            public bool RolledBack { get; private set; }
            public bool Disposed { get; private set; }
            public bool FailOnCommit { get; set; }

            protected override DbConnection DbConnection => null;
            public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

            public override void Commit()
            {
                if (this.FailOnCommit)
                    throw new InvalidOperationException("commit failed");
                this.Committed = true;
            }

            public override void Rollback() => this.RolledBack = true;

            protected override void Dispose(bool disposing)
            {
                this.Disposed = true;
                base.Dispose(disposing);
            }
        }
    }
}
