using System.Linq;

using Atis.Orm;
using Atis.Orm.SqlServer;
using Atis.SqlExpressionEngine.SqlExpressions;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     SQL Server writes every table, column and column alias in square brackets. Unbracketed, an alias
    ///     such as <c>Key</c> or <c>Group</c> is a syntax error on the server, and so is a later reference to
    ///     it from an outer query, so the rule has to hold on both sides of the alias.
    /// </summary>
    [TestClass]
    public class SqlServerNamingTests
    {
        private const string ServerConnectionString =
            "Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True";

        private const string Marker = "SqlNaming";

        [TestInitialize]
        public void EnsureDatabase() => new TestDatabaseSetup(ServerConnectionString).Setup();

        [TestMethod]
        public void Every_part_of_a_table_name_is_bracketed()
        {
            var naming = new SqlServerNaming();
            Assert.AreEqual("[Person]", naming.GetQualifiedTableName(new SqlTable("Person")));
            Assert.AreEqual("[dbo].[Person]", naming.GetQualifiedTableName(new SqlTable("Person", "dbo", null, null)));
            Assert.AreEqual("[srv].[db].[dbo].[Person]", naming.GetQualifiedTableName(new SqlTable("Person", "dbo", "db", "srv")));
        }

        [TestMethod]
        public void A_column_is_bracketed_and_a_closing_bracket_in_it_is_doubled()
        {
            var naming = new SqlServerNaming();
            Assert.AreEqual("[Name]", naming.GetColumnName("Name"));
            Assert.AreEqual("[Key]", naming.GetColumnName("Key"));
            Assert.AreEqual("[File Content]", naming.GetColumnName("File Content"));
            Assert.AreEqual("[a]]b]", naming.GetColumnName("a]b"));
        }

        [TestMethod]
        public void A_reserved_word_alias_is_bracketed_where_it_is_defined_and_where_it_is_read()
        {
            var db = new OrmDbContext();
            var q = db.CreateQuery<TestEntities.Employee>()
                      .Select(x => new { Key = x.FirstName, Group = x.LastName, Distinct = x.Salary })
                      .Where(r => r.Group == Marker && r.Distinct > 150m)
                      .OrderBy(r => r.Key);
            var sql = db.TranslateToSql(q);
            StringAssert.Contains(sql, "AS [Key]");
            StringAssert.Contains(sql, "AS [Group]");
            StringAssert.Contains(sql, "AS [Distinct]");
            StringAssert.Contains(sql, "t1.[Group] = @p");
            StringAssert.Contains(sql, "t1.[Distinct] > @p");
            StringAssert.Contains(sql, "ORDER BY t1.[Key]");
        }

        [TestMethod]
        public void A_reserved_word_alias_runs_on_the_server()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                foreach (var (name, salary) in new[] { ("Ann", 300m), ("Bob", 100m), ("Cid", 200m) })
                {
                    db.InsertEntity<TestEntities.Employee>()
                      .Value(x => x.FirstName, () => name)
                      .Value(x => x.LastName, () => Marker)
                      .Value(x => x.Email, () => name.ToLower() + "@sqlnaming.test")
                      .Value(x => x.Salary, () => salary)
                      .Execute();
                }

                var rows = db.CreateQuery<TestEntities.Employee>()
                             .Where(x => x.LastName == Marker)
                             .Select(x => new { Key = x.FirstName, Group = x.LastName, Distinct = x.Salary })
                             .Where(r => r.Group == Marker && r.Distinct > 150m)
                             .OrderBy(r => r.Key)
                             .ToList();

                CollectionAssert.AreEqual(new[] { "Ann", "Cid" }, rows.Select(r => r.Key).ToArray());
                CollectionAssert.AreEqual(new[] { 300m, 200m }, rows.Select(r => r.Distinct).ToArray());
            });
        }
    }
}
