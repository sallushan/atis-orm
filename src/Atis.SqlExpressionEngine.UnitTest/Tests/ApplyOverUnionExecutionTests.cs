using Atis.Orm;
using Atis.SqlExpressionEngine;
using System;
using System.Linq;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         <c>CROSS APPLY (VALUES (..), (..))</c> is spelled as a cross apply over a
    ///         <c>UNION ALL</c> of standalone (no FROM) selects, so the engine needs no VALUES node.
    ///         The text of that SQL is checked in <see cref="DirectSelectCallTests"/>; here it
    ///         runs on a real SQL Server, because type inference of a NULL-or-string CASE across
    ///         union branches is something only the server can answer.
    ///     </para>
    /// </summary>
    [TestClass]
    public class ApplyOverUnionExecutionTests
    {
        private const string ServerConnectionString =
            "Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True";

        private const string Marker = "ApplyUnion";

        [TestInitialize]
        public void EnsureDatabase() => new TestDatabaseSetup(ServerConnectionString).Setup();

        private static void Seed(OrmDbContext db, string firstName, decimal salary)
        {
            db.InsertEntity<TestEntities.Employee>()
              .Value(x => x.FirstName, () => firstName)
              .Value(x => x.LastName, () => Marker)
              .Value(x => x.Email, () => firstName.ToLower() + "@applyunion.test")
              .Value(x => x.Salary, () => salary)
              .Value(x => x.HireDate, () => (DateTime?)new DateTime(2020, 1, 1))
              .Execute();
        }

        [TestMethod]
        public void Cross_apply_over_union_all_of_one_row_selects_gives_one_row_per_matching_label()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                Seed(db, "Ann", 300m);   // All, Top
                Seed(db, "Bob", 200m);   // All
                Seed(db, "Dee", 100m);   // All, Bottom

                var employees = db.CreateQuery<TestEntities.Employee>();
                var qp = employees.Provider;   // standalone selects start from the query provider
                var rows = employees
                             .Where(x => x.LastName == Marker)
                             .CrossApply(
                                 x => qp.Select(() => new { Label = "All" })
                                        .UnionAll(qp.Select(() => new { Label = x.Salary >= 250m ? "Top" : null }))
                                        .UnionAll(qp.Select(() => new { Label = x.Salary < 150m ? "Bottom" : null })),
                                 (x, v) => new { x.FirstName, v.Label })
                             .Where(r => r.Label != null)
                             .ToList()
                             .Select(r => r.FirstName + ":" + r.Label)
                             .OrderBy(s => s, StringComparer.Ordinal)
                             .ToArray();

                CollectionAssert.AreEqual(
                    new[] { "Ann:All", "Ann:Top", "Bob:All", "Dee:All", "Dee:Bottom" },
                    rows);
            });
        }

        [TestMethod]
        public void Cross_apply_over_union_all_keeps_a_captured_variable_as_a_parameter_across_runs()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                Seed(db, "Ann", 300m);
                Seed(db, "Bob", 200m);

                var employees = db.CreateQuery<TestEntities.Employee>();
                var qp = employees.Provider;
                string[] Run(decimal limit) =>
                    employees
                      .Where(x => x.LastName == Marker)
                      .CrossApply(
                          x => qp.Select(() => new { Label = "All" })
                                 .UnionAll(qp.Select(() => new { Label = x.Salary >= limit ? "AtLeast" : null })),
                          (x, v) => new { x.FirstName, v.Label })
                      .Where(r => r.Label != null)
                      .ToList()
                      .Select(r => r.FirstName + ":" + r.Label)
                      .OrderBy(s => s, StringComparer.Ordinal)
                      .ToArray();

                CollectionAssert.AreEqual(new[] { "Ann:All", "Ann:AtLeast", "Bob:All" }, Run(250m));
                // second call hits the compiled-query cache and must rebind the parameter
                CollectionAssert.AreEqual(new[] { "Ann:All", "Ann:AtLeast", "Bob:All", "Bob:AtLeast" }, Run(150m));
            });
        }
    }
}
