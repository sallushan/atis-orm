using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Atis.Orm;
using Atis.Orm.Benchmarks.Contexts;
using Atis.Orm.Benchmarks.Data;
using Atis.Orm.Benchmarks.Model;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Dapper;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Atis.Orm.Benchmarks.Benchmarks
{
    /// <summary>
    /// End-to-end top-N query benchmark: "the 100 highest-paid active employees in a department
    /// earning above a threshold", projected into a shared <see cref="EmployeeDto"/>. Each ORM runs
    /// the same logical query and materializes the same DTO shape, so the number reflects
    /// translate + execute + hydrate.
    ///
    /// A fresh context/connection is opened per invocation (realistic per-request usage), so results
    /// include connection acquisition just as a real app would incur it.
    /// </summary>
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class QueryBenchmarks
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

        [GlobalSetup]
        public void Setup() => BenchmarkDatabase.EnsureSeeded();

        [Benchmark(Baseline = true)]
        public async Task<List<EmployeeDto>> RawAdoNet()
        {
            var result = new List<EmployeeDto>();
            using var conn = new SqlConnection(BenchmarkDatabase.ConnectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(Sql, conn);
            cmd.Parameters.AddWithValue("@dept", Dept);
            cmd.Parameters.AddWithValue("@minSalary", MinSalary);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
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

        [Benchmark]
        public async Task<List<EmployeeDto>> Dapper_()
        {
            using var conn = new SqlConnection(BenchmarkDatabase.ConnectionString);
            var rows = await conn.QueryAsync<EmployeeDto>(Sql, new { dept = Dept, minSalary = MinSalary });
            return rows.AsList();
        }

        [Benchmark]
        public async Task<List<EmployeeDto>> EfCore()
        {
            using var db = new EfCoreContext();
            var query = db.Employees
                .AsNoTracking()
                .Where(e => e.DepartmentId == Dept && e.IsActive && e.Salary > MinSalary)
                .OrderByDescending(e => e.Salary)
                .Take(TopN)
                .Select(e => new EmployeeDto { EmployeeId = e.EmployeeId, FirstName = e.FirstName, LastName = e.LastName, Salary = e.Salary });
            // Fully qualified: the benchmark lives under the Atis.Orm namespace, whose ToListAsync
            // extension would otherwise shadow EF Core's via enclosing-namespace resolution.
            return await EntityFrameworkQueryableExtensions.ToListAsync(query);
        }

        [Benchmark]
        public async Task<List<EmployeeDto>> Linq2Db()
        {
            using var db = new DataConnection(new DataOptions().UseSqlServer(BenchmarkDatabase.ConnectionString));
            var query = db.GetTable<Employee>()
                .Where(e => e.DepartmentId == Dept && e.IsActive && e.Salary > MinSalary)
                .OrderByDescending(e => e.Salary)
                .Take(TopN)
                .Select(e => new EmployeeDto { EmployeeId = e.EmployeeId, FirstName = e.FirstName, LastName = e.LastName, Salary = e.Salary });
            // Fully qualified for the same enclosing-namespace reason (see EfCore above).
            return await LinqToDB.AsyncExtensions.ToListAsync(query);
        }

        [Benchmark]
        public async Task<List<EmployeeDto>> Atis()
        {
            using var db = new AtisDataContext();
            var query = db.Employees
                .Where(e => e.DepartmentId == Dept && e.IsActive && e.Salary > MinSalary)
                .OrderByDescending(e => e.Salary)
                .Take(TopN)
                // Atis's execution path needs a projection for a top-level ORDER BY to emit valid SQL
                // (a full-entity ordered result currently generates "Incorrect syntax near 'ORDER'").
                .Select(e => new EmployeeDto { EmployeeId = e.EmployeeId, FirstName = e.FirstName, LastName = e.LastName, Salary = e.Salary });
            return await OrmQueryExtensions.ToListAsync(query);
        }
    }
}
