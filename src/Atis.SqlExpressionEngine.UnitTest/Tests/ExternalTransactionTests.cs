using Atis.Orm.DataAccess;
using Atis.Orm.SqlServer;
using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         Covers <c>DbCommunicationBase.UseTransaction</c>: running against a transaction the caller
    ///         began and still owns.
    ///     </para>
    ///     <para>
    ///         Without it such a connection cannot be used at all. The driver refuses a command that is not
    ///         enlisted in the pending transaction ("requires the command to have a transaction when the
    ///         connection assigned to the command is in a pending local transaction"), and refuses a second
    ///         <c>BeginTransaction</c> on top of it ("does not support parallel transactions"). Joining it
    ///         cannot be automatic: ADO.NET has no way to ask a connection what transaction it is in, which
    ///         is why the caller hands it over.
    ///     </para>
    ///     <para>
    ///         A real server throughout -- every one of these behaviours is the provider's, and a fake would
    ///         only replay the test's own assumptions.
    ///     </para>
    /// </summary>
    [TestClass]
    public class ExternalTransactionTests
    {
        private const string ConnectionString =
            "server=localhost;database=TestDb;integrated security=true;TrustServerCertificate=True";

        [TestInitialize]
        public void ResetTable()
        {
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
if object_id('dbo.ExternalTxTest') is null
    create table dbo.ExternalTxTest (Id int not null primary key);
delete from dbo.ExternalTxTest;";
            cmd.ExecuteNonQuery();
        }

        private static int RowCount()
        {
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "select count(*) from dbo.ExternalTxTest";
            return (int)cmd.ExecuteScalar();
        }

        // ---------- the two failures it exists to fix ----------

        [TestMethod]
        public void WithoutUseTransaction_ACommandOnAPendingTransaction_IsRefusedByTheDriver()
        {
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();
            using var callerTx = conn.BeginTransaction();

            var db = new SqlDbCommunication(conn);

            // Left as-is deliberately: the caller has not handed the transaction over, so the ORM cannot
            // know about it, and the driver's own message is the clearest thing anyone will see.
            var thrown = Assert.ThrowsException<InvalidOperationException>(
                () => db.ExecuteScalarCommand<int>("select 1", null, CommandType.Text));
            StringAssert.Contains(thrown.Message, "pending local transaction");

            callerTx.Rollback();
        }

        [TestMethod]
        public void WithoutUseTransaction_TransactionOnAPendingTransaction_IsRefusedByTheDriver()
        {
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();
            using var callerTx = conn.BeginTransaction();

            var db = new SqlDbCommunication(conn);

            var thrown = Assert.ThrowsException<InvalidOperationException>(
                () => db.Transaction(() => { }));
            StringAssert.Contains(thrown.Message, "parallel transactions");

            callerTx.Rollback();
        }

        // ---------- what UseTransaction buys ----------

        [TestMethod]
        public void AfterUseTransaction_CommandsEnlistWithoutAnyTransactionCall()
        {
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();
            using var callerTx = conn.BeginTransaction();

            var db = new SqlDbCommunication(conn);
            db.UseTransaction(callerTx);

            db.ExecuteNonQueryCommand(
                "insert into dbo.ExternalTxTest (Id) values (1)", null, CommandType.Text);

            Assert.AreEqual(1, db.ExecuteScalarCommand<int>(
                "select count(*) from dbo.ExternalTxTest", null, CommandType.Text),
                "The command must run inside the caller's transaction and see its own write.");

            callerTx.Rollback();
            Assert.AreEqual(0, RowCount(), "The caller owns the outcome; rolling back must undo the insert.");
        }

        /// <summary>
        ///     The scenario this was built for: business code already written as <c>Transaction(...)</c> has
        ///     to keep working unchanged when a caller supplies a transaction from outside.
        /// </summary>
        [TestMethod]
        public void AfterUseTransaction_TransactionRunsTheWorkAndCommitsNothing()
        {
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();
            using var callerTx = conn.BeginTransaction();

            var db = new SqlDbCommunication(conn);
            db.UseTransaction(callerTx);

            // Unchanged business code, nested for good measure.
            db.Transaction(() =>
            {
                db.Transaction(() =>
                {
                    db.ExecuteNonQueryCommand(
                        "insert into dbo.ExternalTxTest (Id) values (2)", null, CommandType.Text);
                });
            });

            // Had Transaction committed, the rollback below could not undo the insert.
            callerTx.Rollback();
            Assert.AreEqual(0, RowCount());
        }

        [TestMethod]
        public async Task AfterUseTransaction_TransactionAsyncRunsTheWorkAndCommitsNothing()
        {
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();
            using var callerTx = conn.BeginTransaction();

            var db = new SqlDbCommunication(conn);
            db.UseTransaction(callerTx);

            await db.TransactionAsync(async () =>
            {
                await db.ExecuteNonQueryCommandAsync(
                    "insert into dbo.ExternalTxTest (Id) values (3)", null, CommandType.Text, default);
            });

            callerTx.Rollback();
            Assert.AreEqual(0, RowCount());
        }

        [TestMethod]
        public void AfterUseTransaction_TheCallersWorkCanStillBeCommitted()
        {
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();
            using var callerTx = conn.BeginTransaction();

            var db = new SqlDbCommunication(conn);
            db.UseTransaction(callerTx);

            db.Transaction(() => db.ExecuteNonQueryCommand(
                "insert into dbo.ExternalTxTest (Id) values (4)", null, CommandType.Text));

            callerTx.Commit();
            Assert.AreEqual(1, RowCount(), "Committing is the caller's to do, and must still work.");
        }

        /// <summary>
        ///     A savepoint inside the caller's transaction is safe -- rolling back to one leaves the
        ///     surrounding transaction usable -- and becomes available because UseTransaction satisfies the
        ///     "there is an outer transaction" requirement.
        /// </summary>
        [TestMethod]
        public void AfterUseTransaction_SavepointsWorkInsideTheCallersTransaction()
        {
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();
            using var callerTx = conn.BeginTransaction();

            var db = new SqlDbCommunication(conn);
            db.UseTransaction(callerTx);

            db.ExecuteNonQueryCommand(
                "insert into dbo.ExternalTxTest (Id) values (5)", null, CommandType.Text);

            Assert.ThrowsException<InvalidOperationException>(() =>
                db.TransactionWithSavepoint(() =>
                {
                    db.ExecuteNonQueryCommand(
                        "insert into dbo.ExternalTxTest (Id) values (6)", null, CommandType.Text);
                    throw new InvalidOperationException("business failure");
                }));

            Assert.AreEqual(1, db.ExecuteScalarCommand<int>(
                "select count(*) from dbo.ExternalTxTest", null, CommandType.Text),
                "Only the savepointed insert must be undone, leaving the caller's transaction usable.");

            callerTx.Commit();
            Assert.AreEqual(1, RowCount());
        }

        // ---------- handing it back ----------

        [TestMethod]
        public void UseTransactionNull_HandsControlBack()
        {
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();
            var callerTx = conn.BeginTransaction();

            var db = new SqlDbCommunication(conn);
            db.UseTransaction(callerTx);
            db.Transaction(() => db.ExecuteNonQueryCommand(
                "insert into dbo.ExternalTxTest (Id) values (7)", null, CommandType.Text));
            callerTx.Commit();
            callerTx.Dispose();

            db.UseTransaction(null);

            // Transaction() owns the outcome again, so this one really does commit.
            db.Transaction(() => db.ExecuteNonQueryCommand(
                "insert into dbo.ExternalTxTest (Id) values (8)", null, CommandType.Text));

            Assert.AreEqual(2, RowCount());
        }

        // ---------- IsInTransaction ----------

        [TestMethod]
        public void IsInTransaction_DoesNotDistinguishWhoStartedIt()
        {
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();
            using var callerTx = conn.BeginTransaction();

            var own = new SqlDbCommunication(ConnectionString);
            Assert.IsFalse(own.IsInTransaction);
            own.Transaction(() => Assert.IsTrue(own.IsInTransaction, "Inside a transaction begun here."));
            Assert.IsFalse(own.IsInTransaction, "And false again once it has ended.");

            var handed = new SqlDbCommunication(conn);
            Assert.IsFalse(handed.IsInTransaction);
            handed.UseTransaction(callerTx);
            Assert.IsTrue(handed.IsInTransaction,
                "A transaction handed over counts too -- to a caller asking whether its work is already " +
                "covered, the two are the same thing.");

            handed.UseTransaction(null);
            Assert.IsFalse(handed.IsInTransaction);

            callerTx.Rollback();
        }

        // ---------- isolation level ----------

        [TestMethod]
        public void Transaction_BeginsAtTheRequestedIsolationLevel()
        {
            var db = new SqlDbCommunication(ConnectionString);

            db.Transaction(() =>
            {
                // Reads the level the server actually gave this session, so the level really reached the
                // BeginTransaction call rather than merely being stored.
                var level = db.ExecuteScalarCommand<string>(
                    @"select transaction_isolation_level
                      from sys.dm_exec_sessions where session_id = @@spid", null, CommandType.Text);
                Assert.AreEqual("4", level, "4 is SERIALIZABLE in sys.dm_exec_sessions.");
            }, IsolationLevel.Serializable);
        }

        [TestMethod]
        public async Task TransactionAsync_BeginsAtTheRequestedIsolationLevel()
        {
            var db = new SqlDbCommunication(ConnectionString);

            await db.TransactionAsync(async () =>
            {
                var level = await db.ExecuteScalarCommandAsync<string>(
                    @"select transaction_isolation_level
                      from sys.dm_exec_sessions where session_id = @@spid", null, CommandType.Text, default);
                Assert.AreEqual("4", level);
            }, IsolationLevel.Serializable);
        }

        [TestMethod]
        public void AfterUseTransaction_AnIsolationLevelIsIgnoredRatherThanRefused()
        {
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();
            using var callerTx = conn.BeginTransaction(IsolationLevel.ReadCommitted);

            var db = new SqlDbCommunication(conn);
            db.UseTransaction(callerTx);

            // The surrounding transaction already exists and its level stands. Refusing here would break
            // business code written as Transaction(work, level) the moment a caller hands one over, which
            // is the whole point of UseTransaction.
            db.Transaction(() =>
            {
                var level = db.ExecuteScalarCommand<string>(
                    @"select transaction_isolation_level
                      from sys.dm_exec_sessions where session_id = @@spid", null, CommandType.Text);
                Assert.AreEqual("2", level, "2 is READ COMMITTED -- the caller's level, not the requested one.");
            }, IsolationLevel.Serializable);

            callerTx.Rollback();
        }

        // ---------- the guards ----------

        [TestMethod]
        public void AStaleTransaction_IsReported_RatherThanRunningOutsideAnyTransaction()
        {
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();
            var callerTx = conn.BeginTransaction();

            var db = new SqlDbCommunication(conn);
            db.UseTransaction(callerTx);

            callerTx.Rollback();
            callerTx.Dispose();

            var thrown = Assert.ThrowsException<InvalidOperationException>(
                () => db.ExecuteNonQueryCommand(
                    "insert into dbo.ExternalTxTest (Id) values (9)", null, CommandType.Text));

            StringAssert.Contains(thrown.Message, nameof(IDbCommunication.UseTransaction));
            Assert.AreEqual(0, RowCount(), "Nothing may have been written outside a transaction.");
        }

        [TestMethod]
        public void UseTransaction_InsideAnOrmOwnedTransaction_IsRefused()
        {
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();

            var db = new SqlDbCommunication(ConnectionString);

            db.Transaction(() =>
            {
                using var strayTx = conn.BeginTransaction();
                var thrown = Assert.ThrowsException<InvalidOperationException>(
                    () => db.UseTransaction(strayTx));
                StringAssert.Contains(thrown.Message, "running");
                strayTx.Rollback();
            });
        }

        [TestMethod]
        public void UseTransaction_WithAnAlreadyFinishedTransaction_IsRefused()
        {
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();
            var callerTx = conn.BeginTransaction();
            callerTx.Rollback();
            callerTx.Dispose();

            var db = new SqlDbCommunication(conn);

            var thrown = Assert.ThrowsException<InvalidOperationException>(
                () => db.UseTransaction(callerTx));
            StringAssert.Contains(thrown.Message, "disposed");
        }

        [TestMethod]
        public void UseTransaction_WithATransactionOnAnotherConnection_IsRefused()
        {
            using var ownConn = new SqlConnection(ConnectionString);
            ownConn.Open();

            using var otherConn = new SqlConnection(ConnectionString);
            otherConn.Open();
            using var otherTx = otherConn.BeginTransaction();

            var db = new SqlDbCommunication(ownConn);

            var thrown = Assert.ThrowsException<InvalidOperationException>(
                () => db.UseTransaction(otherTx));
            StringAssert.Contains(thrown.Message, "different connection");

            otherTx.Rollback();
        }
    }
}
