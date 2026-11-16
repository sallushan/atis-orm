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

        [TestMethod]
        public void Two_entity_FirstOrDefault_applies_after_Where_run()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();

            var rows = db.CreateQuery<TestEntities.Employee>()
                         .Where(e => e.LastName == "ProjectedApply2")
                         .Select(p => new
                         {
                             p.EmployeeId,
                             Latest = skills.Where(s => s.EmployeeId == p.EmployeeId)
                                            .OrderByDescending(s => s.CertifiedDate)
                                            .FirstOrDefault(),
                             Earliest = skills.Where(s => s.EmployeeId == p.EmployeeId)
                                              .OrderBy(s => s.CertifiedDate)
                                              .FirstOrDefault()
                         })
                         .ToList();

            Assert.AreEqual(0, rows.Count);
        }

        [TestMethod]
        public void Entity_FirstOrDefault_apply_inside_a_nested_projection_after_Where_runs()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();

            var rows = db.CreateQuery<TestEntities.Employee>()
                         .Where(e => e.LastName == "ProjectedApply3")
                         .Select(p => new
                         {
                             p.EmployeeId,
                             Inner = new
                             {
                                 p.LastName,
                                 Latest = skills.Where(s => s.EmployeeId == p.EmployeeId)
                                                .OrderByDescending(s => s.CertifiedDate)
                                                .FirstOrDefault()
                             }
                         })
                         .ToList();

            Assert.AreEqual(0, rows.Count);
        }

        // KNOWN BUG, not caused by the wrap funnel (it fails the same way before it): when the apply wraps a
        // parent that has an OrderBy, WrapQuery keeps the ORDER BY inside the derived table without TOP,
        // which SQL Server rejects, and the ordering is lost. Remove [Ignore] when that is fixed.
        [Ignore("Known bug: ORDER BY of the wrapped parent stays inside the derived table without TOP.")]
        [TestMethod]
        public void Entity_FirstOrDefault_apply_after_Where_and_OrderBy_runs()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();

            var rows = db.CreateQuery<TestEntities.Employee>()
                         .Where(e => e.LastName == "ProjectedApply4")
                         .OrderBy(e => e.EmployeeId)
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
