using Atis.SqlExpressionEngine.Internal;
using Atis.SqlExpressionEngine.Services;
using Atis.SqlExpressionEngine.SqlExpressions;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     A child collection selected in a projection with <c>ToList()</c> / <c>ToArray()</c>. The call is a marker:
    ///     it must translate to the same sub-query column as the bare query does, and only the member's CLR type
    ///     (a list instead of a lazy query) differs. <see cref="ChildQueryExtractor"/> then splits the final query
    ///     into the query for the main rows and one query per child collection.
    /// </summary>
    [TestClass]
    public class ChildCollectionProjectionTests : TestBase
    {
        private readonly Queryable<Employee> employees = new Queryable<Employee>(new QueryProvider());
        private readonly Queryable<EmployeeDegree> employeeDegrees = new Queryable<EmployeeDegree>(new QueryProvider());

        #region translation of the marker

        [TestMethod]
        public void ToList_on_a_navigation_in_projection_translates_to_a_queryable_column()
        {
            var q = employees.Select(x => new { x.EmployeeId, Degrees = x.NavDegrees.ToList() });
            string expectedResult = @"
select a_1.EmployeeId as EmployeeId, Queryable: {
		(
				select a_2.RowId as RowId, a_2.EmployeeId as EmployeeId, a_2.Degree as Degree, a_2.University as University
				from EmployeeDegree as a_2
				where (a_1.EmployeeId = a_2.EmployeeId)
			)
		} as Degrees
from Employee as a_1
";
            Test("ToList in projection", q.Expression, expectedResult);
        }

        [TestMethod]
        public void ToArray_with_Where_on_a_navigation_in_projection_translates_to_a_queryable_column()
        {
            var q = employees.Select(x => new { x.EmployeeId, Degrees = x.NavDegrees.Where(d => d.University == "MIT").ToArray() });
            string expectedResult = @"
select a_1.EmployeeId as EmployeeId, Queryable: {
		(
				select a_2.RowId as RowId, a_2.EmployeeId as EmployeeId, a_2.Degree as Degree, a_2.University as University
				from EmployeeDegree as a_2
				where (a_1.EmployeeId = a_2.EmployeeId) and (a_2.University = 'MIT')
			)
		} as Degrees
from Employee as a_1
";
            Test("ToArray with Where in projection", q.Expression, expectedResult);
        }

        #endregion

        #region splitting the final query

        private ChildQueryExtractionResult Split(System.Linq.Expressions.Expression expression)
        {
            var root = (SqlDerivedTableExpression)ConvertExpressionToSqlExpression(expression, out _)!;
            return new ChildQueryExtractor(new SqlExpressionFactory()).Extract(root);
        }

        private static string Print(SqlDerivedTableExpression query) => new SqlExpressionTranslator().Translate(query);

        [TestMethod]
        public void Query_without_a_child_collection_is_not_split()
        {
            var q = employees.Select(x => new { x.EmployeeId, DegreeCount = x.NavDegrees.Count() });
            var root = (SqlDerivedTableExpression)ConvertExpressionToSqlExpression(q.Expression, out _)!;

            var result = new ChildQueryExtractor(new SqlExpressionFactory()).Extract(root);

            Assert.AreSame(root, result.ParentQuery);
            Assert.AreEqual(0, result.ChildQueries.Count);
        }

        [TestMethod]
        public void Child_collection_is_loaded_by_a_query_joined_to_the_distinct_link_values_of_the_parent()
        {
            var q = employees.Select(x => new { x.Name, Degrees = x.NavDegrees.ToList() });

            var result = Split(q.Expression);

            ValidateQueryResults(Print(result.ParentQuery), @"
select a_1.EmployeeId as ChildKey1, a_1.Name as Name
from Employee as a_1
");
            Assert.AreEqual(1, result.ChildQueries.Count);
            var child = result.ChildQueries[0];
            Assert.AreEqual(1, child.ParentKeyColumns.Count);
            ValidateQueryResults(Print(child.Query), @"
select NavDegrees_4.RowId as RowId, NavDegrees_4.EmployeeId as EmployeeId, NavDegrees_4.Degree as Degree, NavDegrees_4.University as University, a_3.ChildKey1 as Key1
from (
		select distinct a_2.ChildKey1 as ChildKey1
		from (
				select a_1.EmployeeId as ChildKey1, a_1.Name as Name
				from Employee as a_1
			) as a_2
	) as a_3
	inner join EmployeeDegree as NavDegrees_4 on (a_3.ChildKey1 = NavDegrees_4.EmployeeId)
");
        }

        [TestMethod]
        public void Link_column_is_selected_again_as_a_hidden_first_column_even_when_the_parent_selects_it()
        {
            // The link values are read before the object is constructed, and a sequential reader only reads
            // ascending, so they cannot share the column the object's own member is read from.
            var q = employees.Select(x => new { x.EmployeeId, Degrees = x.NavDegrees.ToList() });

            var result = Split(q.Expression);

            ValidateQueryResults(Print(result.ParentQuery), @"
select a_1.EmployeeId as ChildKey1, a_1.EmployeeId as EmployeeId
from Employee as a_1
");
            Assert.AreEqual(1, result.ChildQueries[0].ParentKeyColumns.Count);
        }

        [TestMethod]
        public void Parent_filter_order_and_top_are_kept_inside_the_child_query()
        {
            var q = employees.Where(x => x.Name != "z")
                             .OrderBy(x => x.Name)
                             .Take(5)
                             .Select(x => new { x.Name, Degrees = x.NavDegrees.Where(d => d.University == "MIT").ToList() });

            var result = Split(q.Expression);

            ValidateQueryResults(Print(result.ParentQuery), @"
select a_2.EmployeeId as ChildKey1, a_2.Name as Name
from (
		select top (5) a_1.RowId as RowId, a_1.EmployeeId as EmployeeId, a_1.Name as Name, a_1.Department as Department, a_1.ManagerId as ManagerId
		from Employee as a_1
		where (a_1.Name <> 'z')
		order by a_1.Name asc
	) as a_2
");
            ValidateQueryResults(Print(result.ChildQueries[0].Query), @"
select NavDegrees_5.RowId as RowId, NavDegrees_5.EmployeeId as EmployeeId, NavDegrees_5.Degree as Degree, NavDegrees_5.University as University, a_4.ChildKey1 as Key1
from (
		select distinct a_3.ChildKey1 as ChildKey1
		from (
				select a_2.EmployeeId as ChildKey1, a_2.Name as Name
				from (
						select top (5) a_1.RowId as RowId, a_1.EmployeeId as EmployeeId, a_1.Name as Name, a_1.Department as Department, a_1.ManagerId as ManagerId
						from Employee as a_1
						where (a_1.Name <> 'z')
						order by a_1.Name asc
					) as a_2
			) as a_3
	) as a_4
	inner join (
		select a_6.RowId as RowId, a_6.EmployeeId as EmployeeId, a_6.Degree as Degree, a_6.University as University
		from EmployeeDegree as a_6
		where (a_6.University = 'MIT')
	) as NavDegrees_5 on (a_4.ChildKey1 = NavDegrees_5.EmployeeId)
");
        }

        [TestMethod]
        public void Top_without_order_by_on_a_table_with_a_primary_key_is_ordered_by_the_key_in_parent_and_child()
        {
            var people = new Queryable<Person>(new QueryProvider());
            var q = people.Take(3)
                          .Select(p => new { p.Age, Same = people.Where(k => k.Age == p.Age).ToList() });

            var result = Split(q.Expression);

            var parent = Print(result.ParentQuery);
            var child = Print(result.ChildQueries[0].Query);
            StringAssert.Contains(parent.ToLower().Replace(" ", ""), "orderbya_1.idasc");
            StringAssert.Contains(child.ToLower().Replace(" ", ""), "orderbya_1.idasc");
            Console.WriteLine(parent);
            Console.WriteLine(child);
        }

        [TestMethod]
        public void Top_with_an_order_by_gets_the_primary_key_as_a_tie_breaker()
        {
            var people = new Queryable<Person>(new QueryProvider());
            var q = people.OrderBy(p => p.Age)
                          .Take(3)
                          .Select(p => new { p.Age, Same = people.Where(k => k.Age == p.Age).ToList() });

            var result = Split(q.Expression);

            var parent = Print(result.ParentQuery).ToLower().Replace(" ", "");
            StringAssert.Contains(parent, "orderbya_1.ageasc,a_1.idasc");
        }

        [TestMethod]
        public void Top_without_order_by_on_a_query_that_cannot_be_ordered_by_a_key_is_refused()
        {
            // Employee has no primary key in the test model, so nothing can make its Top repeatable
            var q = employees.Take(5).Select(x => new { x.Name, Degrees = x.NavDegrees.ToList() });

            var exception = Assert.ThrowsException<NotSupportedException>(() => Split(q.Expression));
            StringAssert.Contains(exception.Message, "OrderBy");
        }

        [TestMethod]
        public void Child_collection_that_is_not_linked_to_the_parent_is_loaded_by_the_plain_query()
        {
            var q = employees.Select(x => new { x.Name, All = employeeDegrees.ToList() });

            var result = Split(q.Expression);

            ValidateQueryResults(Print(result.ParentQuery), @"
select a_1.Name as Name
from Employee as a_1
");
            Assert.AreEqual(0, result.ChildQueries[0].ParentKeyColumns.Count);
            ValidateQueryResults(Print(result.ChildQueries[0].Query), @"
select a_1.RowId as RowId, a_1.EmployeeId as EmployeeId, a_1.Degree as Degree, a_1.University as University
from EmployeeDegree as a_1
");
        }

        [TestMethod]
        public void Two_child_collections_get_one_query_each()
        {
            var q = employees.Select(x => new
            {
                x.Name,
                Degrees = x.NavDegrees.ToList(),
                Known = x.NavDegrees.Where(d => d.Degree != null).ToArray()
            });

            var result = Split(q.Expression);

            Assert.AreEqual(2, result.ChildQueries.Count);
            // both are linked through the same hidden column, which the parent selects only once
            ValidateQueryResults(Print(result.ParentQuery), @"
select a_1.EmployeeId as ChildKey1, a_1.Name as Name
from Employee as a_1
");
            Assert.AreEqual(1, result.ChildQueries[0].ParentKeyColumns.Count);
            Assert.AreEqual(1, result.ChildQueries[1].ParentKeyColumns.Count);
            Assert.AreSame(result.ChildQueries[0].ParentKeyColumns[0], result.ChildQueries[1].ParentKeyColumns[0]);
        }

        #endregion

        #region child collection that selects less than the whole row

        // The join condition has to read the child's link column, so when the child does not select it the extractor
        // adds it to the child's select list as a helper column (SubQueryCol1). A scalar child must still be a scalar.

        [TestMethod]
        public void Scalar_child_is_the_item_and_the_link_value_comes_from_the_keys()
        {
            var q = employees.Select(x => new { x.Name, Universities = x.NavDegrees.Select(d => d.University).ToList() });

            var result = Split(q.Expression);

            ValidateQueryResults(Print(result.ParentQuery), @"
select a_1.EmployeeId as ChildKey1, a_1.Name as Name
from Employee as a_1
");
            Assert.AreEqual(1, result.ChildQueries[0].ParentKeyColumns.Count);
            ValidateQueryResults(Print(result.ChildQueries[0].Query), @"
select NavDegrees_4.Col1 as Item, a_3.ChildKey1 as Key1
from (
		select distinct a_2.ChildKey1 as ChildKey1
		from (
				select a_1.EmployeeId as ChildKey1, a_1.Name as Name
				from Employee as a_1
			) as a_2
	) as a_3
	inner join (
		select a_5.University as Col1, a_5.EmployeeId as SubQueryCol1
		from EmployeeDegree as a_5
	) as NavDegrees_4 on (a_3.ChildKey1 = NavDegrees_4.SubQueryCol1)
");
        }

        [TestMethod]
        public void Columns_added_only_to_link_the_child_are_marked_as_helper_columns()
        {
            var q = employees.Select(x => new { x.Name, Universities = x.NavDegrees.Select(d => d.University).ToList() });

            var result = Split(q.Expression);

            var parentColumns = result.ParentQuery.SelectColumnCollection.SelectColumns;
            Assert.AreEqual("ChildKey1", parentColumns[0].Alias);
            Assert.IsTrue(parentColumns[0].HelperColumn);
            Assert.IsTrue(parentColumns.Skip(1).All(x => !x.HelperColumn));

            // the join condition needed the child's link column, so it was added to the child's select list next to the scalar one
            var joinedSource = result.ChildQueries[0].Query.Joins.Single().QuerySource as SqlDerivedTableExpression;
            Assert.IsNotNull(joinedSource);
            var columns = joinedSource.SelectColumnCollection.SelectColumns;
            Assert.AreEqual(2, columns.Count);
            Assert.IsFalse(columns[0].HelperColumn);
            Assert.IsTrue(columns[1].HelperColumn);
            Assert.IsTrue(joinedSource.SelectColumnCollection.IsScalar);
        }

        [TestMethod]
        public void Scalar_child_keeps_its_own_filter()
        {
            var q = employees.Select(x => new { x.Name, Universities = x.NavDegrees.Where(d => d.Degree != null).Select(d => d.University).ToList() });

            var result = Split(q.Expression);

            ValidateQueryResults(Print(result.ChildQueries[0].Query), @"
select NavDegrees_4.Col1 as Item, a_3.ChildKey1 as Key1
from (
		select distinct a_2.ChildKey1 as ChildKey1
		from (
				select a_1.EmployeeId as ChildKey1, a_1.Name as Name
				from Employee as a_1
			) as a_2
	) as a_3
	inner join (
		select a_5.University as Col1, a_5.EmployeeId as SubQueryCol1
		from EmployeeDegree as a_5
		where (a_5.Degree is not null)
	) as NavDegrees_4 on (a_3.ChildKey1 = NavDegrees_4.SubQueryCol1)
");
        }

        [TestMethod]
        public void Scalar_child_that_selects_the_link_column_needs_no_helper_column()
        {
            var q = employees.Select(x => new { x.Name, Ids = x.NavDegrees.Select(d => d.EmployeeId).ToList() });

            var result = Split(q.Expression);

            ValidateQueryResults(Print(result.ChildQueries[0].Query), @"
select NavDegrees_4.Col1 as Item, a_3.ChildKey1 as Key1
from (
		select distinct a_2.ChildKey1 as ChildKey1
		from (
				select a_1.EmployeeId as ChildKey1, a_1.Name as Name
				from Employee as a_1
			) as a_2
	) as a_3
	inner join (
		select a_5.EmployeeId as Col1
		from EmployeeDegree as a_5
	) as NavDegrees_4 on (a_3.ChildKey1 = NavDegrees_4.Col1)
");
        }

        [TestMethod]
        public void Computed_scalar_child_is_the_item()
        {
            var q = employees.Select(x => new { x.Name, Universities = x.NavDegrees.Select(d => d.University + "!").ToList() });

            var result = Split(q.Expression);

            ValidateQueryResults(Print(result.ChildQueries[0].Query), @"
select NavDegrees_4.Col1 as Item, a_3.ChildKey1 as Key1
from (
		select distinct a_2.ChildKey1 as ChildKey1
		from (
				select a_1.EmployeeId as ChildKey1, a_1.Name as Name
				from Employee as a_1
			) as a_2
	) as a_3
	inner join (
		select (a_5.University + '!') as Col1, a_5.EmployeeId as SubQueryCol1
		from EmployeeDegree as a_5
	) as NavDegrees_4 on (a_3.ChildKey1 = NavDegrees_4.SubQueryCol1)
");
        }

        [TestMethod]
        public void Child_projected_to_an_anonymous_type_is_joined_on_the_link_value()
        {
            var q = employees.Select(x => new { x.Name, Items = x.NavDegrees.Select(d => new { d.University, d.Degree }).ToList() });

            var result = Split(q.Expression);

            // Only the parts that matter are checked. The helper column also appears in the select list of the child
            // query, next to the item members, and it is not yet known whether materialization is fine with that.
            var child = System.Text.RegularExpressions.Regex.Replace(Print(result.ChildQueries[0].Query).ToLower(), @"\s+", "");
            StringAssert.Contains(child, "a_3.childkey1askey1");
            StringAssert.Contains(child, "innerjoin(selecta_5.universityasuniversity,a_5.degreeasdegree,a_5.employeeidassubquerycol1");
            StringAssert.Contains(child, "on(a_3.childkey1=navdegrees_4.subquerycol1)");
        }

        [TestMethod]
        public void Scalar_child_that_is_not_linked_to_the_parent_is_loaded_by_the_plain_query()
        {
            var q = employees.Select(x => new { x.Name, All = employeeDegrees.Select(d => d.University).ToList() });

            var result = Split(q.Expression);

            ValidateQueryResults(Print(result.ParentQuery), @"
select a_1.Name as Name
from Employee as a_1
");
            Assert.AreEqual(0, result.ChildQueries[0].ParentKeyColumns.Count);
            ValidateQueryResults(Print(result.ChildQueries[0].Query), @"
select a_2.Col1 as Item
from (
		select a_1.University as Col1
		from EmployeeDegree as a_1
	) as a_2
");
        }

        [TestMethod]
        public void Scalar_child_with_distinct_cannot_be_joined_and_is_loaded_with_cross_apply()
        {
            var q = employees.Select(x => new { x.Name, Universities = x.NavDegrees.Select(d => d.University).Distinct().ToList() });

            var result = Split(q.Expression);

            ValidateQueryResults(Print(result.ChildQueries[0].Query), @"
select a_4.Col1 as Item, a_3.ChildKey1 as Key1
from (
		select distinct a_2.ChildKey1 as ChildKey1
		from (
				select a_1.EmployeeId as ChildKey1, a_1.Name as Name
				from Employee as a_1
			) as a_2
	) as a_3
	cross apply (
		select distinct a_5.University as Col1
		from EmployeeDegree as a_5
		where (a_3.ChildKey1 = a_5.EmployeeId)
	) as a_4
");
        }

        [TestMethod]
        public void Scalar_child_with_take_cannot_be_joined_and_is_loaded_with_cross_apply()
        {
            var q = employees.Select(x => new { x.Name, Universities = x.NavDegrees.Select(d => d.University).Take(2).ToList() });

            var result = Split(q.Expression);

            ValidateQueryResults(Print(result.ChildQueries[0].Query), @"
select a_4.Col1 as Item, a_3.ChildKey1 as Key1
from (
		select distinct a_2.ChildKey1 as ChildKey1
		from (
				select a_1.EmployeeId as ChildKey1, a_1.Name as Name
				from Employee as a_1
			) as a_2
	) as a_3
	cross apply (
		select top (2) a_5.University as Col1
		from EmployeeDegree as a_5
		where (a_3.ChildKey1 = a_5.EmployeeId)
	) as a_4
");
        }

        [TestMethod]
        public void Child_collection_after_filter_order_and_top_keeps_them_in_the_parent_and_the_child()
        {
            var q = employees.Where(e => e.NavDegrees.Any(y => y.University == "123"))
                             .OrderBy(e => e.Name)
                             .Take(50)
                             .Select(e => new { e.Name, Degs = e.NavDegrees.ToList() });

            var result = Split(q.Expression);

            var parent = Squash(Print(result.ParentQuery));
            var child = Squash(Print(result.ChildQueries[0].Query));
            // the parent's filter, order and top sit inside a derived table, and the child repeats all of it
            StringAssert.Contains(parent, "selecttop(50)");
            StringAssert.Contains(parent, "whereexists(");
            StringAssert.Contains(parent, "orderbya_1.nameasc");
            StringAssert.Contains(child, "selecttop(50)");
            StringAssert.Contains(child, "orderbya_1.nameasc");
            StringAssert.Contains(child, "innerjoinemployeedegreeasnavdegrees_");
        }

        [TestMethod]
        public void Scalar_child_after_filter_order_and_top_is_joined_to_the_surviving_keys()
        {
            var people = new Queryable<Person>(new QueryProvider());
            var q = people.Where(p => p.Age > 5)
                          .OrderBy(p => p.Age)
                          .Take(3)
                          .Select(p => new { p.Age, Styles = p.NavShoes.Select(s => s.Style).ToList() });

            var result = Split(q.Expression);

            var parent = Squash(Print(result.ParentQuery));
            var child = Squash(Print(result.ChildQueries[0].Query));
            // the primary key is the tie-breaker, so both statements pick the same three rows
            StringAssert.Contains(parent, "selecttop(3)");
            StringAssert.Contains(parent, "orderbya_1.ageasc,a_1.idasc");
            StringAssert.Contains(child, "selecttop(3)");
            StringAssert.Contains(child, "orderbya_1.ageasc,a_1.idasc");
            StringAssert.Contains(child, "selectdistincta_3.childkey1");
        }

        private static string Squash(string sql) => sql.Replace(" ", "").Replace("\t", "").Replace("\r", "").Replace("\n", "").ToLowerInvariant();
        #endregion
    }
}
