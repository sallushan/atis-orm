using System;
using System.ComponentModel;
using System.Linq;
using Atis.Orm.Benchmarks.Contexts;
using Atis.Orm.Benchmarks.Model;
using BenchmarkDotNet.Attributes;
using LinqToDB;

namespace Atis.Orm.Benchmarks.Benchmarks
{
    /// <summary>
    /// Ported from Dapper's suite (benchmarks/Dapper.Tests.Performance/Benchmarks.Linq2DB.cs).
    /// Named "LinqToDB" rather than "Linq2DB" for the same reason Dapper's suite is — the digit
    /// confuses BenchmarkDotNet's command-line filter.
    /// </summary>
    [Description("LINQ to DB")]
    [BenchmarkCategory(Scenarios.ByPk)]
    public class LinqToDBBenchmarks : BenchmarkBase
    {
        private Linq2DbContext _context;

        private static readonly Func<Linq2DbContext, int, Post> _compiledQuery =
            CompiledQuery.Compile((Linq2DbContext db, int id) => db.Posts.First(p => p.Id == id));

        [GlobalSetup]
        public void Setup()
        {
            BaseSetup();
            _context = new Linq2DbContext(ConnectionString);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _context?.Dispose();
            BaseCleanup();
        }

        [Benchmark(Description = "First")]
        public Post First()
        {
            Step();
            return _context.Posts.First(p => p.Id == i);
        }

        [Benchmark(Description = "First (Compiled)")]
        public Post Compiled()
        {
            Step();
            return _compiledQuery(_context, i);
        }
    }
}
