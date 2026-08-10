using System;
using System.Data.SqlClient;
using Atis.Orm.Benchmarks.Data;
using Atis.Orm.Benchmarks.Model;
using Legacy = global::Atis.ORM;

namespace Atis.Orm.Benchmarks.Contexts
{
    /// <summary>
    /// The legacy Atis.ORM 9.16.4 equivalent of <see cref="AtisDataContext"/>, and the contender the
    /// current engine is measured against.
    ///
    /// <para>Idiomatic legacy usage, unchanged: a <c>DbContext</c> subclass whose
    /// <c>DataEntity&lt;T,K&gt;</c> properties are populated by reflection in the base constructor, and
    /// mapping expressed as attributes on the entity (<see cref="LegacyPost"/>) rather than fluently.
    /// There is no model-building step and no compiled-query cache — <c>SqlDataLibrary</c> runs
    /// <c>QueryTranslator.Translate</c> on every execution, which is exactly the difference the ByPk
    /// benchmark is there to price.</para>
    ///
    /// <para><b>ADO.NET stack.</b> This is the one place the legacy contender cannot be held to the
    /// suite's usual "every contender shares one connection" rule. <c>SqlDataLibrary</c> creates
    /// <c>System.Data.SqlClient</c> commands directly and assigns the connection to them, so handing
    /// it the <c>Microsoft.Data.SqlClient</c> connection the other contenders share throws on the
    /// first query. It therefore gets its own System.Data.SqlClient connection to the same database
    /// (<see cref="BenchmarkDatabase.LegacyConnectionString"/>), opened once in setup and reused —
    /// the same *arrangement*, over a different provider. Treat provider-level differences (roughly a
    /// few microseconds on this workload) as part of the legacy number.</para>
    /// </summary>
    public class LegacyAtisDataContext : Legacy.DbContext, IDisposable
    {
        private readonly SqlConnection _ownedConnection;

        /// <summary>
        /// Reuses one long-lived connection, matching how <see cref="AtisDataContext"/>, Dapper and
        /// the hand-coded baseline are set up. The connection is opened here rather than lazily
        /// because <c>DataAccessLibrary.PrepareCommandForExecution</c> closes any connection it had to
        /// open itself — which would silently turn this into a per-query open/close.
        /// </summary>
        public static LegacyAtisDataContext WithSharedConnection()
        {
            var connection = new SqlConnection(BenchmarkDatabase.LegacyConnectionString);
            connection.Open();
            return new LegacyAtisDataContext(new Legacy.SqlDataLibrary(connection), connection);
        }

        /// <summary>
        /// Acquires a pooled connection per query, matching <c>AtisDataContext</c>'s own-connection
        /// mode and EF Core. Passing a connection string rather than a connection is what selects
        /// this path inside <c>DataAccessLibrary</c>.
        /// </summary>
        public static LegacyAtisDataContext WithOwnConnection()
            => new LegacyAtisDataContext(new Legacy.SqlDataLibrary(BenchmarkDatabase.LegacyConnectionString), null);

        private LegacyAtisDataContext(Legacy.DataAccessLibrary dataAccessLibrary, SqlConnection ownedConnection)
            : base(dataAccessLibrary)
        {
            _ownedConnection = ownedConnection;
        }

        /// <summary>
        /// Query root for the ByPk scenario. The setter is required, not stylistic:
        /// <c>DbContextBase.InitializeDataEntities</c> reflects over the derived type's properties and
        /// skips any without one, leaving the property null.
        /// </summary>
        public Legacy.DataEntity<LegacyPost, int> Posts { get; set; }

        /// <summary>Query root for the TopN scenario.</summary>
        public Legacy.DataEntity<LegacyEmployee, int> Employees { get; set; }

        public void Dispose()
        {
            _ownedConnection?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
