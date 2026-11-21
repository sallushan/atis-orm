using Atis.SqlExpressionEngine.Internal;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     A sub-query held in the query shape (for example by a LINQ <c>let</c>) is joined as an OUTER APPLY
    ///     the first time one of its members is read. These tests pin the SQL of that lazy path.
    /// </summary>
    [TestClass]
    public class LazySubQueryJoinTests : TestBase
    {
        private IQueryable<Employee> employees = new Queryable<Employee>(new QueryProvider());
        private IQueryable<EmployeeDegree> degrees = new Queryable<EmployeeDegree>(new QueryProvider());

        public TestContext TestContext { get; set; } = null!;

        /// <summary>
        ///     Translates the query, writes the generated SQL to the test result (shown in Visual Studio Test Explorer),
        ///     then compares it with the expected SQL.
        /// </summary>
        private void CheckSql(string heading, System.Linq.Expressions.Expression queryExpression, string expectedResult)
        {
            var sqlExpression = this.ConvertExpressionToSqlExpression(queryExpression, out _);
            var generatedSql = new SqlExpressionTranslator().Translate(sqlExpression!);
            this.TestContext.WriteLine("---- " + heading + " ----");
            this.TestContext.WriteLine(generatedSql);
            ValidateQueryResults(generatedSql, expectedResult);
        }

        [TestMethod]
        public void Let_then_Where_on_sub_query_member()
        {
            var q = from e in employees
                    let d = degrees.Where(x => x.EmployeeId == e.EmployeeId).OrderBy(x => x.University).FirstOrDefault()
                    where d.Degree == "PhD"
                    select e.Name;
            string expectedResult = @"
select a_1.Name as Col1
from Employee as a_1
		outer apply (
			select top (1) a_3.RowId as RowId, a_3.EmployeeId as EmployeeId, a_3.Degree as Degree, a_3.University as University
			from EmployeeDegree as a_3
			where (a_3.EmployeeId = a_1.EmployeeId)
			order by a_3.University asc
		) as d_2
where (d_2.Degree = 'PhD')
";
            CheckSql("let then where", q.Expression, expectedResult);
        }

        [TestMethod]
        public void Let_then_Select_on_sub_query_member()
        {
            var q = from e in employees
                    let d = degrees.Where(x => x.EmployeeId == e.EmployeeId).OrderBy(x => x.University).FirstOrDefault()
                    select new { e.Name, d.Degree };
            string expectedResult = @"
select a_1.Name as Name, d_2.Degree as Degree
from Employee as a_1
		outer apply (
			select top (1) a_3.RowId as RowId, a_3.EmployeeId as EmployeeId, a_3.Degree as Degree, a_3.University as University
			from EmployeeDegree as a_3
			where (a_3.EmployeeId = a_1.EmployeeId)
			order by a_3.University asc
		) as d_2
";
            CheckSql("let then select", q.Expression, expectedResult);
        }

        [TestMethod]
        public void Let_then_OrderBy_on_sub_query_member()
        {
            var q = from e in employees
                    let d = degrees.Where(x => x.EmployeeId == e.EmployeeId).OrderBy(x => x.University).FirstOrDefault()
                    orderby d.Degree
                    select e.Name;
            string expectedResult = @"
select a_1.Name as Col1
from Employee as a_1
		outer apply (
			select top (1) a_3.RowId as RowId, a_3.EmployeeId as EmployeeId, a_3.Degree as Degree, a_3.University as University
			from EmployeeDegree as a_3
			where (a_3.EmployeeId = a_1.EmployeeId)
			order by a_3.University asc
		) as d_2
order by d_2.Degree asc
";
            CheckSql("let then orderby", q.Expression, expectedResult);
        }

        [TestMethod]
        public void Where_then_let_then_Where_on_sub_query_member()
        {
            var q = from e in employees.Where(x => x.Name != "a")
                    let d = degrees.Where(x => x.EmployeeId == e.EmployeeId).OrderBy(x => x.University).FirstOrDefault()
                    where d.Degree == "PhD"
                    select e.Name;
            string expectedResult = @"
select a_1.Name as Col1
from Employee as a_1
		outer apply (
			select top (1) a_3.RowId as RowId, a_3.EmployeeId as EmployeeId, a_3.Degree as Degree, a_3.University as University
			from EmployeeDegree as a_3
			where (a_3.EmployeeId = a_1.EmployeeId)
			order by a_3.University asc
		) as d_2
where (a_1.Name <> 'a')
	 and (d_2.Degree = 'PhD')
";
            CheckSql("where, let, where", q.Expression, expectedResult);
        }

        [TestMethod]
        public void An_unused_let_adds_no_join()
        {
            var q = from e in employees
                    let d = degrees.Where(x => x.EmployeeId == e.EmployeeId).OrderBy(x => x.University).FirstOrDefault()
                    where e.Name == "x"
                    select e.Name;
            string expectedResult = @"
select a_1.Name as Col1
from Employee as a_1
where (a_1.Name = 'x')
";
            CheckSql("let unused", q.Expression, expectedResult);
        }

        [TestMethod]
        public void Two_lets_each_read_once()
        {
            var q = from e in employees
                    let d1 = degrees.Where(x => x.EmployeeId == e.EmployeeId).OrderBy(x => x.University).FirstOrDefault()
                    let d2 = degrees.Where(x => x.EmployeeId == e.EmployeeId).OrderByDescending(x => x.University).FirstOrDefault()
                    where d1.Degree == "PhD" && d2.Degree == "MS"
                    select e.Name;
            string expectedResult = @"
select a_1.Name as Col1
from Employee as a_1
		outer apply (
			select top (1) a_3.RowId as RowId, a_3.EmployeeId as EmployeeId, a_3.Degree as Degree, a_3.University as University
			from EmployeeDegree as a_3
			where (a_3.EmployeeId = a_1.EmployeeId)
			order by a_3.University asc
		) as d1_2
		outer apply (
			select top (1) a_5.RowId as RowId, a_5.EmployeeId as EmployeeId, a_5.Degree as Degree, a_5.University as University
			from EmployeeDegree as a_5
			where (a_5.EmployeeId = a_1.EmployeeId)
			order by a_5.University desc
		) as d2_4
where ((d1_2.Degree = 'PhD') and (d2_4.Degree = 'MS'))
";
            CheckSql("two lets", q.Expression, expectedResult);
        }

        [TestMethod]
        public void Sub_query_read_from_an_inner_lambda_joins_the_outer_query()
        {
            var q = from e in employees
                    let d = degrees.Where(x => x.EmployeeId == e.EmployeeId).OrderBy(x => x.University).FirstOrDefault()
                    where degrees.Any(x => x.EmployeeId == e.EmployeeId && x.Degree == d.Degree)
                    select e.Name;
            string expectedResult = @"
select a_1.Name as Col1
from Employee as a_1
		outer apply (
			select top (1) a_3.RowId as RowId, a_3.EmployeeId as EmployeeId, a_3.Degree as Degree, a_3.University as University
			from EmployeeDegree as a_3
			where (a_3.EmployeeId = a_1.EmployeeId)
			order by a_3.University asc
		) as d_2
where exists(
		select 1 as Col1
		from EmployeeDegree as a_4
		where ((a_4.EmployeeId = a_1.EmployeeId) and (a_4.Degree = d_2.Degree))
	)
";
            CheckSql("inner lambda", q.Expression, expectedResult);
        }

        [TestMethod]
        public void Take_then_let_then_Where_reading_the_sub_query_first()
        {
            var q = from e in employees.Take(5)
                    let d = degrees.Where(x => x.EmployeeId == e.EmployeeId).OrderBy(x => x.University).FirstOrDefault()
                    where d.Degree == "PhD" && e.Name == "x"
                    select e.Name;
            string expectedResult = @"
select a_2.Name as Col1
from (
		select top (5) a_1.RowId as RowId, a_1.EmployeeId as EmployeeId, a_1.Name as Name, a_1.Department as Department, a_1.ManagerId as ManagerId
		from Employee as a_1
	) as a_2
		outer apply (
			select top (1) a_4.RowId as RowId, a_4.EmployeeId as EmployeeId, a_4.Degree as Degree, a_4.University as University
			from EmployeeDegree as a_4
			where (a_4.EmployeeId = a_2.EmployeeId)
			order by a_4.University asc
		) as d_3
where ((d_3.Degree = 'PhD') and (a_2.Name = 'x'))
";
            CheckSql("take, let, where (sub-query first)", q.Expression, expectedResult);
        }

        // e.Name is converted before d.Degree triggers the wrap, so it was built with the old alias (a_1).
        // The query re-points it when the predicate is applied.
        [TestMethod]
        public void Take_then_let_then_Where_reading_the_sub_query_second()
        {
            var q = from e in employees.Take(5)
                    let d = degrees.Where(x => x.EmployeeId == e.EmployeeId).OrderBy(x => x.University).FirstOrDefault()
                    where e.Name == "x" && d.Degree == "PhD"
                    select e.Name;
            string expectedResult = @"
select a_2.Name as Col1
from (
		select top (5) a_1.RowId as RowId, a_1.EmployeeId as EmployeeId, a_1.Name as Name, a_1.Department as Department, a_1.ManagerId as ManagerId
		from Employee as a_1
	) as a_2
		outer apply (
			select top (1) a_4.RowId as RowId, a_4.EmployeeId as EmployeeId, a_4.Degree as Degree, a_4.University as University
			from EmployeeDegree as a_4
			where (a_4.EmployeeId = a_2.EmployeeId)
			order by a_4.University asc
		) as d_3
where ((a_2.Name = 'x') and (d_3.Degree = 'PhD'))
";
            CheckSql("take, let, where (sub-query second)", q.Expression, expectedResult);
        }
    }
}
