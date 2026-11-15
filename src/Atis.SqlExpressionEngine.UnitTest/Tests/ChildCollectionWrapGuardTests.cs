using Atis.SqlExpressionEngine.SqlExpressions;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     A <c>ToList()</c> / <c>ToArray()</c> on a sub-query in a <c>Select</c> becomes a
    ///     <see cref="SqlProjectedSubQueryExpression"/>. <c>SqlSelectExpression.WrapQuery</c> refuses to wrap a query
    ///     that still carries one, because the child query is built from the final query and the wrap would hide the
    ///     link column and the paging key from it. Ordering and paging do not wrap, so they are fine. Plain
    ///     sub-queries (no <c>ToList</c>) are still wrapped as before. Engine level only: no database.
    /// </summary>
    [TestClass]
    public class ChildCollectionWrapGuardTests : TestBase
    {
        private readonly Queryable<Employee> employees = new Queryable<Employee>(new QueryProvider());

        private SqlExpression Convert(Expression expression) => ConvertExpressionToSqlExpression(expression, out _)!;

        [TestMethod]
        public void Marker_in_the_projection_is_a_projected_sub_query_node()
        {
            var q = employees.Select(x => new { x.EmployeeId, Degrees = x.NavDegrees.ToList() });
            var root = (SqlDerivedTableExpression)Convert(q.Expression);
            Assert.IsTrue(root.SelectColumnCollection.SelectColumns.Any(c => c.ColumnExpression is SqlProjectedSubQueryExpression),
                "ToList lost its marker before the query was closed.");
        }

        [TestMethod]
        public void Plain_sub_query_in_the_projection_is_not_marked()
        {
            var q = employees.Select(x => new { x.EmployeeId, Degrees = x.NavDegrees });
            var root = (SqlDerivedTableExpression)Convert(q.Expression);
            Assert.IsFalse(root.SelectColumnCollection.SelectColumns.Any(c => c.ColumnExpression is SqlProjectedSubQueryExpression));
        }

        [TestMethod]
        public void Where_after_the_marker_select_is_refused()
        {
            var q = employees.Select(x => new { x.EmployeeId, Degrees = x.NavDegrees.ToList() })
                             .Where(x => x.EmployeeId != "5");
            Assert.ThrowsException<NotSupportedException>(() => Convert(q.Expression));
        }

        [TestMethod]
        public void Where_then_order_by_and_take_after_the_marker_select_is_refused()
        {
            var q = employees.Select(x => new { x.EmployeeId, Degrees = x.NavDegrees.ToList() })
                             .Where(x => x.EmployeeId != "5")
                             .OrderBy(x => x.EmployeeId)
                             .Take(3);
            Assert.ThrowsException<NotSupportedException>(() => Convert(q.Expression));
        }

        [TestMethod]
        public void Order_by_and_take_after_the_marker_select_are_allowed()
        {
            var q = employees.Where(x => x.EmployeeId != "1").Where(x => x.EmployeeId != "90")
                             .Select(x => new { x.EmployeeId, Degrees = x.NavDegrees.ToList() })
                             .OrderBy(x => x.EmployeeId)
                             .Take(3);
            var root = (SqlDerivedTableExpression)Convert(q.Expression);
            Assert.IsTrue(root.SelectColumnCollection.SelectColumns.Any(c => c.ColumnExpression is SqlProjectedSubQueryExpression));
        }

        [TestMethod]
        public void Skip_and_take_after_the_marker_select_are_allowed()
        {
            var q = employees.Select(x => new { x.EmployeeId, Degrees = x.NavDegrees.ToList() })
                             .OrderBy(x => x.EmployeeId)
                             .Skip(2).Take(3);
            Convert(q.Expression);
        }

        [TestMethod]
        public void Paging_after_the_marker_select_is_allowed()
        {
            var q = employees.Select(x => new { x.EmployeeId, Degrees = x.NavDegrees.ToList() })
                             .OrderBy(x => x.EmployeeId)
                             .Paging(2, 10);
            Convert(q.Expression);
        }

        [TestMethod]
        public void Another_select_after_the_marker_select_is_refused()
        {
            var q = employees.Select(x => new { x.EmployeeId, Degrees = x.NavDegrees.ToList() })
                             .Select(x => new { x.EmployeeId, x.Degrees });
            Assert.ThrowsException<NotSupportedException>(() => Convert(q.Expression));
        }

        [TestMethod]
        public void Another_select_that_drops_the_marker_column_is_refused()
        {
            var q = employees.Select(x => new { x.EmployeeId, Degrees = x.NavDegrees.ToList() })
                             .Select(x => new { x.EmployeeId });
            Assert.ThrowsException<NotSupportedException>(() => Convert(q.Expression));
        }

        [TestMethod]
        public void Order_by_written_after_take_is_refused()
        {
            var q = employees.Select(x => new { x.EmployeeId, Degrees = x.NavDegrees.ToList() })
                             .Take(3)
                             .OrderBy(x => x.EmployeeId);
            Assert.ThrowsException<NotSupportedException>(() => Convert(q.Expression));
        }

        [TestMethod]
        public void Distinct_after_the_marker_select_is_refused()
        {
            var q = employees.Select(x => new { x.EmployeeId, Degrees = x.NavDegrees.ToList() })
                             .Distinct();
            Assert.ThrowsException<NotSupportedException>(() => Convert(q.Expression));
        }

        [TestMethod]
        public void Plain_sub_query_followed_by_where_still_wraps()
        {
            // control: without ToList the old wrap path is untouched
            var q = employees.Select(x => new { x.EmployeeId, Degrees = x.NavDegrees })
                             .Where(x => x.EmployeeId != "5");
            Convert(q.Expression);
        }

        [TestMethod]
        public void Marker_nested_in_another_marker_is_refused()
        {
            var q = employees.Select(x => new
            {
                x.EmployeeId,
                Degrees = x.NavDegrees.Select(d => new { d.Degree, Same = employees.Where(e => e.EmployeeId == d.EmployeeId).Select(e => e.NavDegrees.ToList()).ToList() }).ToList()
            });
            Assert.ThrowsException<NotSupportedException>(() => Convert(q.Expression));
        }
    }
}
