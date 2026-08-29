using Atis.Orm.DataAccess;
using Atis.Orm.Querying;
using Atis.Orm.SqlServer;
using System;
using System.Data;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         Covers the guard that stops one <c>IDbCommunication</c> instance being driven by two flows of
    ///         execution at once. An instance holds a single connection, a single transaction and a single
    ///         reference count over that connection, so concurrent use has no correct behaviour to fall back
    ///         on -- the guard reports it where it happens rather than letting it surface later as a
    ///         connection closed under a reader or a command outside the transaction it was meant to be in.
    ///     </para>
    ///     <para>
    ///         Half of what is asserted here is what must <em>not</em> be rejected: an <c>await</c> resuming
    ///         on another thread, a command inside a transaction, an async enumeration hopping threads
    ///         between rows. Those are ordinary correct code, and a guard keyed on thread identity rather
    ///         than on overlap would break every one of them.
    ///     </para>
    ///     <para>
    ///         Everything here is gated rather than timed: a sleep would make the assertions depend on the
    ///         scheduler, and a concurrency test that passes because it lost a race is worse than none.
    ///     </para>
    /// </summary>
    [TestClass]
    public class ConcurrencyDetectionTests
    {
        private const string ConnectionString =
            "server=localhost;database=TestDb;integrated security=true;TrustServerCertificate=True";

        /// <summary>
        ///     The same server with MARS on, for the one test that needs a command to run while a reader is
        ///     open on the same connection. Whether that is allowed is the driver's business and not the
        ///     guard's -- but it has to be allowed for the guard's part to be visible at all.
        /// </summary>
        private const string MarsConnectionString = ConnectionString + ";MultipleActiveResultSets=True";

        /// <summary>
        ///     <para>
        ///         Starts <paramref name="work"/> on a flow that is genuinely unrelated to the caller's,
        ///         modelling the other thread or the other request that the guard exists to catch.
        ///     </para>
        ///     <para>
        ///         Suppressing the execution context around the start is the whole point: a thread or task
        ///         started from <em>inside</em> a critical section otherwise inherits the starting flow's
        ///         state and is let straight through, which would make these tests pass for the wrong
        ///         reason.
        ///     </para>
        ///     <para>
        ///         A real thread rather than <c>Task.Run</c>, and not for isolation's sake: waiting on a
        ///         task can run it inline on the waiting thread, and an inlined task started with the flow
        ///         suppressed carries no captured context -- so it executes under the waiting flow's own
        ///         state and quietly undoes the suppression. A thread cannot be inlined.
        ///     </para>
        /// </summary>
        private static Thread StartOnAnUnrelatedFlow(Action work, Action<Exception> onFailure)
        {
            var thread = new Thread(() =>
            {
                try
                {
                    work();
                }
                catch (Exception ex)
                {
                    onFailure(ex);
                }
            })
            {
                IsBackground = true,
            };

            // Fully qualified: Atis.Orm.DataAccess has an ExecutionContext of its own, unrelated to this
            // one. Suppressed around Start, which is where the context would be captured.
            using (System.Threading.ExecutionContext.SuppressFlow())
            {
                thread.Start();
            }
            return thread;
        }

        private static void AssertRejectedFromAnotherFlow(ConcurrencyDetector detector)
        {
            Exception failure = null;
            var thread = StartOnAnUnrelatedFlow(
                () => Assert.ThrowsException<InvalidOperationException>(() => detector.EnterCriticalSection()),
                ex => failure = ex);
            thread.Join();
            if (failure != null)
                ExceptionDispatchInfo.Capture(failure).Throw();
        }

        /// <summary>
        ///     Holds a critical section open on a flow of its own until disposed, so a test can ask what a
        ///     second flow gets while it is held.
        /// </summary>
        private sealed class SectionHolder : IDisposable
        {
            private readonly ManualResetEventSlim entered = new ManualResetEventSlim();
            private readonly ManualResetEventSlim release = new ManualResetEventSlim();
            private readonly Thread thread;
            private Exception failure;

            public SectionHolder(ConcurrencyDetector detector)
            {
                this.thread = StartOnAnUnrelatedFlow(
                    () =>
                    {
                        using (detector.EnterCriticalSection())
                        {
                            this.entered.Set();
                            this.release.Wait();
                        }
                    },
                    ex =>
                    {
                        this.failure = ex;
                        // Set as well, so a failure to enter does not leave the constructor waiting forever.
                        this.entered.Set();
                    });
                this.entered.Wait();
            }

            public void Dispose()
            {
                this.release.Set();
                this.thread.Join();
                this.entered.Dispose();
                this.release.Dispose();
                if (this.failure != null)
                    ExceptionDispatchInfo.Capture(this.failure).Throw();
            }
        }

        // ---------- the detector itself; these need no database ----------

        [TestMethod]
        public void ASecondFlowArrivingWhileTheSectionIsHeld_IsRejected()
        {
            var detector = new ConcurrencyDetector();

            using (new SectionHolder(detector))
            {
                var ex = Assert.ThrowsException<InvalidOperationException>(
                    () => detector.EnterCriticalSection());

                // The message is the whole value of the guard: without it this surfaces later as a
                // connection or transaction failure that says nothing about the actual mistake.
                StringAssert.Contains(ex.Message, "A second operation was started");
            }
        }

        [TestMethod]
        public void TheSectionIsAvailableAgain_OnceTheHolderLeaves()
        {
            var detector = new ConcurrencyDetector();

            using (new SectionHolder(detector)) { }

            // A detector that failed to release would wedge the instance for the rest of its life, which is
            // a worse bug than the one it is there to report.
            using (detector.EnterCriticalSection()) { }
        }

        [TestMethod]
        public void NestingWithinOneFlow_IsAllowed_AndOnlyTheOutermostExitReleases()
        {
            var detector = new ConcurrencyDetector();

            using (detector.EnterCriticalSection())
            {
                // A command inside a transaction is exactly this shape, and so is a connection opened
                // inside a command.
                using (detector.EnterCriticalSection())
                {
                    using (detector.EnterCriticalSection())
                    {
                    }

                    // Two levels are still held, so a stranger must still be turned away -- the inner
                    // exit released a level, not the section.
                    AssertRejectedFromAnotherFlow(detector);
                }

                AssertRejectedFromAnotherFlow(detector);
            }

            using (detector.EnterCriticalSection()) { }
        }

        [TestMethod]
        public async Task AnAwaitResumingOnAnotherThread_IsNotRejected()
        {
            var detector = new ConcurrencyDetector();

            // The whole reason for keying on the execution context rather than on the thread: this method
            // is one sequential flow, and stays one even though the thread underneath it changes.
            using (detector.EnterCriticalSection())
            {
                await Task.Yield();
                await Task.Run(() => { }).ConfigureAwait(false);

                using (detector.EnterCriticalSection())
                {
                }
            }

            using (detector.EnterCriticalSection()) { }
        }

        [TestMethod]
        public async Task AnAsyncSectionEnteredAndLeftAcrossAwaits_ReleasesProperly()
        {
            var detector = new ConcurrencyDetector();

            // A write to an AsyncLocal made inside an async method is invisible to its caller afterwards,
            // so the release cannot be driven by that alone. This is the shape that catches it: an async
            // method entering, awaiting a nested async method that also enters, and both leaving.
            await OuterAsync(detector).ConfigureAwait(false);

            // Both have left, so the section is free again -- the assertion that the lost AsyncLocal
            // decrement did not strand it.
            using (detector.EnterCriticalSection()) { }

            async Task OuterAsync(ConcurrencyDetector d)
            {
                using (d.EnterCriticalSection())
                {
                    await InnerAsync(d).ConfigureAwait(false);
                    // The inner one has left; the outer one has not, so the section is still held.
                    AssertRejectedFromAnotherFlow(d);
                }
            }

            async Task InnerAsync(ConcurrencyDetector d)
            {
                using (d.EnterCriticalSection())
                {
                    await Task.Yield();
                }
            }
        }

        // ---------- the guard on a real instance; these need the server ----------

        /// <summary>
        ///     Starts a transaction on the given instance and leaves it running until disposed, so a test
        ///     can ask what a second flow gets while the instance is busy. The un-awaited call is the shape
        ///     of the bug being caught, and also the only way to have an operation genuinely in flight.
        /// </summary>
        private sealed class BusyInstance : IDisposable
        {
            private readonly TaskCompletionSource<bool> release =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly Task task;

            public BusyInstance(SqlDbCommunication db)
            {
                var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                this.task = db.TransactionAsync(async () =>
                {
                    entered.SetResult(true);
                    await this.release.Task.ConfigureAwait(false);
                });
                entered.Task.GetAwaiter().GetResult();
            }

            public void Dispose()
            {
                this.release.SetResult(true);
                this.task.GetAwaiter().GetResult();
            }
        }

        [TestMethod]
        public void ACommandStartedWhileAnotherOperationIsInFlight_IsRejected()
        {
            var db = new SqlDbCommunication(ConnectionString);

            using (new BusyInstance(db))
            {
                var ex = Assert.ThrowsException<InvalidOperationException>(
                    () => db.ExecuteScalarCommand<int>("select 1", null, CommandType.Text));

                StringAssert.Contains(ex.Message, "A second operation was started");
            }
        }

        [TestMethod]
        public void OpeningAReaderWhileAnotherOperationIsInFlight_IsRejected()
        {
            var db = new SqlDbCommunication(ConnectionString);

            using (new BusyInstance(db))
            {
                Assert.ThrowsException<InvalidOperationException>(
                    () => db.OpenReader("select 1", null, CommandType.Text, r => r.GetInt32(0)));
            }
        }

        [TestMethod]
        public void ReadingAnOpenSession_WhileAnotherOperationIsInFlight_IsRejected()
        {
            var db = new SqlDbCommunication(ConnectionString);

            using (var session = db.OpenReader(
                       "select 1 union all select 2", null, CommandType.Text, r => r.GetInt32(0)))
            {
                Assert.IsTrue(session.Read());

                using (new BusyInstance(db))
                {
                    // The session shares the instance's detector, so a row read is an operation on the
                    // instance like any other -- otherwise the one long lived thing in the API would be the
                    // one thing left unguarded.
                    Assert.ThrowsException<InvalidOperationException>(() => session.Read());
                    Assert.ThrowsException<InvalidOperationException>(() => session.Current);
                }

                // Still perfectly usable once the other flow has finished: the guard rejects the call, it
                // does not poison what it was called on.
                Assert.IsTrue(session.Read());
                Assert.AreEqual(2, session.Current);
            }
        }

        [TestMethod]
        public void TheCheckCanBeTurnedOff()
        {
            var db = new SqlDbCommunication(ConnectionString) { ThreadSafetyChecksEnabled = false };

            using (new BusyInstance(db))
            {
                // The transaction is running and its connection is already open, so this reaches no further
                // than a state check -- enough to show the guard is gone without actually racing the server.
                db.OpenConnection();
            }
        }

        // ---------- what must not be rejected ----------

        [TestMethod]
        public void CommandsInsideATransaction_AreNotRejected()
        {
            var db = new SqlDbCommunication(ConnectionString);

            db.Transaction(() =>
            {
                Assert.AreEqual(1, db.ExecuteScalarCommand<int>("select 1", null, CommandType.Text));

                db.Transaction(() =>
                {
                    db.TransactionWithSavepoint(() =>
                    {
                        Assert.AreEqual(2, db.ExecuteScalarCommand<int>("select 2", null, CommandType.Text));
                    });
                });
            });
        }

        [TestMethod]
        public async Task CommandsInsideAnAsyncTransaction_AreNotRejected()
        {
            var db = new SqlDbCommunication(ConnectionString);

            await db.TransactionAsync(async () =>
            {
                Assert.AreEqual(1, await db.ExecuteScalarCommandAsync<int>(
                    "select 1", null, CommandType.Text, default).ConfigureAwait(false));

                await db.TransactionWithSavepointAsync(async () =>
                {
                    Assert.AreEqual(2, await db.ExecuteScalarCommandAsync<int>(
                        "select 2", null, CommandType.Text, default).ConfigureAwait(false));
                }).ConfigureAwait(false);
            });
        }

        [TestMethod]
        public async Task CommandsAwaitedOneAfterAnother_AreNotRejected()
        {
            var db = new SqlDbCommunication(ConnectionString);

            for (var i = 0; i < 5; i++)
            {
                Assert.AreEqual(i, await db.ExecuteScalarCommandAsync<int>(
                    $"select {i}", null, CommandType.Text, default).ConfigureAwait(false));
            }
        }

        [TestMethod]
        public async Task AnAsyncEnumerationHoppingThreads_IsNotRejected()
        {
            var db = new SqlDbCommunication(ConnectionString);

            var enumerator = new DbAsyncEnumerator<int>(
                "select 1 union all select 2 union all select 3", null, r => r.GetInt32(0), db);
            try
            {
                var total = 0;
                while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    // ConfigureAwait(false) throughout, so each row may well be read on a different thread
                    // from the last. Nothing overlaps, so nothing may be rejected.
                    total += enumerator.Current;
                }
                Assert.AreEqual(6, total);
            }
            finally
            {
                await enumerator.DisposeAsync().ConfigureAwait(false);
            }
        }

        [TestMethod]
        public void ACommandRunningWhileASessionIsOpen_OnTheSameFlow_IsNotRejected()
        {
            // MARS, because the command and the reader end up on the one connection the instance holds.
            var db = new SqlDbCommunication(MarsConnectionString);

            using (var session = db.OpenReader("select 1", null, CommandType.Text, r => r.GetInt32(0)))
            {
                Assert.IsTrue(session.Read());

                // One flow doing two things in turn, which the reference count over the connection exists
                // to support. The guard is about overlap, and there is none here.
                Assert.AreEqual(7, db.ExecuteScalarCommand<int>("select 7", null, CommandType.Text));

                Assert.AreEqual(1, session.Current);
            }
        }
    }
}
