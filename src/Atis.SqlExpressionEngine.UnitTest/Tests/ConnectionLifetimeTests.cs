using Atis.Orm.Querying;
using Atis.Orm.SqlServer;
using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         Covers the reference count that <c>DbCommunicationBase</c> keeps over the connection it
    ///         opens for itself. The field holding that connection is a single slot with no ownership
    ///         information, so before the count existed the first caller to finish closed it under
    ///         everyone else — a data reader mid-enumeration being the case that mattered.
    ///     </para>
    ///     <para>
    ///         These drive <see cref="SqlDbCommunication"/> against a real server rather than a fake,
    ///         because what is being asserted is that a live connection stays usable; a fake would only
    ///         echo back the test's own idea of open and closed.
    ///     </para>
    /// </summary>
    [TestClass]
    public class ConnectionLifetimeTests
    {
        private const string ConnectionString =
            "server=localhost;database=TestDb;integrated security=true;TrustServerCertificate=True";

        /// <summary>
        ///     Surfaces the connection the base class is currently working on, which is protected. Nothing
        ///     else here can tell "still the same open connection" from "silently replaced with a fresh
        ///     one", and the second is exactly the failure being guarded against.
        /// </summary>
        private sealed class ProbeDbCommunication : SqlDbCommunication
        {
            public ProbeDbCommunication(string connString) : base(connString) { }

            public DbConnection CurrentConnection => this.GetCurrentConnection();
        }

        // ---------- the count ----------

        [TestMethod]
        public void NestedOpen_KeepsOneConnection_UntilTheLastCloseReleasesIt()
        {
            var db = new ProbeDbCommunication(ConnectionString);

            db.OpenConnection();
            var opened = db.CurrentConnection;
            Assert.IsNotNull(opened);
            Assert.AreEqual(ConnectionState.Open, opened.State);

            db.OpenConnection();
            Assert.AreSame(opened, db.CurrentConnection,
                "A second open must reuse the connection, not create another one.");

            db.CloseConnection();
            Assert.AreSame(opened, db.CurrentConnection,
                "One claim is still outstanding, so the connection must still be here.");
            Assert.AreEqual(ConnectionState.Open, db.CurrentConnection.State);

            db.CloseConnection();
            Assert.IsNull(db.CurrentConnection, "The last claim released must close and drop it.");
        }

        [TestMethod]
        public void ACommandRunningUnderAHeldConnection_LeavesItOpen()
        {
            var db = new ProbeDbCommunication(ConnectionString);

            db.OpenConnection();
            var held = db.CurrentConnection;
            try
            {
                // Opens and closes a connection of its own accord -- which, before the count, meant
                // closing this one.
                Assert.AreEqual(1, db.ExecuteScalarCommand<int>("select 1", null, CommandType.Text));

                Assert.AreSame(held, db.CurrentConnection,
                    "The command must release only its own claim, leaving the held one untouched.");
                Assert.AreEqual(ConnectionState.Open, db.CurrentConnection.State);
            }
            finally
            {
                db.CloseConnection();
            }

            Assert.IsNull(db.CurrentConnection);
        }

        [TestMethod]
        public async Task ACommandRunningUnderAHeldConnection_LeavesItOpen_Async()
        {
            var db = new ProbeDbCommunication(ConnectionString);

            await db.OpenConnectionAsync(default);
            var held = db.CurrentConnection;
            try
            {
                Assert.AreEqual(1, await db.ExecuteScalarCommandAsync<int>(
                    "select 1", null, CommandType.Text, default));

                Assert.AreSame(held, db.CurrentConnection);
                Assert.AreEqual(ConnectionState.Open, db.CurrentConnection.State);
            }
            finally
            {
                await db.CloseConnectionAsync();
            }

            Assert.IsNull(db.CurrentConnection);
        }

        [TestMethod]
        public void AnUnbalancedClose_IsANoOp_RatherThanDrivingTheCountNegative()
        {
            var db = new ProbeDbCommunication(ConnectionString);

            db.OpenConnection();
            db.CloseConnection();
            Assert.IsNull(db.CurrentConnection);

            // OpenConnection/CloseConnection are public on IDbCommunication, so a caller can close what
            // it never opened. Were the count allowed to go negative, the next open would come back
            // already in debt and the connection would be closed under its holder.
            db.CloseConnection();
            db.CloseConnection();

            db.OpenConnection();
            db.OpenConnection();
            var opened = db.CurrentConnection;
            db.CloseConnection();

            Assert.AreSame(opened, db.CurrentConnection,
                "Two opens still need two closes; the earlier stray closes must not have counted.");
            db.CloseConnection();
            Assert.IsNull(db.CurrentConnection);
        }

        // ---------- the enumerators ----------

        [TestMethod]
        public void AnEnumeratorDisposedWithoutEnumerating_ReleasesNothing()
        {
            var db = new ProbeDbCommunication(ConnectionString);

            db.OpenConnection();
            var held = db.CurrentConnection;
            try
            {
                // The connection is opened lazily on the first MoveNext, so this enumerator never took a
                // claim -- and must not release one.
                var enumerator = new DbEnumerator<int>(
                    "select 1", null, r => r.GetInt32(0), db);
                enumerator.Dispose();

                Assert.AreSame(held, db.CurrentConnection,
                    "Disposing an enumerator that never opened anything must leave the held connection alone.");
                Assert.AreEqual(ConnectionState.Open, db.CurrentConnection.State);
            }
            finally
            {
                db.CloseConnection();
            }

            Assert.IsNull(db.CurrentConnection);
        }

        [TestMethod]
        public async Task AnAsyncEnumeratorDisposedTwice_ReleasesOneClaim()
        {
            var db = new ProbeDbCommunication(ConnectionString);

            db.OpenConnection();
            var held = db.CurrentConnection;
            try
            {
                var enumerator = new DbAsyncEnumerator<int>(
                    "select 1", null, r => r.GetInt32(0), db);
                Assert.IsTrue(await enumerator.MoveNextAsync());

                // Both spellings on one instance: DisposeAsync used to skip the disposed-check that
                // Dispose honours, so the pair released the connection twice for a single claim.
                enumerator.Dispose();
                await enumerator.DisposeAsync();

                Assert.AreSame(held, db.CurrentConnection,
                    "The second disposal must not release a claim the first already gave up.");
                Assert.AreEqual(ConnectionState.Open, db.CurrentConnection.State);
            }
            finally
            {
                db.CloseConnection();
            }

            Assert.IsNull(db.CurrentConnection);
        }
    }
}
