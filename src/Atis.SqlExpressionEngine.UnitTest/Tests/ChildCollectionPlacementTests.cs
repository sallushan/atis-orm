using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using Atis.Orm.Services;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         Covers <see cref="ChildCollectionPlacementValidator"/>: a child collection
    ///         (<c>ToList()</c> / <c>ToArray()</c> on a sub-query in a <c>Select</c>) is allowed only in the last
    ///         <c>Select</c> of the query. The trees are built over in-memory queryables; nothing is translated
    ///         or executed.
    ///     </para>
    /// </summary>
    [TestClass]
    public class ChildCollectionPlacementTests
    {
        private class Shoe { public int Id { get; set; } public int PersonId { get; set; } public string Brand { get; set; } }

        private class Person
        {
            public int Id { get; set; }
            public int Age { get; set; }
            public IQueryable<Shoe> Shoes { get; set; }
        }

        private static readonly IQueryable<Person> People = new List<Person>().AsQueryable();

        private static void AssertAllowed(Expression expression)
            => ChildCollectionPlacementValidator.Validate(expression);

        private static NotSupportedException AssertRejected(Expression expression)
        {
            try
            {
                ChildCollectionPlacementValidator.Validate(expression);
            }
            catch (NotSupportedException ex)
            {
                return ex;
            }
            Assert.Fail("Expected NotSupportedException.");
            return null;
        }

        [TestMethod]
        public void Marker_in_the_last_select_is_allowed()
        {
            var q = People.Where(x => x.Age > 5).OrderBy(x => x.Age).Take(3)
                          .Select(x => new { x.Age, Shoes = x.Shoes.ToList() });
            AssertAllowed(q.Expression);
        }

        [TestMethod]
        public void ToArray_in_the_last_select_is_allowed()
        {
            var q = People.Select(x => new { x.Age, Shoes = x.Shoes.ToArray() });
            AssertAllowed(q.Expression);
        }

        [TestMethod]
        public void Terminal_after_the_last_select_is_allowed()
        {
            var q = People.Select(x => new { x.Age, Shoes = x.Shoes.ToList() });
            var first = Expression.Call(typeof(System.Linq.Queryable), nameof(System.Linq.Queryable.FirstOrDefault), new[] { q.ElementType }, q.Expression);
            AssertAllowed(first);
        }

        [TestMethod]
        public void Marker_in_an_earlier_select_followed_by_where_is_rejected()
        {
            // the example that started this: reads as if the shoes were loaded before the filter
            var q = People.Select(x => new { x.Age, Shoes = x.Shoes.ToList() })
                          .Where(x => x.Age > 5)
                          .OrderBy(x => x.Age)
                          .Take(3);
            var ex = AssertRejected(q.Expression);
            StringAssert.Contains(ex.Message, "last Select");
            StringAssert.Contains(ex.Message, "followed by 'Where'");
        }

        [TestMethod]
        public void Marker_followed_by_take_alone_is_rejected()
        {
            var q = People.Select(x => new { x.Age, Shoes = x.Shoes.ToList() }).Take(3);
            var ex = AssertRejected(q.Expression);
            StringAssert.Contains(ex.Message, "followed by 'Take'");
        }

        [TestMethod]
        public void Marker_in_an_earlier_select_followed_by_another_select_is_rejected()
        {
            var q = People.Select(x => new { x.Age, Shoes = x.Shoes.ToList() })
                          .Select(x => new { x.Age, x.Shoes });
            AssertRejected(q.Expression);
        }

        [TestMethod]
        public void Marker_in_an_earlier_select_followed_by_a_filtering_terminal_is_rejected()
        {
            // First(predicate) filters, so it is not a plain terminal
            var q = People.Select(x => new { x.Age, Shoes = x.Shoes.ToList() });
            var predicate = Expression.Lambda(Expression.Constant(true), Expression.Parameter(q.ElementType, "r"));
            var first = Expression.Call(typeof(System.Linq.Queryable), nameof(System.Linq.Queryable.First), new[] { q.ElementType }, q.Expression, Expression.Quote(predicate));
            var ex = AssertRejected(first);
            StringAssert.Contains(ex.Message, "followed by 'First'");
        }

        [TestMethod]
        public void Marker_nested_in_another_marker_is_rejected()
        {
            var q = People.Select(x => new
            {
                x.Age,
                Shoes = x.Shoes.Select(s => new { s.Brand, Same = People.Where(p => p.Id == s.PersonId).Select(p => p.Shoes.ToList()).ToList() }).ToList()
            });
            var ex = AssertRejected(q.Expression);
            StringAssert.Contains(ex.Message, "inside another child collection");
        }

        [TestMethod]
        public void Plain_sub_query_in_an_earlier_select_is_left_alone()
        {
            // not a marker: the sub-query stays inside the SQL and is used by Where
            var q = People.Select(x => new { x.Age, Shoes = x.Shoes.Where(s => s.Brand == "a") })
                          .Where(x => x.Shoes.Any())
                          .Take(3);
            AssertAllowed(q.Expression);
        }

        [TestMethod]
        public void Marker_outside_a_select_is_not_this_rules_business()
        {
            var q = People.Where(x => x.Shoes.ToList().Count > 1);
            AssertAllowed(q.Expression);
        }

        [TestMethod]
        public void Query_without_a_marker_is_allowed()
        {
            var q = People.Select(x => new { x.Age }).Where(x => x.Age > 1);
            AssertAllowed(q.Expression);
        }
    }
}
