using Atis.SqlExpressionEngine.Internal;
using Atis.SqlExpressionEngine.Services;
using Atis.SqlExpressionEngine.SqlExpressions;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     Exploration: where do OrderBy / Take sit in the AST when they are written before or after the Select that
    ///     carries the child collection, and what does <see cref="ChildQueryExtractor"/> make of each spelling.
    /// </summary>
    [TestClass]
    public class ChildQueryExtractorOrderingExplorationTests : TestBase
    {
        private readonly Queryable<Person> people = new Queryable<Person>(new QueryProvider());

        private void Explore(string title, System.Linq.Expressions.Expression expression)
        {
            Console.WriteLine("=================== " + title);
            var root = (SqlDerivedTableExpression)ConvertExpressionToSqlExpression(expression, out _)!;
            Console.WriteLine("--- root as the engine built it");
            Console.WriteLine(new SqlExpressionTranslator().Translate(root));
            try
            {
                var result = new ChildQueryExtractor(new SqlExpressionFactory()).Extract(root);
                Console.WriteLine("--- parent query after Extract");
                Console.WriteLine(new SqlExpressionTranslator().Translate(result.ParentQuery));
                foreach (var child in result.ChildQueries)
                {
                    Console.WriteLine("--- child sub-query as it was in the root (not yet rewritten)");
                    Console.WriteLine(new SqlExpressionTranslator().Translate(child.Source.Query));
                    Console.WriteLine("--- child query after Extract (what will run)");
                    Console.WriteLine(new SqlExpressionTranslator().Translate(child.Query));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("--- Extract threw " + ex.GetType().Name + ": " + ex.Message);
                Console.WriteLine(string.Join(Environment.NewLine, (ex.StackTrace ?? "").Split('\n').Take(8)));
            }
        }

        [TestMethod]
        public void A_OrderBy_Take_then_Select()
            => Explore("A: OrderBy(Age).Take(3).Select(..)",
                people.OrderBy(p => p.Age).Take(3)
                      .Select(p => new { p.Age, Shoes = p.NavShoes.ToList() }).Expression);

        [TestMethod]
        public void B_Select_then_OrderBy_Take()
            => Explore("B: Select(..).OrderBy(Age).Take(3)",
                people.Select(p => new { p.Age, Shoes = p.NavShoes.ToList() })
                      .OrderBy(x => x.Age).Take(3).Expression);

        [TestMethod]
        public void C_OrderBy_then_Select_no_Take()
            => Explore("C: OrderBy(Age).Select(..)  (no Take)",
                people.OrderBy(p => p.Age)
                      .Select(p => new { p.Age, Shoes = p.NavShoes.ToList() }).Expression);

        [TestMethod]
        public void D_Select_then_OrderBy_no_Take()
            => Explore("D: Select(..).OrderBy(Age)  (no Take)",
                people.Select(p => new { p.Age, Shoes = p.NavShoes.ToList() })
                      .OrderBy(x => x.Age).Expression);

        [TestMethod]
        public void E_Select_then_Take_no_OrderBy()
            => Explore("E: Select(..).Take(3)  (no OrderBy)",
                people.Select(p => new { p.Age, Shoes = p.NavShoes.ToList() })
                      .Take(3).Expression);

        [TestMethod]
        public void F_Where_Select_OrderBy_Take()
            => Explore("F: Where.Select(..).OrderBy(Age).Take(3)",
                people.Where(p => p.Age > 10)
                      .Select(p => new { p.Age, Shoes = p.NavShoes.ToList() })
                      .OrderBy(x => x.Age).Take(3).Expression);

        [TestMethod]
        public void G_Select_Where_OrderBy_Take()
            => Explore("G: Select(..).Where(on selected col).OrderBy(Age).Take(3)",
                people.Select(p => new { p.Age, Shoes = p.NavShoes.ToList() })
                      .Where(x => x.Age > 10)
                      .OrderBy(x => x.Age).Take(3).Expression);

        [TestMethod]
        public void H_Select_without_key_column_OrderBy_Take_OrderByThenBy()
            => Explore("H: Select(..).OrderBy(Age).ThenBy(Age).Take(3)",
                people.Select(p => new { p.Age, p.FirstName, Shoes = p.NavShoes.ToList() })
                      .OrderBy(x => x.Age).ThenBy(x => x.FirstName).Take(3).Expression);

        [TestMethod]
        public void J_Select_then_OrderBy_primary_key_Take()
            => Explore("J: Select(Id, ..).OrderBy(Id).Take(3)  (alias spelling of the key)",
                people.Select(p => new { p.Id, Shoes = p.NavShoes.ToList() })
                      .OrderBy(x => x.Id).Take(3).Expression);

        [TestMethod]
        public void K_OrderBy_primary_key_Take_then_Select()
            => Explore("K: OrderBy(Id).Take(3).Select(..)  (qualified spelling of the key)",
                people.OrderBy(p => p.Id).Take(3)
                      .Select(p => new { p.Id, Shoes = p.NavShoes.ToList() }).Expression);

        [TestMethod]
        public void L_OrderBy_primary_key_Select_Take()
            => Explore("L: OrderBy(Id).Select(..).Take(3)",
                people.OrderBy(p => p.Id)
                      .Select(p => new { p.Id, Shoes = p.NavShoes.ToList() }).Take(3).Expression);

        [TestMethod]
        public void M_Projected_child_Select_Where_OrderBy_Take()
            => Explore("M: Select(Age, Shoes = NavShoes.Select(Style).ToList()).Where.OrderBy.Take",
                people.Select(p => new { p.Age, Shoes = p.NavShoes.Select(s => s.Style).ToList() })
                      .Where(x => x.Age > 5)
                      .OrderBy(x => x.Age).Take(3).Expression);

        [TestMethod]
        public void N_Scalar_child_select_list_before_and_after_join_predicate_extraction()
        {
            var q = people.Select(p => new { p.Age, Shoes = p.NavShoes.Select(s => s.Style).ToList() })
                          .Where(x => x.Age > 5);
            var root = (SqlDerivedTableExpression)ConvertExpressionToSqlExpression(q.Expression, out _)!;
            var queryable = (SqlQueryableExpression)root.SelectColumnCollection.SelectColumns
                                .First(x => x.ColumnExpression is SqlQueryableExpression).ColumnExpression;

            static string Columns(SqlDerivedTableExpression d)
                => string.Join(", ", d.SelectColumnCollection.SelectColumns.Select(c => c.Alias + " (scalar=" + c.ScalarColumn + ")"));

            Console.WriteLine("before: " + Columns(queryable.Query));

            var builder = new JoinableQueryBuilder(queryable.Query, new HashSet<Guid> { root.FromSource.Alias }, new SqlExpressionFactory(), Guid.NewGuid());
            Assert.IsTrue(builder.TryBuild(out var result));
            Console.WriteLine("after:  " + Columns(result.NormalizedDerivedTable));
            Console.WriteLine("IsTableOnly after: " + result.NormalizedDerivedTable.IsTableOnly);
        }

        [TestMethod]
        public void I_Select_Take_then_Select_again()
            => Explore("I: Select(..).Take(3).Select(..) twice",
                people.OrderBy(p => p.Age).Take(3)
                      .Select(p => new { p.Age, p.Id })
                      .Select(x => new { x.Age, Shoes = people.Where(s => s.Id == x.Id).ToList() }).Expression);
    }
}
