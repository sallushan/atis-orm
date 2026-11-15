using Atis.Orm;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     Child collections under paging, against a real SQL Server. The shape follows a legacy inquiry method that
    ///     loads a page of rows and then runs several side queries (serials, history lines) for exactly that page.
    ///     Every test seeds its own rows inside <c>TransactionWithRollback</c> under a last name of its own
    ///     (<c>ChildPage1</c> ...).
    /// </summary>
    [TestClass]
    public class ChildCollectionPagingTests
    {
        private const string ServerConnectionString =
            "Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True";

        [TestInitialize]
        public void EnsureDatabase() => new TestDatabaseSetup(ServerConnectionString).Setup();

        private static int SeedEmployee(OrmDbContext db, string lastName, string firstName, decimal salary = 1m)
        {
            var rows = db.InsertEntity<TestEntities.Employee>()
                         .Value(x => x.FirstName, () => firstName)
                         .Value(x => x.LastName, () => lastName)
                         .Value(x => x.Email, () => firstName + "@" + lastName + ".test")
                         .Value(x => x.Salary, () => salary)
                         .Output(x => x.EmployeeId)
                         .ExecuteDictionary();
            return (int)rows[0]["EmployeeId"];
        }

        private static void SeedSkill(OrmDbContext db, int employeeId, string skillName)
        {
            db.InsertEntity<TestEntities.EmployeeSkill>()
              .Value(x => x.EmployeeId, () => employeeId)
              .Value(x => x.SkillName, () => skillName)
              .Value(x => x.ProficiencyLevel, () => 3)
              .Execute();
        }

        /// <summary>Ten employees A..J, employee at index i gets (i % 3) + 1 skills named "{name}-{n}".</summary>
        private static int[] SeedTen(OrmDbContext db, string lastName, decimal salary = 1m)
        {
            var ids = new int[10];
            for (int i = 0; i < 10; i++)
            {
                var name = ((char)('A' + i)).ToString();
                ids[i] = SeedEmployee(db, lastName, name, salary);
                for (int n = 1; n <= (i % 3) + 1; n++)
                    SeedSkill(db, ids[i], name + "-" + n);
            }
            return ids;
        }

        [TestMethod]
        public void Second_page_gets_only_its_own_rows_and_each_row_gets_its_own_children()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            db.TransactionWithRollback(() =>
            {
                var ids = SeedTen(db, "ChildPage1");

                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(e => e.LastName == "ChildPage1")
                             .OrderBy(e => e.FirstName)
                             .Paging(2, 4)
                             .Select(e => new
                             {
                                 e.FirstName,
                                 Skills = skills.Where(s => s.EmployeeId == e.EmployeeId).ToList(),
                                 SkillNames = skills.Where(s => s.EmployeeId == e.EmployeeId).Select(s => s.SkillName).ToList()
                             })
                             .ToList();

                CollectionAssert.AreEqual(new[] { "E", "F", "G", "H" }, rows.Select(x => x.FirstName).ToArray());
                for (int i = 0; i < rows.Count; i++)
                {
                    int index = 4 + i;
                    int expected = (index % 3) + 1;
                    Assert.AreEqual(expected, rows[i].Skills.Count, rows[i].FirstName);
                    Assert.IsTrue(rows[i].Skills.All(s => s.EmployeeId == ids[index]), "A child belongs to another parent.");
                    Assert.AreEqual(expected, rows[i].SkillNames.Count);
                    Assert.IsTrue(rows[i].SkillNames.All(n => n.StartsWith(rows[i].FirstName + "-")));
                }
            });
        }

        [TestMethod]
        public void Skip_and_Take_give_the_same_page_as_Paging()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            db.TransactionWithRollback(() =>
            {
                SeedTen(db, "ChildPage2");

                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(e => e.LastName == "ChildPage2")
                             .OrderByDescending(e => e.FirstName)
                             .Skip(3)
                             .Take(3)
                             .Select(e => new
                             {
                                 e.FirstName,
                                 Skills = skills.Where(s => s.EmployeeId == e.EmployeeId).ToList()
                             })
                             .ToList();

                CollectionAssert.AreEqual(new[] { "G", "F", "E" }, rows.Select(x => x.FirstName).ToArray());
                foreach (var row in rows)
                    Assert.IsTrue(row.Skills.All(s => s.SkillName.StartsWith(row.FirstName + "-")));
            });
        }

        [TestMethod]
        public void Ties_in_the_order_by_do_not_overlap_or_lose_children_between_pages()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            db.TransactionWithRollback(() =>
            {
                // every employee has the same salary, so ORDER BY Salary alone cannot tell them apart
                SeedTen(db, "ChildPage3", salary: 5m);

                var all = new List<string>();
                for (int page = 1; page <= 4; page++)
                {
                    var rows = db.CreateQuery<TestEntities.Employee>()
                                 .Where(e => e.LastName == "ChildPage3")
                                 .OrderBy(e => e.Salary)
                                 .Paging(page, 3)
                                 .Select(e => new
                                 {
                                     e.FirstName,
                                     Skills = skills.Where(s => s.EmployeeId == e.EmployeeId).ToList()
                                 })
                                 .ToList();
                    foreach (var row in rows)
                    {
                        Assert.IsTrue(row.Skills.Count > 0, "Parent " + row.FirstName + " lost its children on page " + page);
                        Assert.IsTrue(row.Skills.All(s => s.SkillName.StartsWith(row.FirstName + "-")));
                    }
                    all.AddRange(rows.Select(x => x.FirstName));
                }

                Assert.AreEqual(10, all.Count);
                Assert.AreEqual(10, all.Distinct().Count(), "A row appeared on two pages.");
            });
        }

        [TestMethod]
        public void A_page_past_the_end_is_empty()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            db.TransactionWithRollback(() =>
            {
                SeedTen(db, "ChildPage4");

                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(e => e.LastName == "ChildPage4")
                             .OrderBy(e => e.FirstName)
                             .Paging(5, 4)
                             .Select(e => new { e.FirstName, Skills = skills.Where(s => s.EmployeeId == e.EmployeeId).ToList() })
                             .ToList();

                Assert.AreEqual(0, rows.Count);
            });
        }

        [TestMethod]
        public void The_last_short_page_returns_the_remaining_rows()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            db.TransactionWithRollback(() =>
            {
                SeedTen(db, "ChildPage5");

                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(e => e.LastName == "ChildPage5")
                             .OrderBy(e => e.FirstName)
                             .Paging(3, 4)
                             .Select(e => new { e.FirstName, Skills = skills.Where(s => s.EmployeeId == e.EmployeeId).ToList() })
                             .ToList();

                CollectionAssert.AreEqual(new[] { "I", "J" }, rows.Select(x => x.FirstName).ToArray());
            });
        }

        [TestMethod]
        public void Same_query_run_for_different_pages_reuses_the_plan_and_still_returns_the_right_children()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            db.TransactionWithRollback(() =>
            {
                SeedTen(db, "ChildPage6");

                List<string> Page(int pageNumber) =>
                    db.CreateQuery<TestEntities.Employee>()
                      .Where(e => e.LastName == "ChildPage6")
                      .OrderBy(e => e.FirstName)
                      .Paging(pageNumber, 3)
                      .Select(e => new { e.FirstName, Skills = skills.Where(s => s.EmployeeId == e.EmployeeId).ToList() })
                      .ToList()
                      .Select(x => x.FirstName + ":" + x.Skills.Count)
                      .ToList();

                CollectionAssert.AreEqual(new[] { "A:1", "B:2", "C:3" }, Page(1));
                CollectionAssert.AreEqual(new[] { "D:1", "E:2", "F:3" }, Page(2));
                CollectionAssert.AreEqual(new[] { "G:1", "H:2", "I:3" }, Page(3));
                CollectionAssert.AreEqual(new[] { "J:1" }, Page(4));
            });
        }

        [TestMethod]
        public void Order_by_and_paging_written_after_the_select_that_holds_the_child_collection_work()
        {
            // the legacy method pages the finished query ("finalQuery.Paging(...)"); ordering and paging do not
            // wrap the query, so the placement rule lets them follow the Select
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            db.TransactionWithRollback(() =>
            {
                SeedTen(db, "ChildPage7");

                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(e => e.LastName == "ChildPage7")
                             .Select(e => new { e.FirstName, Skills = skills.Where(s => s.EmployeeId == e.EmployeeId).ToList() })
                             .OrderBy(x => x.FirstName)
                             .Paging(2, 3)
                             .ToList();

                CollectionAssert.AreEqual(new[] { "D", "E", "F" }, rows.Select(x => x.FirstName).ToArray());
                foreach (var row in rows)
                {
                    int index = row.FirstName[0] - 'A';
                    Assert.AreEqual((index % 3) + 1, row.Skills.Count, row.FirstName);
                    Assert.IsTrue(row.Skills.All(s => s.SkillName.StartsWith(row.FirstName + "-")));
                }
            });
        }

        [TestMethod]
        public void Order_by_and_take_after_the_select_with_ties_do_not_lose_or_mix_children()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            db.TransactionWithRollback(() =>
            {
                // every employee has the same salary, so ORDER BY Salary alone cannot tell them apart
                SeedTen(db, "ChildPage8", salary: 5m);

                var all = new List<string>();
                for (int page = 0; page < 4; page++)
                {
                    var rows = db.CreateQuery<TestEntities.Employee>()
                                 .Where(e => e.LastName == "ChildPage8")
                                 .Where(e => e.Salary > 0)
                                 .Select(e => new
                                 {
                                     e.FirstName,
                                     e.Salary,
                                     Skills = skills.Where(s => s.EmployeeId == e.EmployeeId).ToList()
                                 })
                                 .OrderBy(x => x.Salary)
                                 .Skip(page * 3)
                                 .Take(3)
                                 .ToList();
                    foreach (var row in rows)
                    {
                        Assert.IsTrue(row.Skills.Count > 0, "Parent " + row.FirstName + " lost its children on page " + page);
                        Assert.IsTrue(row.Skills.All(s => s.SkillName.StartsWith(row.FirstName + "-")));
                    }
                    all.AddRange(rows.Select(x => x.FirstName));
                }

                Assert.AreEqual(10, all.Count);
                Assert.AreEqual(10, all.Distinct().Count(), "A row appeared on two pages.");
            });
        }

        [TestMethod]
        public void Where_after_the_select_that_holds_the_child_collection_is_still_refused()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            db.TransactionWithRollback(() =>
            {
                SeedTen(db, "ChildPage9");

                Assert.ThrowsException<NotSupportedException>(() =>
                    db.CreateQuery<TestEntities.Employee>()
                      .Where(e => e.LastName == "ChildPage9")
                      .Select(e => new { e.FirstName, Skills = skills.Where(s => s.EmployeeId == e.EmployeeId).ToList() })
                      .Where(x => x.FirstName != "A")
                      .OrderBy(x => x.FirstName)
                      .Take(3)
                      .ToList());
            });
        }
    }
}
