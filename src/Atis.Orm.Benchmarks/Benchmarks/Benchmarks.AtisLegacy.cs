using System.ComponentModel;
using Atis.Orm.Benchmarks.Contexts;
using Atis.Orm.Benchmarks.Model;
using BenchmarkDotNet.Attributes;
// Imported unaliased so Where/FirstOrDefault below can be written in extension-method form, the way
// legacy calling code actually reads. global:: is not decoration: this file's own namespace nests
// under Atis, and the target namespace differs from Atis.Orm only by casing.
using global::Atis.ORM;

namespace Atis.Orm.Benchmarks.Benchmarks
{
    /// <summary>
    /// The previous-generation Atis engine (Atis.ORM 9.16.4) running the same by-primary-key fetch as
    /// every other contender, so the rewrite can be read against what it replaced in one summary.
    ///
    /// <para>The query is <c>Where(predicate).FirstOrDefault()</c> rather than <c>First(predicate)</c>:
    /// the legacy API has no <c>First</c>, and its <c>FirstOrDefault</c> is the same shape — it appends
    /// <c>Top(1)</c> and takes the first row, which is what the current engine's
    /// <c>FirstOrDefaultQueryMethodExpressionConverter</c> emits too. Note these are Atis.ORM's own
    /// extension methods over <c>IQuery&lt;T&gt;</c>, not LINQ's over <c>IQueryable&lt;T&gt;</c>.</para>
    ///
    /// <para>The captured <c>i</c> becomes a SQL parameter here as well (the legacy translator hoists
    /// evaluated constants into its <c>DbParameterList</c>), so both engines are measured on the same
    /// parameterized query. What differs is what happens before that: the legacy engine has no
    /// compiled-query cache, so <c>QueryTranslator.Translate</c> runs on <em>every</em> call, where
    /// the current engine's steady state is a cache hit plus parameter rebinding. That gap is the
    /// point of this row.</para>
    /// </summary>
    [Description("Atis (legacy 9.16.4)")]
    [BenchmarkCategory(Scenarios.ByPk)]
    public class AtisLegacyBenchmarks : BenchmarkBase
    {
        private LegacyAtisDataContext _shared;
        private LegacyAtisDataContext _owned;

        [GlobalSetup]
        public void Setup()
        {
            // Still called even though the legacy engine cannot use _connection: it is what resets
            // the shared id counter, and keeping every benchmark class on one setup path means the
            // legacy rows are produced under the same harness rules as the rest.
            BaseSetup();
            _shared = LegacyAtisDataContext.WithSharedConnection();
            _owned = LegacyAtisDataContext.WithOwnConnection();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _shared?.Dispose();
            _owned?.Dispose();
            BaseCleanup();
        }

        /// <summary>
        /// Reuses one open connection — the row to compare against the current engine's "First",
        /// Dapper and the hand-coded baseline.
        /// </summary>
        [Benchmark(Description = "First")]
        public LegacyPost First()
        {
            Step();
            return _shared.Posts.Where(p => p.Id == i).FirstOrDefault();
        }

        /// <summary>Acquires a pooled connection per query, like EF Core and the current engine's
        /// own-connection row, so the connection cost stays visible rather than averaged in.</summary>
        [Benchmark(Description = "First (Own Connection)")]
        public LegacyPost FirstOwnConnection()
        {
            Step();
            return _owned.Posts.Where(p => p.Id == i).FirstOrDefault();
        }
    }
}
