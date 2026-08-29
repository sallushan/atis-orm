using System;
using System.Threading;

namespace Atis.Orm.DataAccess
{
    /*
     * IMPORTANT:
     *      Summary: one async/await operation can run on several different thread pool threads over its
     *      lifetime, but it stays ONE ExecutionContext throughout. So the ExecutionContext is what
     *      identifies "the same piece of work"; the Thread ID identifies nothing useful here.
     *
     *      Call that one piece of work a FLOW. A flow is what we allow to re-enter; a different flow is
     *      what we reject. The threads underneath a flow can change as often as they like.
     *
     *      For example,
     *
     *          await DoSomeWorkAsync();
     *
     *      Say DoSomeWorkAsync starts on Thread A. Inside, it reaches an await on something that has not
     *      finished yet -- typically I/O: a network round trip to the database, a disk read. At that
     *      point the compiler-generated async state machine captures the current ExecutionContext,
     *      records where it got to, and RETURNS. Thread A is not terminated and is not left waiting -- it
     *      goes straight back to the thread pool and picks up completely unrelated work. That is the
     *      whole reason async exists: nobody is blocked while the database thinks.
     *
     *      When the I/O completes, the runtime takes some thread out of the pool -- possibly Thread A
     *      again, possibly Thread Q, nothing promises either way -- restores the SAME captured
     *      ExecutionContext onto it, and resumes the method from where it stopped. Different thread, same
     *      flow.
     *
     *      This is why the traditional thread-based locking APIs are useless here. Monitor (`lock`) is
     *      thread-affine: it grants re-entry only to the exact thread that took the lock, and only that
     *      thread may release it -- releasing from another one throws SynchronizationLockException. So a
     *      lock spanning an await is not merely unhelpful, it is broken, and C# refuses to compile
     *      `await` inside `lock` for exactly this reason.
     *
     *      Two separate problems, two separate tools -- do not confuse them:
     *
     *        (1) "Is anybody inside right now?"  -> inCriticalSection, flipped with
     *            Interlocked.CompareExchange. Needed because two threads could otherwise both read 0 and
     *            both conclude the section was free. CompareExchange makes read-and-write one indivisible
     *            step, so exactly one of them can win.
     *
     *            Note this NEVER blocks and NEVER throws by itself. It does not make thread B wait for
     *            thread A to finish. It just reports who won; the throwing is done by our own `if`
     *            immediately after.
     *
     *        (2) "The section is taken -- but is it taken by ME?"  -> flowDepth, an AsyncLocal<int>.
     *            An AsyncLocal value belongs to the FLOW, not to the thread and not to the object. So on
     *            one and the same ConcurrencyDetector instance, flow A can read 1 while flow B reads 0,
     *            and flow A still reads 1 after it has hopped to another thread.
     *
     *            Losing the CompareExchange means only "someone is inside". If flowDepth is non-zero,
     *            that someone is us and this is legitimate nesting (a command inside a transaction).
     *            If flowDepth is zero, it is a stranger, and that is the bug we throw for.
     *
     * Usages:
     *      CompareExchange - compares and, ONLY if it matched, writes -- as one atomic hardware
     *                  operation -- and returns the value that was there before. That conditional part is
     *                  what makes it a claim rather than a plain assignment.
     *      flowDepth - one value per flow, per instance:
     *                      concurrencyDetector1.flowDepth.Value = 1; // flow A
     *                          |
     *                        same instance
     *                          |
     *                      concurrencyDetector1.flowDepth.Value = 0; // flow B
     *                  It does not detect anything on its own. inCriticalSection says "occupied";
     *                  flowDepth answers "occupied by me, or by someone else?".
     *      refCount - how deep the ONE flow inside has nested, so inCriticalSection is cleared by the
     *                  OUTERMOST exit and not by an inner one. (There is never more than one flow inside,
     *                  so "the last flow to leave" would be the wrong way to say it.)
     *                  Why it is needed at all, given flowDepth already counts the same thing: an
     *                  AsyncLocal write made inside an `async` method is NOT visible to that method's
     *                  caller after it returns. So when a nested async call decrements flowDepth, the
     *                  caller never sees the decrement -- its own copy still holds the old value. refCount
     *                  is an ordinary field on the object, so it cannot be lost that way. It is the single
     *                  source of truth for releasing.
     *                  It needs no interlocking of its own: only the one flow already inside ever touches
     *                  it.
     *      Volatile.Write - the guarantee is about ORDERING, not about speed. It is not "everyone sees it
     *                  immediately"; it is "anyone who sees inCriticalSection == 0 is also guaranteed to
     *                  see every write we made before it" -- refCount == 0 in particular, rather than a
     *                  stale leftover count. Without the barrier the CPU or the JIT is free to reorder or
     *                  delay those writes, and the next flow in could win the CompareExchange and then
     *                  read the previous flow's refCount. Interlocked.CompareExchange on the way in is a
     *                  barrier too, so the two pair up: release-then-acquire.
     * */


    /// <summary>
    ///     <para>
    ///         Catches an <see cref="IDbCommunication"/> instance being used by two flows of execution at
    ///         once and turns it into an exception that names the mistake, instead of the assortment of
    ///         failures the mistake would otherwise produce further down -- a connection closed under a
    ///         reader, a command enlisted in a transaction that has already been committed, two commands on
    ///         one connection, or nothing at all going wrong until the day the timing changes.
    ///     </para>
    ///     <para>
    ///         <strong>It detects, it does not serialize.</strong> A lock here would make the same misuse
    ///         run, slowly and in an order nobody chose, and would deadlock the moment one flow waited on
    ///         another. Concurrent use of one instance is a bug in the calling code -- an instance holds one
    ///         connection, one transaction and one reference count, so there is no correct concurrent
    ///         behaviour to fall back on. The only useful thing to do with it is report it where it happens.
    ///     </para>
    ///     <para>
    ///         <strong>It is about overlap, not about threads.</strong> Nothing here looks at
    ///         <see cref="Thread.CurrentThread"/>: an <c>await</c> legitimately resumes on whatever thread
    ///         the scheduler has free, so the same sequential <c>async</c> method routinely touches the
    ///         instance from several threads and must keep working. What is rejected is a second operation
    ///         starting while a first is still running -- <c>Task.WhenAll</c> over two calls, a
    ///         fire-and-forget task, a <c>Parallel.ForEach</c> body -- whether or not the threads differ.
    ///     </para>
    ///     <para>
    ///         Re-entering from the flow that already holds the section is allowed and expected: a command
    ///         run inside <see cref="DbCommunicationBase.Transaction(Action)"/> nests inside the
    ///         transaction's own section, and opens and closes a connection inside its own.
    ///     </para>
    ///     <para>
    ///         <strong>What it will not catch.</strong> Two, both inherited from the same design in Entity
    ///         Framework Core, and both the price of the re-entrancy above. A task started from
    ///         <em>inside</em> a section inherits the execution context and with it the right to re-enter,
    ///         so <c>Task.Run(() =&gt; db.Execute(...))</c> written inside a transaction passes the check
    ///         and then genuinely races its parent -- there is no way to tell that task apart from the
    ///         nested call it is indistinguishable from. And because a section covers one operation rather
    ///         than one session, a partly-consumed reader left open on one flow while another flow runs a
    ///         command is caught only if the two actually overlap in time, not merely because the reader is
    ///         still open. Neither is a reason to widen the guard: doing so would reject correct code, which
    ///         costs more than missing these.
    ///     </para>
    /// </summary>
    public sealed class ConcurrencyDetector
    {
        /// <summary>0 when nothing is running, 1 while some flow holds the section.</summary>
        private int inCriticalSection;

        /// <summary>
        ///     <para>
        ///         How deep the flow currently holding the section has nested. This is the only field that
        ///         can answer "is the flow arriving here the one already inside?", because it travels with
        ///         the execution context rather than with the thread -- through an <c>await</c>, through a
        ///         continuation on another thread, and into any task started from inside the section.
        ///     </para>
        ///     <para>
        ///         Per instance rather than static: the question is whether this flow holds
        ///         <em>this</em> detector, and a static one would let a flow holding some other instance's
        ///         section pass the check here.
        ///     </para>
        /// </summary>
        private readonly AsyncLocal<int> flowDepth = new AsyncLocal<int>();

        /// <summary>
        ///     The same depth again, as an ordinary field. Needed as well as <see cref="flowDepth"/>
        ///     because a write to an <see cref="AsyncLocal{T}"/> made inside an <c>async</c> method is not
        ///     visible to that method's caller once it returns: the decrement on the way out of a nested
        ///     call can therefore be lost, and the release has to be driven by something that cannot be.
        ///     Only the flow holding the section touches it, so it needs no interlocking of its own.
        /// </summary>
        private int refCount;

        /// <summary>
        ///     Marks the start of one operation on the instance. Dispose the returned value to end it --
        ///     the section is a <c>using</c> block, never a field.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        ///     Another flow of execution is in the middle of an operation on the same instance.
        /// </exception>
        public ConcurrencyDetectorCriticalSection EnterCriticalSection()
        {
            // CompareExchange below, in plain code:
            //      originalValue = inCriticalSection;
            //      if (inCriticalSection == 0) inCriticalSection = 1;
            //      return originalValue;                       // <-- the value from BEFORE, always
            // ...except the whole thing is one atomic hardware step, so no other thread can slip in
            // between the read and the write. That is the guarantee: inCriticalSection goes 0 -> 1 for
            // exactly one caller, never two.
            //
            // It returns the value that was there BEFORE. So it returns 0 or 1, and which one tells us
            // what happened:
            //  Case-A: it was 0, and we changed it to 1: RETURN VALUE = 0.
            //          We just took the section. Happy path -- `left condition` is false, no exception.
            //  Case-B: it was already 1, so nothing changed: return value = 1.
            //          Someone is already inside. Two very different situations produce this identical
            //          result, and CompareExchange CANNOT tell them apart:
            //              (b1) the flow already inside has called in again (legal nesting), or
            //              (b2) a completely different flow has arrived (the bug).
            //          It does not "allow" or "refuse" anybody -- it only reports. Separating b1 from b2
            //          is exactly, and only, what flowDepth is for.
            //  Case-C: it was 0 but we failed to change it to 1 -- IMPOSSIBLE, by definition. A return of
            //          0 means the write happened. If two flows race, one gets 0 back and the other gets
            //          1 back; there is no third outcome and no half-done outcome.
            //
            // Nutshell (left condition):
            //      left condition == false  ->  the section was free and is now ours. Done, no check needed.
            //      left condition == true   ->  someone is inside. Now we must ask WHO.
            //
            // flowDepth answers "who". It is an AsyncLocal<int>, and that is the crux of the whole class:
            // the value belongs to the FLOW, not to the thread and not to the object. One instance of this
            // class can therefore hold a different flowDepth for every flow that looks at it -- flow A
            // reads 1 while flow B reads 0 -- and flow A keeps reading 1 even after it has been resumed on
            // some other thread.
            // So: flowDepth != 0 means "the one inside is us" -> legal nesting, carry on.
            //     flowDepth == 0 means "the one inside is a stranger" -> throw.
            //
            // refCount is ALSO required, and the reason is easy to state wrongly. It is NOT that an
            // AsyncLocal is lost when the flow moves to another thread -- it survives that perfectly, and
            // if it did not, flowDepth could not work at all. The real rule is narrower:
            //
            //      a write to an AsyncLocal made INSIDE an async method is not visible to that method's
            //      CALLER after it returns.
            //
            // So when a nested async call decrements flowDepth on its way out, the caller never sees the
            // decrement -- the caller's own copy still holds the old number. Releasing cannot be driven by
            // a value that can go stale like that. refCount is an ordinary field on the object, so it
            // cannot. ExitCriticalSection uses it to clear inCriticalSection on the OUTERMOST exit.
            // (Not "the last flow to leave" -- there is only ever one flow inside; the nesting is that one
            // flow calling in again.)
            //
            // You might think: why not make flowDepth a plain 0/1 flag and let refCount decide when to
            // clear it? That would still work today. It is rejected for robustness, not correctness: the
            // flag would have to be cleared inside the `if (--refCount == 0)` branch of
            // ExitCriticalSection, which breaks symmetric stack unwinding -- the rule that the exit path
            // should undo exactly what the entry path did, step for step, unconditionally. Entry writes
            // flowDepth every time, so exit should too. Counting keeps each field independently correct
            // and merely agreeing with the other, instead of making flowDepth's correctness depend on
            // refCount's. (It is also what EF Core's own detector does.)


            if (Interlocked.CompareExchange(ref this.inCriticalSection, 1, 0) == 1      // left condition
                && 
                this.flowDepth.Value == 0)                                           // right condition
            {
                // Held, and not by us.
                throw new InvalidOperationException(
                    "A second operation was started on this IDbCommunication instance before a previous " +
                    "operation completed. This is usually caused by two threads, or two tasks that were " +
                    "not awaited one after the other, using the same instance concurrently. An instance " +
                    "holds a single connection and a single transaction, so it can only run one operation " +
                    "at a time: await each call before starting the next, or give each concurrent flow its " +
                    "own instance.");
            }

            this.flowDepth.Value++;
            this.refCount++;
            return new ConcurrencyDetectorCriticalSection(this);
        }

        /// <summary>
        ///     Ends one operation, releasing the section once the outermost one ends.
        /// </summary>
        internal void ExitCriticalSection()
        {
            this.flowDepth.Value--;
            if (--this.refCount == 0)
            {
                // Written last, and with a barrier. The barrier is about ORDERING, not speed: whoever
                // later sees this 0 -- through the interlocked read at the top of EnterCriticalSection --
                // is guaranteed to also see everything written before it, refCount == 0 above all. Without
                // it the CPU or the JIT could reorder those writes, and the next flow in could take the
                // section and then read the previous flow's leftover count.
                Volatile.Write(ref this.inCriticalSection, 0);
            }
        }
    }

    /// <summary>
    ///     One operation in progress, as returned by
    ///     <see cref="ConcurrencyDetector.EnterCriticalSection"/>. A struct, and handed straight to a
    ///     <c>using</c> statement, so guarding an operation allocates nothing. Its default value guards
    ///     nothing, which is what an instance with the checks turned off hands back.
    /// </summary>
    public readonly struct ConcurrencyDetectorCriticalSection : IDisposable
    {
        private readonly ConcurrencyDetector detector;

        internal ConcurrencyDetectorCriticalSection(ConcurrencyDetector detector)
        {
            this.detector = detector;
        }

        /// <summary>Ends the operation this value marked the start of.</summary>
        public void Dispose()
        {
            this.detector?.ExitCriticalSection();
        }
    }
}
