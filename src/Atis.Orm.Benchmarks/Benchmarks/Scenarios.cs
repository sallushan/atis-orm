namespace Atis.Orm.Benchmarks.Benchmarks
{
    /// <summary>
    /// Scenario names. These double as BenchmarkDotNet categories, which is what
    /// <see cref="Config"/> groups baselines and ratios by — so every class running the same
    /// workload must use the same constant, and no two workloads may share one.
    ///
    /// Filter a run with e.g. <c>-- --anyCategories ByPk</c>.
    /// </summary>
    public static class Scenarios
    {
        /// <summary>One full entity fetched by primary key — the workload Dapper's suite benchmarks.</summary>
        public const string ByPk = "ByPk";

        /// <summary>Top-100 ordered list projected into a DTO.</summary>
        public const string TopN = "TopN";
    }
}
