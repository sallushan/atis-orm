using System;
using System.ComponentModel;
using System.Linq;
using Atis.Orm.Benchmarks.Contexts;
using Atis.Orm.Benchmarks.Model;
using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;

namespace Atis.Orm.Benchmarks.Benchmarks
{
    /// <summary>
    /// Ported from Dapper's suite (benchmarks/Dapper.Tests.Performance/Benchmarks.EntityFrameworkCore.cs).
    /// The context is built once in setup and uses its own pooled connection per query, exactly as
    /// Dapper's suite has it.
    /// </summary>
    [Description("EF Core")]
    [BenchmarkCategory(Scenarios.ByPk)]
    public class EFCoreBenchmarks : BenchmarkBase
    {
        private EfCoreContext _context;

        private static readonly Func<EfCoreContext, int, Post> _compiledQuery =
            EF.CompileQuery((EfCoreContext ctx, int id) => ctx.Posts.First(p => p.Id == id));

        [GlobalSetup]
        public void Setup()
        {
            BaseSetup();
            _context = new EfCoreContext(ConnectionString);
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

        [Benchmark(Description = "First (No Tracking)")]
        public Post NoTracking()
        {
            Step();
            return _context.Posts.AsNoTracking().First(p => p.Id == i);
        }
    }
}
