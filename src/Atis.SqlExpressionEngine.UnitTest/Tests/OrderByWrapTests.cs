namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     An ORDER BY inside a derived table is only valid with TOP, OFFSET or FETCH. When an operation would wrap a
    ///     query that has an ORDER BY and none of those, the engine says so instead of producing SQL that SQL Server
    ///     rejects. The wrap rules themselves are not changed by these tests.
    /// </summary>
    [TestClass]
    public class OrderByWrapTests : TestBase
    {
        private IQueryable<Employee> employees = new Queryable<Employee>(new QueryProvider());
        private IQueryable<EmployeeDegree> degrees = new Queryable<EmployeeDegree>(new QueryProvider());

        public TestContext TestContext { get; set; } = null!;

        private void CheckSql(string heading, System.Linq.Expressions.Expression queryExpression, string expectedResult)
        {
            var sqlExpression = this.ConvertExpressionToSqlExpression(queryExpression, out _);
            var generatedSql = new SqlExpressionTranslator().Translate(sqlExpression!);
            this.TestContext.WriteLine("---- " + heading + " ----");
            this.TestContext.WriteLine(generatedSql);
            ValidateQueryResults(generatedSql, expectedResult);
        }

        private NotSupportedException Refused(System.Linq.Expressions.Expression queryExpression)
        {
            return Assert.ThrowsException<NotSupportedException>(() => this.ConvertExpressionToSqlExpression(queryExpression, out _));
        }

        // ---- refused: OrderBy, then something that wraps ----

        [TestMethod]
        public void OrderBy_then_Join_is_refused()
        {
            var q = employees.OrderBy(e => e.EmployeeId).Join(degrees, e => e.EmployeeId, d => d.EmployeeId, (e, d) => new { e.Name, d.Degree });

            var ex = Refused(q.Expression);

            StringAssert.Contains(ex.Message, "OrderBy");
            StringAssert.Contains(ex.Message, "Join");
            StringAssert.Contains(ex.Message, "derived table");
            StringAssert.Contains(ex.Message, "Move OrderBy to the end");
        }

        [TestMethod]
        public void OrderBy_Select_then_Where_is_refused()
        {
            var q = employees.OrderBy(e => e.EmployeeId).Select(p => new { p.EmployeeId, p.Name }).Where(r => r.Name == "x");

            var ex = Refused(q.Expression);

            StringAssert.Contains(ex.Message, "Where");
        }

        [TestMethod]
        public void OrderBy_Select_then_Select_is_refused()
        {
            var q = employees.OrderBy(e => e.EmployeeId).Select(p => new { p.EmployeeId, p.Name }).Select(r => new { r.Name });

            var ex = Refused(q.Expression);

            StringAssert.Contains(ex.Message, "Select");
        }

        [TestMethod]
        public void Where_OrderBy_then_Select_with_a_sub_query_apply_is_refused()
        {
            var q = employees.Where(e => e.Name == "x")
                             .OrderBy(e => e.EmployeeId)
                             .Select(p => new
                             {
                                 p.EmployeeId,
                                 Latest = degrees.Where(s => s.EmployeeId == p.EmployeeId).OrderByDescending(s => s.University).FirstOrDefault()
                             });

            var ex = Refused(q.Expression);

            StringAssert.Contains(ex.Message, "OrderBy");
        }

        // ---- not refused: the same queries written so that no wrap is needed, or the ORDER BY is legal ----

        [TestMethod]
        public void OrderBy_after_the_sub_query_apply_works()
        {
            var q = employees.Where(e => e.Name == "x")
                             .Select(p => new
                             {
                                 p.EmployeeId,
                                 Latest = degrees.Where(s => s.EmployeeId == p.EmployeeId).OrderByDescending(s => s.University).FirstOrDefault()
                             })
                             .OrderBy(r => r.EmployeeId);
            string expectedResult = @"
select a_2.EmployeeId as EmployeeId, Latest_3.RowId as RowId, Latest_3.EmployeeId as EmployeeId_1, Latest_3.Degree as Degree, Latest_3.University as University
from (
		select a_1.RowId as RowId, a_1.EmployeeId as EmployeeId, a_1.Name as Name, a_1.Department as Department, a_1.ManagerId as ManagerId
		from Employee as a_1
		where (a_1.Name = 'x')
	) as a_2
		outer apply (
			select top (1) a_4.RowId as RowId, a_4.EmployeeId as EmployeeId, a_4.Degree as Degree, a_4.University as University
			from EmployeeDegree as a_4
			where (a_4.EmployeeId = a_2.EmployeeId)
			order by a_4.University desc
		) as Latest_3
order by EmployeeId asc
";
            CheckSql("where, select+apply, orderby", q.Expression, expectedResult);
        }

        [TestMethod]
        public void OrderBy_with_Take_then_Where_works_because_TOP_makes_the_order_legal()
        {
            var q = employees.OrderBy(e => e.EmployeeId).Take(5).Where(r => r.Name == "x");
            string expectedResult = @"
select a_2.RowId as RowId, a_2.EmployeeId as EmployeeId, a_2.Name as Name, a_2.Department as Department, a_2.ManagerId as ManagerId
from (
		select top (5) a_1.RowId as RowId, a_1.EmployeeId as EmployeeId, a_1.Name as Name, a_1.Department as Department, a_1.ManagerId as ManagerId
		from Employee as a_1
		order by a_1.EmployeeId asc
	) as a_2
where (a_2.Name = 'x')
";
            CheckSql("orderby, take, where", q.Expression, expectedResult);
        }

        [TestMethod]
        public void OrderBy_then_Where_needs_no_wrap_and_works()
        {
            var q = employees.OrderBy(e => e.EmployeeId).Where(r => r.Name == "x");
            string expectedResult = @"
select a_1.RowId as RowId, a_1.EmployeeId as EmployeeId, a_1.Name as Name, a_1.Department as Department, a_1.ManagerId as ManagerId
from Employee as a_1
where (a_1.Name = 'x')
order by a_1.EmployeeId asc
";
            CheckSql("orderby, where", q.Expression, expectedResult);
        }

        [TestMethod]
        public void OrderBy_then_Select_needs_no_wrap_and_works()
        {
            var q = employees.OrderBy(e => e.EmployeeId).Select(p => new { p.EmployeeId, p.Name });
            string expectedResult = @"
select a_1.EmployeeId as EmployeeId, a_1.Name as Name
from Employee as a_1
order by a_1.EmployeeId asc
";
            CheckSql("orderby, select", q.Expression, expectedResult);
        }
    }
}
