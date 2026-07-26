using System.ComponentModel;
using System.Linq;
using Atis.Orm.Benchmarks.Contexts;
using Atis.Orm.Benchmarks.Model;
using BenchmarkDotNet.Attributes;

namespace Atis.Orm.Benchmarks.Benchmarks
{
    /// <summary>
    /// Atis running the same by-primary-key fetch as every other contender.
    ///
    /// <c>First(predicate)</c> is a fully synchronous path: the engine's
    /// <c>FirstOrDefaultQueryMethodExpressionConverter</c> turns it into <c>WHERE … </c> plus
    /// <c>TOP 1</c>, and <c>DatabaseAdapter.Execute&lt;T&gt;</c> unwraps the non-enumerable result.
    ///
    /// The captured <c>i</c> becomes a SQL parameter rather than a literal, so after the first call
    /// this measures a compiled-query cache hit plus parameter rebinding — the steady state a real
    /// application sees, and what BenchmarkDotNet's warmup iterations establish before measuring.
    /// </summary>
    [Description("Atis")]
    [BenchmarkCategory(Scenarios.ByPk)]
    public class AtisBenchmarks : BenchmarkBase
    {
        private AtisDataContext _shared;
        private AtisDataContext _owned;

        [GlobalSetup]
        public void Setup()
        {
            BaseSetup();
            _shared = new AtisDataContext(_connection);
            _owned = new AtisDataContext();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _shared?.Dispose();
            _owned?.Dispose();
            BaseCleanup();
        }

        /// <summary>
        /// Reuses the one open connection, the same arrangement as Dapper and the hand-coded
        /// baseline. This is the row to compare against those two.
        /// </summary>
        [Benchmark(Description = "First")]
        public Post First()
        {
            Step();
            return _shared.Posts.First(p => p.Id == i);
        }

        /// <summary>
        /// Acquires a pooled connection per query, the same as EF Core. Reported separately so the
        /// connection cost is visible rather than hidden inside a single "Atis" number.
        /// </summary>
        [Benchmark(Description = "First (Own Connection)")]
        public Post FirstOwnConnection()
        {
            Step();
            return _owned.Posts.First(p => p.Id == i);
        }
    }
}
