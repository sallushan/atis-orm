using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Order;
using Atis.Orm.Benchmarks.Helpers;

namespace Atis.Orm.Benchmarks
{
    /// <summary>
    /// Mirrors the configuration of Dapper's own benchmark suite
    /// (DapperLib/Dapper, benchmarks/Dapper.Tests.Performance/Config.cs) so results from this
    /// project can be read against Dapper's published table: same job, same columns, same
    /// fastest-to-slowest ordering, all ORMs joined into one summary.
    /// </summary>
    public class Config : ManualConfig
    {
        /// <summary>
        /// Unroll factor — BenchmarkDotNet calls each benchmark method this many times per
        /// measured operation. Matches Dapper's suite; combined with <c>BenchmarkBase.Step()</c>
        /// it means each operation walks 500 different post ids.
        /// </summary>
        public const int Iterations = 500;

        public Config()
        {
            AddLogger(ConsoleLogger.Default);

            AddExporter(CsvExporter.Default);
            AddExporter(MarkdownExporter.GitHub);
            AddExporter(HtmlExporter.Default);

            AddDiagnoser(MemoryDiagnoser.Default);
            AddColumn(new ORMColumn());
            AddColumn(TargetMethodColumn.Method);
            AddColumn(new ReturnColumn());
            // Two scenarios share one joined summary, so the scenario has to be visible per row.
            AddColumn(CategoriesColumn.Default);
            AddColumn(StatisticColumn.Mean);
            AddColumn(StatisticColumn.StdDev);
            AddColumn(StatisticColumn.Error);
            AddColumn(BaselineRatioColumn.RatioMean);
            AddColumnProvider(DefaultColumnProviders.Metrics);

            AddJob(Job.ShortRun
                   // Dapper's suite uses LaunchCount(1). Three is needed here because this machine
                   // shows real between-process variance: a benchmark process that contends with
                   // SQL Server runs uniformly slow through its pilot, warmup and every iteration,
                   // so no amount of iterations inside that process recovers. Measured across five
                   // single-launch runs the hand-coded baseline landed at 45, 51, 52 and 78 us —
                   // and at 78 it ranked sixth, behind three ORMs, which is simply wrong. Pooling
                   // iterations across processes is what makes the ranking trustworthy.
                   .WithLaunchCount(3)
                   .WithWarmupCount(2)
                   .WithUnrollFactor(Iterations)
                   .WithIterationCount(10)
            );

            // Deviation from Dapper's config, and the reason the Ratio column is meaningful here:
            // each ORM lives in its own class, so BDN's default per-type grouping would compare a
            // baseline only against its own class's methods. Grouping by category instead puts every
            // ORM running the same scenario into one logical group with one shared baseline
            // (the hand-coded SqlCommand), which is the comparison this suite exists to make.
            // ByJob has to be spelled out alongside it: naming any rule replaces the defaults, and
            // without it a run that adds a second job (e.g. `--job Dry`) merges both jobs into one
            // group with two baselines, and the ratios stop meaning anything.
            AddLogicalGroupRules(BenchmarkLogicalGroupRule.ByCategory, BenchmarkLogicalGroupRule.ByJob);

            Orderer = new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest);
            Options |= ConfigOptions.JoinSummary;
        }
    }
}
