using Atis.Orm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         A sub-query selected in a projection with <c>ToList()</c> / <c>ToArray()</c> actually running against a
    ///         real SQL Server -- the counterpart to <see cref="ChildCollectionProjectionTests"/>, which asserts the
    ///         generated queries and needs no database.
    ///     </para>
    ///     <para>
    ///         Every test seeds its own rows inside <c>TransactionWithRollback</c> and tells them apart from the shared
    ///         data by a last name of its own, so each knows exactly which parents and which children it is looking at.
    ///     </para>
    /// </summary>
    [TestClass]
    public class ChildCollectionExecutionTests
    {
        private const string ServerConnectionString =
            "Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True";

        public class EmployeeWithSkills
        {
            public string FirstName { get; set; }
            public List<TestEntities.EmployeeSkill> Skills { get; set; }
        }

        [TestInitialize]
        public void EnsureDatabase() => new TestDatabaseSetup(ServerConnectionString).Setup();

        private static int SeedEmployee(OrmDbContext db, string lastName, string firstName)
        {
            var rows = db.InsertEntity<TestEntities.Employee>()
                         .Value(x => x.FirstName, () => firstName)
                         .Value(x => x.LastName, () => lastName)
                         .Value(x => x.Email, () => firstName + "@" + lastName + ".test")
                         .Value(x => x.Salary, () => 1m)
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

        [TestMethod]
        public void Each_parent_gets_its_own_children_and_a_parent_without_children_gets_an_empty_list()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            db.TransactionWithRollback(() =>
            {
                var ann = SeedEmployee(db, "ChildExec1", "Ann");
                var bob = SeedEmployee(db, "ChildExec1", "Bob");
                SeedEmployee(db, "ChildExec1", "Cid");
                SeedSkill(db, ann, "Sql");
                SeedSkill(db, ann, "C#");
                SeedSkill(db, bob, "Go");

                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(e => e.LastName == "ChildExec1")
                             .OrderBy(e => e.FirstName)
                             .Select(e => new
                             {
                                 e.FirstName,
                                 Skills = skills.Where(s => s.EmployeeId == e.EmployeeId).ToList()
                             })
                             .ToList();

                Assert.AreEqual(3, rows.Count);
                CollectionAssert.AreEquivalent(new[] { "Sql", "C#" }, rows[0].Skills.Select(x => x.SkillName).ToArray());
                CollectionAssert.AreEquivalent(new[] { "Go" }, rows[1].Skills.Select(x => x.SkillName).ToArray());
                Assert.IsNotNull(rows[2].Skills, "A parent without children gets an empty list, not null.");
                Assert.AreEqual(0, rows[2].Skills.Count);
            });
        }

        [TestMethod]
        public void Child_collection_is_assigned_to_a_member_of_a_class_and_a_filtered_array_works()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            db.TransactionWithRollback(() =>
            {
                var ann = SeedEmployee(db, "ChildExec2", "Ann");
                SeedSkill(db, ann, "Sql");
                SeedSkill(db, ann, "C#");

                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(e => e.LastName == "ChildExec2")
                             .Select(e => new EmployeeWithSkills
                             {
                                 FirstName = e.FirstName,
                                 Skills = skills.Where(s => s.EmployeeId == e.EmployeeId && s.SkillName == "C#").ToList()
                             })
                             .ToList();
                Assert.AreEqual(1, rows.Count);
                Assert.AreEqual(1, rows[0].Skills.Count);
                Assert.AreEqual("C#", rows[0].Skills[0].SkillName);

                var withArray = db.CreateQuery<TestEntities.Employee>()
                                  .Where(e => e.LastName == "ChildExec2")
                                  .Select(e => new
                                  {
                                      e.FirstName,
                                      Skills = skills.Where(s => s.EmployeeId == e.EmployeeId).ToArray()
                                  })
                                  .ToList();
                Assert.AreEqual(2, withArray[0].Skills.Length);
            });
        }

        [TestMethod]
        public void Top_without_order_by_returns_the_same_parents_as_the_children_were_loaded_for()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            db.TransactionWithRollback(() =>
            {
                var ids = new[] { "A", "B", "C", "D" }.Select(n => SeedEmployee(db, "ChildExec3", n)).ToArray();
                foreach (var id in ids)
                    SeedSkill(db, id, "Skill" + id);

                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(e => e.LastName == "ChildExec3")
                             .Take(2)
                             .Select(e => new
                             {
                                 e.EmployeeId,
                                 Skills = skills.Where(s => s.EmployeeId == e.EmployeeId).ToList()
                             })
                             .ToList();

                // the primary key is appended as the order, so it is the first two that come back, and each has its own skill
                Assert.AreEqual(2, rows.Count);
                CollectionAssert.AreEqual(ids.Take(2).ToArray(), rows.Select(x => x.EmployeeId).ToArray());
                foreach (var row in rows)
                {
                    Assert.AreEqual(1, row.Skills.Count);
                    Assert.AreEqual(row.EmployeeId, row.Skills[0].EmployeeId);
                }
            });
        }

        [TestMethod]
        public void Same_query_with_another_captured_value_gets_that_value_in_the_parent_and_the_child_queries()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            db.TransactionWithRollback(() =>
            {
                var ann = SeedEmployee(db, "ChildExec4a", "Ann");
                var bob = SeedEmployee(db, "ChildExec4b", "Bob");
                SeedSkill(db, ann, "FromAnn");
                SeedSkill(db, bob, "FromBob");

                List<string> skillsOf(string lastName)
                {
                    var rows = db.CreateQuery<TestEntities.Employee>()
                                 .Where(e => e.LastName == lastName)
                                 .Select(e => new
                                 {
                                     e.FirstName,
                                     Skills = skills.Where(s => s.EmployeeId == e.EmployeeId).ToList()
                                 })
                                 .ToList();
                    return rows.SelectMany(x => x.Skills).Select(x => x.SkillName).ToList();
                }

                // the second call is served from the compiled query cache and rebinds the value in every statement
                CollectionAssert.AreEqual(new[] { "FromAnn" }, skillsOf("ChildExec4a"));
                CollectionAssert.AreEqual(new[] { "FromBob" }, skillsOf("ChildExec4b"));
            });
        }

        [TestMethod]
        public async Task Child_collection_is_loaded_when_the_query_is_run_asynchronously()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            await db.TransactionWithRollbackAsync(async () =>
            {
                var ann = SeedEmployee(db, "ChildExec5", "Ann");
                SeedSkill(db, ann, "Sql");

                var rows = await db.CreateQuery<TestEntities.Employee>()
                                   .Where(e => e.LastName == "ChildExec5")
                                   .Select(e => new
                                   {
                                       e.FirstName,
                                       Skills = skills.Where(s => s.EmployeeId == e.EmployeeId).ToList()
                                   })
                                   .ToListAsync();

                Assert.AreEqual(1, rows.Count);
                Assert.AreEqual("Sql", rows[0].Skills.Single().SkillName);
            });
        }

        [TestMethod]
        public void A_query_member_that_is_not_a_list_is_refused_with_a_message_that_says_what_to_write()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            db.TransactionWithRollback(() =>
            {
                SeedEmployee(db, "ChildExec6", "Ann");

                var query = db.CreateQuery<TestEntities.Employee>()
                              .Where(e => e.LastName == "ChildExec6")
                              .Select(e => new
                              {
                                  e.FirstName,
                                  Skills = skills.Where(s => s.EmployeeId == e.EmployeeId)
                              });

                var exception = Assert.ThrowsException<NotSupportedException>(() => query.ToList());
                StringAssert.Contains(exception.Message, "ToList()");
            });
        }

        [TestMethod]
        public void Child_collection_of_a_single_column_gets_its_values_per_parent()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            db.TransactionWithRollback(() =>
            {
                var ann = SeedEmployee(db, "ChildExec7", "Ann");
                var bob = SeedEmployee(db, "ChildExec7", "Bob");
                SeedEmployee(db, "ChildExec7", "Cid");
                SeedSkill(db, ann, "Sql");
                SeedSkill(db, ann, "C#");
                SeedSkill(db, bob, "Go");

                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(e => e.LastName == "ChildExec7")
                             .OrderBy(e => e.FirstName)
                             .Select(e => new
                             {
                                 e.FirstName,
                                 Names = skills.Where(s => s.EmployeeId == e.EmployeeId).Select(s => s.SkillName).ToList()
                             })
                             .ToList();

                Assert.AreEqual(3, rows.Count);
                CollectionAssert.AreEquivalent(new[] { "Sql", "C#" }, rows[0].Names);
                CollectionAssert.AreEquivalent(new[] { "Go" }, rows[1].Names);
                Assert.AreEqual(0, rows[2].Names.Count);
            });
        }

        [TestMethod]
        public void Child_collection_projected_to_an_anonymous_type_gets_its_items_per_parent()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            db.TransactionWithRollback(() =>
            {
                var ann = SeedEmployee(db, "ChildExec8", "Ann");
                var bob = SeedEmployee(db, "ChildExec8", "Bob");
                SeedSkill(db, ann, "Sql");
                SeedSkill(db, ann, "C#");
                SeedSkill(db, bob, "Go");

                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(e => e.LastName == "ChildExec8")
                             .OrderBy(e => e.FirstName)
                             .Select(e => new
                             {
                                 e.FirstName,
                                 Items = skills.Where(s => s.EmployeeId == e.EmployeeId)
                                               .Select(s => new { s.SkillName, s.ProficiencyLevel })
                                               .ToList()
                             })
                             .ToList();

                Assert.AreEqual(2, rows.Count);
                CollectionAssert.AreEquivalent(new[] { "Sql", "C#" }, rows[0].Items.Select(x => x.SkillName).ToArray());
                CollectionAssert.AreEquivalent(new[] { "Go" }, rows[1].Items.Select(x => x.SkillName).ToArray());
                Assert.IsTrue(rows.SelectMany(x => x.Items).All(x => x.ProficiencyLevel == 3));
            });
        }

        [TestMethod]
        public void Child_collection_with_its_own_take_gets_that_many_values_per_parent()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            db.TransactionWithRollback(() =>
            {
                var ann = SeedEmployee(db, "ChildExec9", "Ann");
                var bob = SeedEmployee(db, "ChildExec9", "Bob");
                SeedSkill(db, ann, "Sql");
                SeedSkill(db, ann, "C#");
                SeedSkill(db, ann, "Go");
                SeedSkill(db, bob, "Rust");

                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(e => e.LastName == "ChildExec9")
                             .OrderBy(e => e.FirstName)
                             .Select(e => new
                             {
                                 e.FirstName,
                                 Names = skills.Where(s => s.EmployeeId == e.EmployeeId).Select(s => s.SkillName).Take(2).ToList()
                             })
                             .ToList();

                // the limit is per parent, not over all the child rows
                Assert.AreEqual(2, rows[0].Names.Count);
                CollectionAssert.AreEqual(new[] { "Rust" }, rows[1].Names);
            });
        }

        [TestMethod]
        public void Child_collection_of_a_single_column_under_a_parent_with_order_and_take_is_loaded_for_the_returned_parents_only()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            db.TransactionWithRollback(() =>
            {
                var ids = new[] { "A", "B", "C" }.Select(n => SeedEmployee(db, "ChildExec10", n)).ToArray();
                foreach (var id in ids)
                    SeedSkill(db, id, "Skill" + id);

                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(e => e.LastName == "ChildExec10")
                             .Select(e => new
                             {
                                 e.FirstName,
                                 Names = skills.Where(s => s.EmployeeId == e.EmployeeId).Select(s => s.SkillName).ToList()
                             })
                             .OrderBy(x => x.FirstName)
                             .Take(2)
                             .ToList();

                Assert.AreEqual(2, rows.Count);
                CollectionAssert.AreEqual(new[] { "A", "B" }, rows.Select(x => x.FirstName).ToArray());
                for (var i = 0; i < rows.Count; i++)
                    CollectionAssert.AreEqual(new[] { "Skill" + ids[i] }, rows[i].Names);
            });
        }
    }
}
