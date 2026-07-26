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
        private readonly string _connectionString;

        public EfCoreContext() : this(BenchmarkDatabase.ConnectionString)
        {
        }

        /// <summary>Mirrors Dapper's suite, which builds its EF Core context from the connection string once in setup.</summary>
        public EfCoreContext(string connectionString)
        {
            _connectionString = connectionString;
        }

        public DbSet<Employee> Employees { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Post> Posts { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(_connectionString);
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

            modelBuilder.Entity<Post>(e =>
            {
                e.ToTable("Posts", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedOnAdd();
            });
        }
    }
}
