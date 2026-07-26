using System.ComponentModel;
using System.Linq;
using Atis.Orm.Benchmarks.Model;
using BenchmarkDotNet.Attributes;
using Dapper;

namespace Atis.Orm.Benchmarks.Benchmarks
{
    /// <summary>
    /// Ported from Dapper's suite (benchmarks/Dapper.Tests.Performance/Benchmarks.Dapper.cs).
    /// The <c>dynamic</c> and Dapper.Contrib variants are omitted — neither has a counterpart in
    /// the other contenders, and Contrib is not referenced by this project.
    /// </summary>
    [Description("Dapper")]
    [BenchmarkCategory(Scenarios.ByPk)]
    public class DapperBenchmarks : BenchmarkBase
    {
        [GlobalSetup]
        public void Setup() => BaseSetup();

        [GlobalCleanup]
        public void Cleanup() => BaseCleanup();

        [Benchmark(Description = "Query<T> (buffered)")]
        public Post QueryBuffered()
        {
            Step();
            return _connection.Query<Post>("select * from Posts where Id = @Id", new { Id = i }, buffered: true).First();
        }

        [Benchmark(Description = "Query<T> (unbuffered)")]
        public Post QueryUnbuffered()
        {
            Step();
            return _connection.Query<Post>("select * from Posts where Id = @Id", new { Id = i }, buffered: false).First();
        }

        [Benchmark(Description = "QueryFirstOrDefault<T>")]
        public Post QueryFirstOrDefault()
        {
            Step();
            return _connection.QueryFirstOrDefault<Post>("select * from Posts where Id = @Id", new { Id = i });
        }
    }
}
