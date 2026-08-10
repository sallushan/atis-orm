using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Atis.Orm.Benchmarks.Contexts;
using Atis.Orm.Benchmarks.Model;
using BenchmarkDotNet.Attributes;
using Dapper;
using LinqToDB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
// The legacy engine's IQuery<T> extension methods, so its row below reads fluently like the others.
// They never collide with System.Linq's or linq2db's: IQuery<T> is neither IEnumerable nor IQueryable.
// global:: is required — this file's namespace nests under Atis, and Atis.ORM differs from Atis.Orm
// only by casing.
using global::Atis.ORM;

namespace Atis.Orm.Benchmarks.Benchmarks
{
    /// <summary>
    /// Second scenario, kept alongside the Dapper-shaped by-primary-key benchmarks: "the 100
    /// highest-paid active employees in a department earning above a threshold", projected into a
    /// shared <see cref="EmployeeDto"/>. It exercises what a single-row fetch cannot — ordering,
    /// <c>TOP</c>, and a member-init projection — which is where Atis's translation work actually
    /// shows up.
    ///
    /// It runs on the same harness as the by-primary-key scenario (one connection and one context
    /// per ORM, opened once in setup; synchronous throughout), so the two sets of numbers are
    /// measured the same way even though they are not comparable to each other. The category keeps
    /// them in separate baseline groups — see <see cref="Config"/>.
    ///
    /// Unlike the by-primary-key scenario, the filter values are constant: this measures a single
    /// fixed query shape, not parameter variation.
    /// </summary>
    [Description("Top-100 projection")]
    [BenchmarkCategory(Scenarios.TopN)]
    // Raises the config's LaunchCount(1) to 5 for this scenario only ([ProcessCount] is BDN's
    // job mutator for LaunchCount). Its variance is *between* processes, not within them: the query
    // sorts ~860 rows server-side (the seeded index covers (DepartmentId, IsActive) but not Salary),
    // so a benchmark process that happens to contend with SQL Server runs uniformly ~2x slow —
    // pilot, warmup and all ten iterations together. More iterations cannot average that out;
    // more launches can.
    [ProcessCount(5)]
    public class TopNBenchmarks : BenchmarkBase
    {
        private const int Dept = 1;
        private const decimal MinSalary = 60000m;
        private const int TopN = 100;

        private static readonly string Sql =
            $"SELECT TOP ({TopN}) " +
            "[EmployeeId],[FirstName],[LastName],[Salary] " +
            "FROM [dbo].[Employee] " +
            "WHERE [DepartmentId] = @dept AND [IsActive] = 1 AND [Salary] > @minSalary " +
            "ORDER BY [Salary] DESC";

        private EfCoreContext _efCore;
        private Linq2DbContext _linq2Db;
        private AtisDataContext _atis;
        private LegacyAtisDataContext _atisLegacy;

        [GlobalSetup]
        public void Setup()
        {
            BaseSetup();
            _efCore = new EfCoreContext(ConnectionString);
            _linq2Db = new Linq2DbContext(ConnectionString);
            _atis = new AtisDataContext(_connection);
            _atisLegacy = LegacyAtisDataContext.WithSharedConnection();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _efCore?.Dispose();
            _linq2Db?.Dispose();
            _atis?.Dispose();
            _atisLegacy?.Dispose();
            BaseCleanup();
        }

        [Benchmark(Description = "Raw ADO.NET", Baseline = true)]
        public List<EmployeeDto> RawAdoNet()
        {
            var result = new List<EmployeeDto>();
            using var cmd = new SqlCommand(Sql, _connection);
            cmd.Parameters.AddWithValue("@dept", Dept);
            cmd.Parameters.AddWithValue("@minSalary", MinSalary);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new EmployeeDto
                {
                    EmployeeId = reader.GetInt32(0),
                    FirstName = reader.GetString(1),
                    LastName = reader.GetString(2),
                    Salary = reader.GetDecimal(3),
                });
            }
            return result;
        }

        [Benchmark(Description = "Dapper Query<T>")]
        public List<EmployeeDto> Dapper_()
            => _connection.Query<EmployeeDto>(Sql, new { dept = Dept, minSalary = MinSalary }).AsList();

        [Benchmark(Description = "EF Core (No Tracking)")]
        public List<EmployeeDto> EfCore()
            => _efCore.Employees
                .AsNoTracking()
                .Where(e => e.DepartmentId == Dept && e.IsActive && e.Salary > MinSalary)
                .OrderByDescending(e => e.Salary)
                .Take(TopN)
                .Select(e => new EmployeeDto { EmployeeId = e.EmployeeId, FirstName = e.FirstName, LastName = e.LastName, Salary = e.Salary })
                .ToList();

        [Benchmark(Description = "LINQ to DB")]
        public List<EmployeeDto> Linq2Db()
            => _linq2Db.Employees
                .Where(e => e.DepartmentId == Dept && e.IsActive && e.Salary > MinSalary)
                .OrderByDescending(e => e.Salary)
                .Take(TopN)
                .Select(e => new EmployeeDto { EmployeeId = e.EmployeeId, FirstName = e.FirstName, LastName = e.LastName, Salary = e.Salary })
                .ToList();

        [Benchmark(Description = "Atis")]
        public List<EmployeeDto> Atis()
            => _atis.Employees
                .Where(e => e.DepartmentId == Dept && e.IsActive && e.Salary > MinSalary)
                .OrderByDescending(e => e.Salary)
                .Take(TopN)
                // Atis's execution path needs a projection for a top-level ORDER BY to emit valid SQL
                // (a full-entity ordered result currently generates "Incorrect syntax near 'ORDER'").
                // The projection must be member-init; a constructor call fails with
                // "Members of the new expression are not set". All contenders use the same shape,
                // so the comparison stays fair.
                .Select(e => new EmployeeDto { EmployeeId = e.EmployeeId, FirstName = e.FirstName, LastName = e.LastName, Salary = e.Salary })
                .ToList();

        /// <summary>
        /// The same query on the previous-generation engine. Written with Atis.ORM's own
        /// <c>IQuery&lt;T&gt;</c> extension methods — hence <c>OrderByDesc</c> and <c>Top</c> where the
        /// LINQ providers use <c>OrderByDescending</c> and <c>Take</c>, and the call-order convention
        /// that <c>Top</c> precedes <c>Select</c>. The emitted SQL is the same TOP-100 ordered
        /// projection, and the DTO is the shared one, so the row is directly comparable.
        /// </summary>
        [Benchmark(Description = "Atis (legacy 9.16.4)")]
        public List<EmployeeDto> AtisLegacy()
            => _atisLegacy.Employees
                .Where(e => e.DepartmentId == Dept && e.IsActive && e.Salary > MinSalary)
                .OrderByDesc(e => e.Salary)
                .Top(TopN)
                .Select(e => new EmployeeDto { EmployeeId = e.EmployeeId, FirstName = e.FirstName, LastName = e.LastName, Salary = e.Salary })
                .ToList();
    }
}
