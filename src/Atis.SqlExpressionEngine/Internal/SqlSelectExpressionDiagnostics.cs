using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Atis.SqlExpressionEngine.Internal
{
    /// <summary>
    ///     What to do when a <see cref="SqlExpressions.SqlSelectExpression"/> finds a broken rule after an operation.
    /// </summary>
    internal enum SelectValidationMode
    {
        /// <summary>No checking. This is the default and what production uses.</summary>
        Off = 0,
        /// <summary>Remember the broken rules and carry on. Used to take a baseline.</summary>
        Record = 1,
        /// <summary>Remember the broken rules and throw.</summary>
        Throw = 2,
    }

    /// <summary>
    ///     Switch and log for the self-checks of <see cref="SqlExpressions.SqlSelectExpression"/>.
    ///     Only the unit test project turns this on (through InternalsVisibleTo).
    /// </summary>
    internal static class SqlSelectExpressionDiagnostics
    {
        internal static SelectValidationMode Mode = SelectValidationMode.Off;

        /// <summary>
        ///     Optional. Returns text that says where we are (for example the test method name), added to each entry.
        /// </summary>
        internal static Func<string> ContextProvider;

        private static readonly ConcurrentQueue<string> entries = new ConcurrentQueue<string>();

        internal static void Report(string rule, string message)
        {
            string context = null;
            try { context = ContextProvider?.Invoke(); } catch { /* diagnostics must never break a query */ }
            entries.Enqueue($"{rule} | {context ?? "(no context)"} | {message}");
        }

        internal static IReadOnlyList<string> Snapshot() => entries.ToArray();

        internal static void Clear()
        {
            while (entries.TryDequeue(out _)) { }
        }
    }
}
