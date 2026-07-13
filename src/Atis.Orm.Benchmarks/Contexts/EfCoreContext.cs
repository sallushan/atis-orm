using Atis.Orm.Benchmarks.Data;
using Atis.Orm.Benchmarks.Model;
using Microsoft.EntityFrameworkCore;

namespace Atis.Orm.Benchmarks.Contexts
{
    /// <summary>
    /// EF Core 8 context over the shared benchmark database. The closest peer to Atis:
    /// also a full LINQ provider that translates expression trees to SQL.
    /// </summary>
    public class EfCoreContext : DbContext
    {
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Department> Departments { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(BenchmarkDatabase.ConnectionString);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Employee>(e =>
            {
                e.ToTable("Employee", "dbo");
                e.HasKey(x => x.EmployeeId);
                e.Property(x => x.EmployeeId).ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<Department>(e =>
            {
                e.ToTable("Department", "dbo");
                e.HasKey(x => x.DepartmentId);
                e.Property(x => x.DepartmentId).ValueGeneratedOnAdd();
            });
        }
    }
}
