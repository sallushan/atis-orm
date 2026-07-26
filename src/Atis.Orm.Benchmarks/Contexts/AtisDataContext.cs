using System.Data.Common;
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
    ///
    /// Every benchmark entity is registered here rather than being split across per-scenario
    /// context classes. That is deliberate: per docs/ServiceProviderCachingAndModelLifetime.md the
    /// root provider and <c>IOrmModel</c> are cached process-wide by configuration type + extension
    /// types, so two different <see cref="DataContext"/> subclasses sharing this configuration would
    /// share one model and only the first one's <see cref="OnModelCreating"/> would ever run —
    /// silently losing the other's mappings.
    /// </summary>
    public class AtisDataContext : DataContext
    {
        // Backing fields so each query root (DataSet) is built lazily on first access and reused,
        // rather than re-running CreateQuery<T>() (which touches the model) on every property read.
        // Nothing is constructed until a benchmark actually touches the property.
        private IQueryable<Employee> _employees;
        private IQueryable<Department> _departments;
        private IQueryable<Post> _posts;

        private readonly DbConnection _externalConnection;

        /// <summary>Opens and closes its own pooled connection per query, like EF Core does.</summary>
        public AtisDataContext()
        {
        }

        /// <summary>
        /// Runs against a connection the caller owns and keeps open. Atis opens it if needed but
        /// never disposes it (see <c>DbCommunicationBase</c>), which is the same arrangement Dapper's
        /// suite uses for Dapper and the hand-coded baseline.
        /// </summary>
        public AtisDataContext(DbConnection connection)
        {
            _externalConnection = connection;
        }

        protected override void OnConfiguring(DataContextConfiguration config)
        {
            // Must precede UseSqlServer: registration is first-wins, and the provider extension's
            // AddCoreServices() is what registers the console-writing default logger.
            config.AddOrUpdateExtension(new SilentLoggerExtension());

            if (_externalConnection != null)
                config.UseSqlServer(_externalConnection);
            else
                config.UseSqlServer(BenchmarkDatabase.ConnectionString);
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

            mb.Entity<Post>(e =>
            {
                e.ToTable("Posts", "dbo");
                e.HasKey(x => x.Id);
            });
        }

        /// <summary>Lazily created, cached query root for Employee — the "DataSet" for this entity.</summary>
        public IQueryable<Employee> Employees => _employees ??= CreateQuery<Employee>();

        /// <summary>Lazily created, cached query root for Department.</summary>
        public IQueryable<Department> Departments => _departments ??= CreateQuery<Department>();

        /// <summary>Lazily created, cached query root for Post — the by-primary-key scenario.</summary>
        public IQueryable<Post> Posts => _posts ??= CreateQuery<Post>();
    }
}
