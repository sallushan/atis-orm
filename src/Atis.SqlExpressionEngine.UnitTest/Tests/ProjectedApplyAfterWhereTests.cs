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

        // The parent has an OrderBy and no Take. The apply wraps it, and SQL Server does not allow an ORDER BY
        // inside a derived table without TOP, so the engine refuses the query instead of sending that SQL.
        [TestMethod]
        public void Entity_FirstOrDefault_apply_after_Where_and_OrderBy_is_refused()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();

            var query = db.CreateQuery<TestEntities.Employee>()
                          .Where(e => e.LastName == "ProjectedApply4")
                          .OrderBy(e => e.EmployeeId)
                          .Select(p => new
                          {
                              p.EmployeeId,
                              Latest = skills.Where(s => s.EmployeeId == p.EmployeeId)
                                             .OrderByDescending(s => s.CertifiedDate)
                                             .FirstOrDefault()
                          });

            var ex = Assert.ThrowsException<NotSupportedException>(() => query.ToList());
            StringAssert.Contains(ex.Message, "OrderBy");
        }

        [TestMethod]
        public void Entity_FirstOrDefault_apply_with_OrderBy_after_the_Select_runs()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();

            var rows = db.CreateQuery<TestEntities.Employee>()
                         .Where(e => e.LastName == "ProjectedApply5")
                         .Select(p => new
                         {
                             p.EmployeeId,
                             Latest = skills.Where(s => s.EmployeeId == p.EmployeeId)
                                            .OrderByDescending(s => s.CertifiedDate)
                                            .FirstOrDefault()
                         })
                         .OrderBy(r => r.EmployeeId)
                         .ToList();

            Assert.AreEqual(0, rows.Count);
        }
    }
}
