using System;
using Atis.Orm.Benchmarks.Data;
using BenchmarkDotNet.Running;
using static System.Console;

namespace Atis.Orm.Benchmarks
{
    public static class Program
    {
        public static void Main(string[] args)
        {
#if DEBUG
            WriteLineColor("Warning: DEBUG configuration; performance may be impacted!", ConsoleColor.Red);
            WriteLine();
#endif
            WriteLine("Atis ORM benchmark suite — a mirror of Dapper's own suite, so results can be");
            WriteLine("read against Dapper's published performance table directly.");
            WriteLine();

            if (args.Length == 0)
            {
                WriteLine("Optional arguments:");
                WriteColor("  (no args)", ConsoleColor.Blue);
                WriteLine(": run all benchmarks");
                WriteColor("  --anyCategories ByPk", ConsoleColor.Blue);
                WriteLine(": only the single-row-by-primary-key scenario (the Dapper-comparable one)");
                WriteColor("  --anyCategories TopN", ConsoleColor.Blue);
                WriteLine(": only the top-100 ordered projection scenario");
                WriteLine();
            }

            WriteLine("Using ConnectionString: " + BenchmarkBase.ConnectionString);

            // Seed once up front so the first benchmarked iteration doesn't pay DDL/bulk-load cost.
            WriteLine("Ensuring benchmark database is seeded...");
            BenchmarkDatabase.EnsureSeeded();       // Employee/Department — TopN scenario
            BenchmarkDatabase.EnsurePostsSeeded();  // Posts — ByPk scenario
            WriteLine("Database setup complete.");

            WriteLine("Iterations: " + Config.Iterations);
            WriteLine();

            // With no arguments BenchmarkSwitcher prompts for a selection on stdin, which hangs any
            // unattended run. "Everything" is the sensible default for a suite this small.
            if (args.Length == 0)
                args = new[] { "--filter", "*" };

            new BenchmarkSwitcher(typeof(BenchmarkBase).Assembly).Run(args, new Config());
        }

        private static void WriteLineColor(string message, ConsoleColor color)
        {
            var orig = ForegroundColor;
            ForegroundColor = color;
            WriteLine(message);
            ForegroundColor = orig;
        }

        private static void WriteColor(string message, ConsoleColor color)
        {
            var orig = ForegroundColor;
            ForegroundColor = color;
            Write(message);
            ForegroundColor = orig;
        }
    }
}
