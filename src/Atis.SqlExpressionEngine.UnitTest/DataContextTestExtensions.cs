using System;
using System.Threading;
using System.Threading.Tasks;

namespace Atis.SqlExpressionEngine.UnitTest
{
    /// <summary>
    ///     <para>
    ///         Helpers for tests that exercise real DML against the shared test database.
    ///     </para>
    /// </summary>
    internal static class DataContextTestExtensions
    {
        /// <summary>
        ///     <para>
        ///         Runs <paramref name="work"/> inside a transaction that is always rolled back, so a
        ///         test can execute real DML without leaving the seeded fixture data changed.
        ///     </para>
        ///     <para>
        ///         Use this for any test that writes to the tables <see cref="TestDatabaseSetup"/>
        ///         seeds. Committing there mutates the fixture permanently — for the rest of the run
        ///         and for every run afterwards — which is how <c>Update_execution_test</c> came to
        ///         append <c>_Updated</c> to <c>Employee.FirstName</c> until the column overflowed,
        ///         and took <c>Cache_hit_rebinds_new_variable_value</c> down with it.
        ///     </para>
        ///     <para>
        ///         Do NOT use this in tests that are asserting commit behaviour itself — those must
        ///         drive <c>Transaction</c> directly. Tests owning a private table they reset in
        ///         <c>[TestInitialize]</c> do not need this either.
        ///     </para>
        ///     <para>
        ///         <c>Transaction</c> commits when its delegate returns and only rolls back when the
        ///         delegate throws, so the rollback has to be signalled with an exception. The signal
        ///         type is private, so nothing but this method can catch it by accident.
        ///     </para>
        /// </summary>
        public static void TransactionWithRollback(this Atis.Orm.DataContext db, Action work)
        {
            if (db is null)
                throw new ArgumentNullException(nameof(db));
            if (work is null)
                throw new ArgumentNullException(nameof(work));

            try
            {
                db.Transaction(() =>
                {
                    work();
                    throw new RollbackSignal();
                });
            }
            catch (RollbackSignal)
            {
                // Expected. Everything work() did has been rolled back; a genuine failure inside
                // work() is a different type and propagates.
            }
        }

        /// <summary>
        ///     The asynchronous <see cref="TransactionWithRollback"/>, for tests awaiting real DML. The
        ///     same rules apply.
        /// </summary>
        public static async Task TransactionWithRollbackAsync(
            this Atis.Orm.DataContext db, Func<Task> work, CancellationToken cancellationToken = default)
        {
            if (db is null)
                throw new ArgumentNullException(nameof(db));
            if (work is null)
                throw new ArgumentNullException(nameof(work));

            try
            {
                await db.TransactionAsync(async () =>
                {
                    await work().ConfigureAwait(false);
                    throw new RollbackSignal();
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (RollbackSignal)
            {
                // Expected, exactly as in the synchronous version.
            }
        }

        private sealed class RollbackSignal : Exception
        {
        }
    }
}
