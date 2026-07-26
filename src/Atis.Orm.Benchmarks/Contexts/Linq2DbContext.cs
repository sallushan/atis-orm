using Atis.Orm.Benchmarks.Data;
using Atis.Orm.Benchmarks.Model;
using LinqToDB;
using LinqToDB.Data;

namespace Atis.Orm.Benchmarks.Contexts
{
    /// <summary>
    /// linq2db context over the shared benchmark database, built once per benchmark class rather
    /// than per invocation — matching how Dapper's suite treats linq2db. Mapping comes from the
    /// <c>LinqToDB.Mapping</c> attributes on the entities.
    /// </summary>
    public class Linq2DbContext : DataConnection
    {
        public Linq2DbContext() : this(BenchmarkDatabase.ConnectionString)
        {
        }

        public Linq2DbContext(string connectionString)
            : base(new DataOptions().UseSqlServer(connectionString))
        {
        }

        public ITable<Post> Posts => this.GetTable<Post>();
        public ITable<Employee> Employees => this.GetTable<Employee>();
    }
}
