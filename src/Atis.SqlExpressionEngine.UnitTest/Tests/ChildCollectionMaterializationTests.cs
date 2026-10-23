using Atis.Orm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         More end-to-end checks of how child collections are put back together in memory (the "materialization"
    ///         step), against a real SQL Server. They complement <see cref="ChildCollectionExecutionTests"/>.
    ///     </para>
    ///     <para>
    ///         Every test seeds its own rows inside <c>TransactionWithRollback</c> and tells them apart by a last name of
    ///         its own (<c>ChildMat1</c> to <c>ChildMat14</c>).
    ///     </para>
    /// </summary>
    [TestClass]
    public class ChildCollectionMaterializationTests
    {
        private const string ServerConnectionString =
            "Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True";

        public class EmployeeWithEveryListType
        {
            public string FirstName { get; set; }
            public List<TestEntities.EmployeeSkill> AsList { get; set; }
            public IList<TestEntities.EmployeeSkill> AsIList { get; set; }
            public ICollection<TestEntities.EmployeeSkill> AsICollection { get; set; }
            public IReadOnlyList<TestEntities.EmployeeSkill> AsIReadOnlyList { get; set; }
            public IEnumerable<TestEntities.EmployeeSkill> AsIEnumerable { get; set; }
        }

        [TestInitialize]
        public void EnsureDatabase() => new TestDatabaseSetup(ServerConnectionString).Setup();

        private static int SeedEmployee(OrmDbContext db, string lastName, string firstName, int? managerId = null)
        {
            var rows = db.InsertEntity<TestEntities.Employee>()
                         .Value(x => x.FirstName, () => firstName)
                         .Value(x => x.LastName, () => lastName)
                         .Value(x => x.Email, () => firstName + "@" + lastName + ".test")
                         .Value(x => x.Salary, () => 1m)
                         .Value(x => x.ManagerId, () => managerId)
                         .Output(x => x.EmployeeId)
                         .ExecuteDictionary();
            return (int)rows[0]["EmployeeId"];
        }

        private static void SeedSkill(OrmDbContext db, int employeeId, string skillName, int? years = null)
        {
            db.InsertEntity<TestEntities.EmployeeSkill>()
              .Value(x => x.EmployeeId, () => employeeId)
              .Value(x => x.SkillName, () => skillName)
              .Value(x => x.ProficiencyLevel, () => 3)
              .Value(x => x.YearsOfExperience, () => years)
              .Execute();
        }

        // 1. A link made of two columns ------------------------------------------------------------------------------

        [TestMethod]
        public void A_link_made_of_two_columns_gives_each_parent_only_the_rows_that_match_both()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            db.TransactionWithRollback(() =>
            {
                var ann = SeedEmployee(db, "ChildMat1", "Ann");
                var bob = SeedEmployee(db, "ChildMat1", "Bob");
                // The link is "same employee AND skill name equals the parent's first name".
                SeedSkill(db, ann, "Ann");      // matches Ann
                SeedSkill(db, ann, "Bob");      // right employee, wrong name: must not be given to anyone
                SeedSkill(db, bob, "Bob");      // matches Bob
                SeedSkill(db, bob, "Ann");      // right name for Ann, but belongs to Bob's employee id

                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(e => e.LastName == "ChildMat1")
                             .OrderBy(e => e.FirstName)
                             .Select(e => new
                             {
                                 e.FirstName,
                                 Skills = skills.Where(s => s.EmployeeId == e.EmployeeId && s.SkillName == e.FirstName).ToList()
                             })
                             .ToList();

                Assert.AreEqual(2, rows.Count);
                Assert.AreEqual(1, rows[0].Skills.Count);
                Assert.AreEqual(ann, rows[0].Skills[0].EmployeeId);
                Assert.AreEqual("Ann", rows[0].Skills[0].SkillName);
                Assert.AreEqual(1, rows[1].Skills.Count);
                Assert.AreEqual(bob, rows[1].Skills[0].EmployeeId);
                Assert.AreEqual("Bob", rows[1].Skills[0].SkillName);
            });
        }

        // 2. A parent whose link value is NULL -----------------------------------------------------------------------

        [TestMethod]
        public void A_parent_whose_link_value_is_null_gets_an_empty_list_and_not_null()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            db.TransactionWithRollback(() =>
            {
                var boss = SeedEmployee(db, "ChildMat2", "Boss");
                SeedSkill(db, boss, "Leading");
                SeedEmployee(db, "ChildMat2", "Worker", boss);
                SeedEmployee(db, "ChildMat2", "Loner");   // ManagerId is NULL

                // The link is "the skill belongs to my manager": ManagerId is the parent value, and it can be NULL.
                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(e => e.LastName == "ChildMat2" && e.FirstName != "Boss")
                             .OrderBy(e => e.FirstName)
                             .Select(e => new
                             {
                                 e.FirstName,
                                 ManagerSkills = skills.Where(s => s.EmployeeId == e.ManagerId).ToList()
                             })
                             .ToList();

                Assert.AreEqual(2, rows.Count);
                Assert.AreEqual("Loner", rows[0].FirstName);
                Assert.IsNotNull(rows[0].ManagerSkills, "A NULL link matches nothing and gives an empty list, not null.");
                Assert.AreEqual(0, rows[0].ManagerSkills.Count);
                Assert.AreEqual("Worker", rows[1].FirstName);
                Assert.AreEqual("Leading", rows[1].ManagerSkills.Single().SkillName);
            });
        }

        // 3. A child that is not linked to the parent ----------------------------------------------------------------

        [TestMethod]
        public void A_child_that_is_not_linked_to_the_parent_gives_every_parent_every_row()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            db.TransactionWithRollback(() =>
            {
                var ann = SeedEmployee(db, "ChildMat3", "Ann");
                var bob = SeedEmployee(db, "ChildMat3", "Bob");
                SeedSkill(db, ann, "ChildMat3Skill1");
                SeedSkill(db, bob, "ChildMat3Skill2");
                SeedSkill(db, bob, "ChildMat3Other");

                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(e => e.LastName == "ChildMat3")
                             .OrderBy(e => e.FirstName)
                             .Select(e => new
                             {
                                 e.FirstName,
                                 // nothing here refers to e
                                 Skills = skills.Where(s => s.SkillName.StartsWith("ChildMat3Skill")).ToList()
                             })
                             .ToList();

                Assert.AreEqual(2, rows.Count);
                foreach (var row in rows)
                    CollectionAssert.AreEquivalent(new[] { "ChildMat3Skill1", "ChildMat3Skill2" }, row.Skills.Select(x => x.SkillName).ToArray());
                // each parent gets its own list object, not one shared list
                Assert.AreNotSame(rows[0].Skills, rows[1].Skills);
            });
        }

        // 4. Two child collections in one projection -----------------------------------------------------------------

        [TestMethod]
        public void Two_child_collections_in_one_projection_are_each_filled_from_their_own_query()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            var employees = db.CreateQuery<TestEntities.Employee>();
            db.TransactionWithRollback(() =>
            {
                var boss = SeedEmployee(db, "ChildMat4", "Boss");
                var other = SeedEmployee(db, "ChildMat4", "Other");
                SeedEmployee(db, "ChildMat4", "Rep1", boss);
                SeedEmployee(db, "ChildMat4", "Rep2", boss);
                SeedSkill(db, boss, "Leading");
                SeedSkill(db, other, "Sql");
                SeedSkill(db, other, "Go");

                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(e => e.LastName == "ChildMat4" && e.ManagerId == null)
                             .OrderBy(e => e.FirstName)
                             .Select(e => new
                             {
                                 e.FirstName,
                                 Skills = skills.Where(s => s.EmployeeId == e.EmployeeId).Select(s => s.SkillName).ToList(),
                                 Reports = employees.Where(x => x.ManagerId == e.EmployeeId).Select(x => x.FirstName).ToList()
                             })
                             .ToList();

                Assert.AreEqual(2, rows.Count);
                Assert.AreEqual("Boss", rows[0].FirstName);
                CollectionAssert.AreEqual(new[] { "Leading" }, rows[0].Skills);
                CollectionAssert.AreEquivalent(new[] { "Rep1", "Rep2" }, rows[0].Reports);
                Assert.AreEqual("Other", rows[1].FirstName);
                CollectionAssert.AreEquivalent(new[] { "Sql", "Go" }, rows[1].Skills);
                Assert.AreEqual(0, rows[1].Reports.Count, "The second collection must not receive the first one's rows.");
            });
        }

        // 5. Two parents sharing the same link value -----------------------------------------------------------------

        [TestMethod]
        public void Two_parents_with_the_same_link_value_both_get_the_same_children()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            db.TransactionWithRollback(() =>
            {
                var boss = SeedEmployee(db, "ChildMat5", "Boss");
                SeedSkill(db, boss, "Leading");
                SeedSkill(db, boss, "Planning");
                SeedEmployee(db, "ChildMat5", "Rep1", boss);
                SeedEmployee(db, "ChildMat5", "Rep2", boss);

                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(e => e.LastName == "ChildMat5" && e.ManagerId != null)
                             .OrderBy(e => e.FirstName)
                             .Select(e => new
                             {
                                 e.FirstName,
                                 ManagerSkills = skills.Where(s => s.EmployeeId == e.ManagerId).ToList()
                             })
                             .ToList();

                Assert.AreEqual(2, rows.Count);
                foreach (var row in rows)
                    CollectionAssert.AreEquivalent(new[] { "Leading", "Planning" }, row.ManagerSkills.Select(x => x.SkillName).ToArray());
                Assert.AreNotSame(rows[0].ManagerSkills, rows[1].ManagerSkills, "Each parent gets its own list.");
            });
        }

        // 6. Member types --------------------------------------------------------------------------------------------

        [TestMethod]
        public void Child_collection_can_be_assigned_to_List_IList_ICollection_IReadOnlyList_and_IEnumerable_members()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            db.TransactionWithRollback(() =>
            {
                var ann = SeedEmployee(db, "ChildMat6", "Ann");
                SeedEmployee(db, "ChildMat6", "Bob");
                SeedSkill(db, ann, "Sql");
                SeedSkill(db, ann, "C#");

                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(e => e.LastName == "ChildMat6")
                             .OrderBy(e => e.FirstName)
                             .Select(e => new EmployeeWithEveryListType
                             {
                                 FirstName = e.FirstName,
                                 AsList = skills.Where(s => s.EmployeeId == e.EmployeeId).ToList(),
                                 AsIList = skills.Where(s => s.EmployeeId == e.EmployeeId).ToList(),
                                 AsICollection = skills.Where(s => s.EmployeeId == e.EmployeeId).ToList(),
                                 AsIReadOnlyList = skills.Where(s => s.EmployeeId == e.EmployeeId).ToList(),
                                 AsIEnumerable = skills.Where(s => s.EmployeeId == e.EmployeeId).ToList(),
                             })
                             .ToList();

                Assert.AreEqual(2, rows.Count);
                var expected = new[] { "Sql", "C#" };
                CollectionAssert.AreEquivalent(expected, rows[0].AsList.Select(x => x.SkillName).ToArray());
                CollectionAssert.AreEquivalent(expected, rows[0].AsIList.Select(x => x.SkillName).ToArray());
                CollectionAssert.AreEquivalent(expected, rows[0].AsICollection.Select(x => x.SkillName).ToArray());
                CollectionAssert.AreEquivalent(expected, rows[0].AsIReadOnlyList.Select(x => x.SkillName).ToArray());
                CollectionAssert.AreEquivalent(expected, rows[0].AsIEnumerable.Select(x => x.SkillName).ToArray());

                // a parent without children gets an empty collection of each type, never null
                Assert.AreEqual(0, rows[1].AsList.Count);
                Assert.AreEqual(0, rows[1].AsIList.Count);
                Assert.AreEqual(0, rows[1].AsICollection.Count);
                Assert.AreEqual(0, rows[1].AsIReadOnlyList.Count);
                Assert.AreEqual(0, rows[1].AsIEnumerable.Count());
            });
        }

        // 7. Terminals other than ToList / ToListAsync ---------------------------------------------------------------

        [TestMethod]
        public void First_and_FirstOrDefault_on_the_outer_query_return_the_parent_with_its_children()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            db.TransactionWithRollback(() =>
            {
                var ann = SeedEmployee(db, "ChildMat7", "Ann");
                SeedSkill(db, ann, "Sql");
                SeedSkill(db, ann, "C#");

                var query = db.CreateQuery<TestEntities.Employee>()
                              .Where(e => e.LastName == "ChildMat7")
                              .Select(e => new
                              {
                                  e.FirstName,
                                  Skills = skills.Where(s => s.EmployeeId == e.EmployeeId).ToList()
                              });

                var first = query.First();
                Assert.AreEqual("Ann", first.FirstName);
                Assert.AreEqual(2, first.Skills.Count);

                var firstOrDefault = query.FirstOrDefault();
                Assert.AreEqual(2, firstOrDefault.Skills.Count);
            });
        }

        [TestMethod]
        public void Enumerating_the_outer_query_directly_and_ToArray_return_the_parents_with_their_children()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            db.TransactionWithRollback(() =>
            {
                var ann = SeedEmployee(db, "ChildMat8", "Ann");
                var bob = SeedEmployee(db, "ChildMat8", "Bob");
                SeedSkill(db, ann, "Sql");
                SeedSkill(db, bob, "Go");
                SeedSkill(db, bob, "Rust");

                var query = db.CreateQuery<TestEntities.Employee>()
                              .Where(e => e.LastName == "ChildMat8")
                              .OrderBy(e => e.FirstName)
                              .Select(e => new
                              {
                                  e.FirstName,
                                  Skills = skills.Where(s => s.EmployeeId == e.EmployeeId).ToList()
                              });

                var counts = new List<int>();
                foreach (var row in query)
                    counts.Add(row.Skills.Count);
                CollectionAssert.AreEqual(new[] { 1, 2 }, counts);

                var array = query.ToArray();
                CollectionAssert.AreEqual(new[] { 1, 2 }, array.Select(x => x.Skills.Count).ToArray());
            });
        }

        [TestMethod]
        public async Task Async_FirstOrDefault_on_the_outer_query_return_the_parents_with_their_children()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            await db.TransactionWithRollbackAsync(async () =>
            {
                var ann = SeedEmployee(db, "ChildMat9", "Ann");
                SeedSkill(db, ann, "Sql");
                SeedSkill(db, ann, "C#");

                var query = db.CreateQuery<TestEntities.Employee>()
                              .Where(e => e.LastName == "ChildMat9")
                              .Select(e => new
                              {
                                  e.FirstName,
                                  Skills = skills.Where(s => s.EmployeeId == e.EmployeeId).ToList()
                              });

                var first = await query.FirstOrDefaultAsync();
                Assert.AreEqual(2, first.Skills.Count);
            });
        }

        // 8. NULL values inside a child column -----------------------------------------------------------------------

        [TestMethod]
        public void A_null_inside_a_single_column_child_is_kept_as_a_null_item()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            db.TransactionWithRollback(() =>
            {
                var ann = SeedEmployee(db, "ChildMat10", "Ann");
                SeedSkill(db, ann, "Sql", 5);
                SeedSkill(db, ann, "C#");           // YearsOfExperience is NULL

                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(e => e.LastName == "ChildMat10")
                             .Select(e => new
                             {
                                 e.FirstName,
                                 Years = skills.Where(s => s.EmployeeId == e.EmployeeId).Select(s => s.YearsOfExperience).ToList()
                             })
                             .ToList();

                Assert.AreEqual(1, rows.Count);
                Assert.AreEqual(2, rows[0].Years.Count, "A NULL value is still an item; it must not be dropped.");
                CollectionAssert.AreEquivalent(new int?[] { 5, null }, rows[0].Years);
            });
        }

        [TestMethod]
        public void A_null_inside_an_entity_child_is_read_as_a_null_property()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            db.TransactionWithRollback(() =>
            {
                var ann = SeedEmployee(db, "ChildMat11", "Ann");
                SeedSkill(db, ann, "Sql");

                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(e => e.LastName == "ChildMat11")
                             .Select(e => new
                             {
                                 e.FirstName,
                                 Items = skills.Where(s => s.EmployeeId == e.EmployeeId)
                                               .Select(s => new { s.SkillName, s.YearsOfExperience, s.CertifiedDate })
                                               .ToList()
                             })
                             .ToList();

                var item = rows.Single().Items.Single();
                Assert.AreEqual("Sql", item.SkillName);
                Assert.IsNull(item.YearsOfExperience);
                Assert.IsNull(item.CertifiedDate);
            });
        }

        [TestMethod]
        public void A_null_in_the_parent_columns_does_not_disturb_the_children()
        {
            var db = new OrmDbContext();
            var skills = db.CreateQuery<TestEntities.EmployeeSkill>();
            db.TransactionWithRollback(() =>
            {
                var ann = SeedEmployee(db, "ChildMat12", "Ann");     // HireDate and ManagerId are NULL
                SeedSkill(db, ann, "Sql");

                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(e => e.LastName == "ChildMat12")
                             .Select(e => new
                             {
                                 e.HireDate,
                                 e.ManagerId,
                                 Skills = skills.Where(s => s.EmployeeId == e.EmployeeId).ToList(),
                                 e.FirstName
                             })
                             .ToList();

                var row = rows.Single();
                Assert.IsNull(row.HireDate);
                Assert.IsNull(row.ManagerId);
                Assert.AreEqual("Ann", row.FirstName);
                Assert.AreEqual("Sql", row.Skills.Single().SkillName);
            });
        }
    }
}
