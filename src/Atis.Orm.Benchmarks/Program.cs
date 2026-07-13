using System;
using System.Reflection;
using Atis.Orm.Benchmarks.Data;
using BenchmarkDotNet.Running;

namespace Atis.Orm.Benchmarks
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            // Seed once up front so the first benchmarked iteration doesn't pay DDL/bulk-load cost.
            Console.WriteLine("Ensuring benchmark database is seeded...");
            BenchmarkDatabase.EnsureSeeded();
            Console.WriteLine("Ready. Launching BenchmarkDotNet.\n");

            BenchmarkSwitcher
                .FromAssembly(Assembly.GetExecutingAssembly())
                .Run(args);
        }
    }
}
