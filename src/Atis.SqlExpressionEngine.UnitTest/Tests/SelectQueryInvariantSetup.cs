using Atis.SqlExpressionEngine.Internal;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     Switches on the self-checks of <c>SqlSelectExpression</c> for every test in this assembly.
    ///     See docs/SqlSelectExpressionRedesign.md, section 7.
    ///     Mode comes from the environment variable ATIS_SELECT_VALIDATION: "off", "record" or "throw" (default).
    ///     The log is written to select-invariants.txt next to the test assembly.
    /// </summary>
    [TestClass]
    public static class SelectQueryInvariantSetup
    {
        [AssemblyInitialize]
        public static void Initialize(TestContext _)
        {
            var setting = Environment.GetEnvironmentVariable("ATIS_SELECT_VALIDATION")?.Trim().ToLowerInvariant();
            SqlSelectExpressionDiagnostics.Mode = setting switch
            {
                "off" => SelectValidationMode.Off,
                "record" => SelectValidationMode.Record,
                _ => SelectValidationMode.Throw,
            };
            SqlSelectExpressionDiagnostics.ContextProvider = FindTestMethodName;
            SqlSelectExpressionDiagnostics.Clear();
        }

        [AssemblyCleanup]
        public static void Cleanup()
        {
            var entries = SqlSelectExpressionDiagnostics.Snapshot();
            var path = Path.Combine(AppContext.BaseDirectory, "select-invariants.txt");
            File.WriteAllLines(path, entries.OrderBy(x => x, StringComparer.Ordinal));
            Console.WriteLine($"SqlSelectExpression self-checks: {entries.Count} entries, written to {path}");
        }

        private static string FindTestMethodName()
        {
            foreach (var frame in new StackTrace(false).GetFrames() ?? Array.Empty<StackFrame>())
            {
                var method = frame.GetMethod();
                if (method != null && method.GetCustomAttribute<TestMethodAttribute>() != null)
                    return $"{method.DeclaringType?.Name}.{method.Name}";
            }
            return null;
        }
    }
}
