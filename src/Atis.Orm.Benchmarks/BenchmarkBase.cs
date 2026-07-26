using Atis.Orm.Benchmarks.Data;
using BenchmarkDotNet.Attributes;
using Microsoft.Data.SqlClient;

namespace Atis.Orm.Benchmarks
{
    /// <summary>
    /// Shared harness, ported from Dapper's benchmark suite
    /// (DapperLib/Dapper, benchmarks/Dapper.Tests.Performance/Benchmarks.cs).
    ///
    /// Two things here are load-bearing for faithful numbers:
    /// <list type="bullet">
    /// <item>the connection is opened <em>once</em> in setup and reused, so measurements isolate
    /// translate + execute + hydrate instead of being swamped by connection acquisition;</item>
    /// <item><see cref="Step"/> advances the id on every call, so the parameter genuinely varies —
    /// exercising each ORM's compiled-query cache + parameter rebinding rather than letting a
    /// constant sit in one perfectly warm plan.</item>
    /// </list>
    ///
    /// Note there is deliberately no <c>[BenchmarkCategory]</c> here: categories are the logical
    /// grouping for baselines and ratios (see <see cref="Config"/>), so each concrete class declares
    /// its own scenario category.
    /// </summary>
    public abstract class BenchmarkBase
    {
        protected SqlConnection _connection;
        protected int i;

        /// <summary>
        /// The benchmark database, honouring the <c>ATIS_BENCH_SQL</c> environment variable.
        /// Dapper's suite reads this from <c>app.config</c>; we keep the existing env-var mechanism.
        /// </summary>
        public static string ConnectionString => BenchmarkDatabase.ConnectionString;

        protected void BaseSetup()
        {
            i = 0;
            _connection = new SqlConnection(ConnectionString);
            _connection.Open();
        }

        protected void BaseCleanup()
        {
            _connection?.Dispose();
            _connection = null;
        }

        /// <summary>Cycles through the seeded post ids 1..5000, exactly as Dapper's suite does.</summary>
        protected void Step()
        {
            i++;
            if (i > 5000) i = 1;
        }
    }
}
