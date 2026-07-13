using System.Linq;
using Atis.Orm;
using Atis.Orm.DataAccess;
using Atis.Orm.Metadata;
using Atis.Orm.SqlServer;
using Atis.Orm.Benchmarks.Data;
using Atis.Orm.Benchmarks.Model;

namespace Atis.Orm.Benchmarks.Contexts
{
    /// <summary>
    /// Atis <see cref="DataContext"/> pointed at the shared benchmark database.
    /// Column names match property names, so only table + key need declaring.
    /// </summary>
    public class AtisDataContext : DataContext
    {
        // Backing fields so each query root (DataSet) is built lazily on first access and reused,
        // rather than re-running CreateQuery<T>() (which touches the model) on every property read.
        // Nothing is constructed until a benchmark actually touches the property.
        private IQueryable<Employee> _employees;
        private IQueryable<Department> _departments;

        protected override void OnConfiguring(DataContextConfiguration config)
        {
            config.UseSqlServer(BenchmarkDatabase.AtisConnectionString);
        }

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<Employee>(e =>
            {
                e.ToTable("Employee", "dbo");
                e.HasKey(x => x.EmployeeId);
            });

            mb.Entity<Department>(e =>
            {
                e.ToTable("Department", "dbo");
                e.HasKey(x => x.DepartmentId);
            });
        }

        /// <summary>Lazily created, cached query root for Employee — the "DataSet" for this entity.</summary>
        public IQueryable<Employee> Employees => _employees ??= CreateQuery<Employee>();

        /// <summary>Lazily created, cached query root for Department.</summary>
        public IQueryable<Department> Departments => _departments ??= CreateQuery<Department>();
    }
}
