using Atis.Orm;
using System.Linq;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     An entity <c>FirstOrDefault()</c> on a correlated sub-query inside a projection becomes an OUTER APPLY.
    ///     When the parent query already has a Where, adding that apply wraps the parent in a derived table.
    /// </summary>
    [TestClass]
    public class ProjectedApplyAfterWhereTests
    {
        private const string ServerConnectionString =
            "Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True";

        [TestInitialize]
        public void EnsureDatabase() => new TestDatabaseSetup(ServerConnectionString).Setup();

        [TestMethod]
        public void Where_then_Select_with_entity_FirstOrDefault_runs()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();

            // No rows are needed: SQL Server rejects the SQL while compiling it, even for empty tables.
            var rows = db.CreateQuery<TestEntities.Employee>()
                         .Where(e => e.LastName == "ProjectedApply1")
                         .Select(p => new
                         {
                             p.EmployeeId,
                             Latest = skills.Where(s => s.EmployeeId == p.EmployeeId)
                                            .OrderByDescending(s => s.CertifiedDate)
                                            .FirstOrDefault()
                         })
                         .ToList();

            Assert.AreEqual(0, rows.Count);
        }
    }
}
