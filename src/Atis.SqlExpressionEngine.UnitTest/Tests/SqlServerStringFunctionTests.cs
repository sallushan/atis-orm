using System.Linq;
using System.Linq.Expressions;

using Atis.Orm.SqlServer;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    [TestClass]
    public class SqlServerStringFunctionTests : TestBase
    {
        private string TranslateForSqlServer(Expression expression)
        {
            var sqlExpression = ConvertExpressionToSqlExpression(expression, out _);
            Assert.IsNotNull(sqlExpression, "Expected a non-null SqlExpression.");
            var translator = new SqlServerSqlExpressionTranslator();
            var sql = translator.Translate(sqlExpression).Sql;
            System.Console.WriteLine(sql);
            return sql;
        }

        [TestMethod]
        public void Scalar_string_Join_translates_to_CONCAT_WS()
        {
            var q = queryProvider.Select(() => new { C1 = string.Join(", ", new object[] { "1", 2, "3" }) });
            var sql = TranslateForSqlServer(q.Expression);
            StringAssert.Contains(sql, "CONCAT_WS(");
        }

        [TestMethod]
        public void Scalar_string_Concat_translates_to_CONCAT()
        {
            var q = queryProvider.Select(() => new { C1 = string.Concat("abc", "def") });
            var sql = TranslateForSqlServer(q.Expression);
            StringAssert.Contains(sql, "CONCAT(");
        }

        [TestMethod]
        public void Aggregate_string_Join_translates_to_STRING_AGG()
        {
            var employees = new Queryable<Employee>(queryProvider);
            var employeeDegrees = new Queryable<EmployeeDegree>(queryProvider);
            var q = from e in employees
                    join ed in employeeDegrees
                                    .GroupBy(x => x.EmployeeId)
                                    .Select(x => new { EmployeeId = x.Key, Degrees = string.Join(", ", x.Select(y => y.Degree)) })
                                    on e.EmployeeId equals ed.EmployeeId
                    select new { e.EmployeeId, e.Name, ed.Degrees };
            var sql = TranslateForSqlServer(q.Expression);
            StringAssert.Contains(sql, "STRING_AGG(");
        }

        [TestMethod]
        public void Scalar_string_functions_translate_to_tsql()
        {
            var students = new Queryable<Student>(queryProvider);
            var q = students.Select(x => new
            {
                C1 = x.Name.Trim(),
                C2 = x.Name.TrimStart(),
                C3 = x.Name.TrimEnd(),
                C4 = x.Name.ToUpper(),
                C5 = x.Name.ToLower(),
                C6 = x.Name.Replace("a", "b"),
                C7 = x.Name.Length,
                C8 = x.Name.Substring(4),
                C9 = x.Name.Substring(6, 3),
                C10 = x.Name.IndexOf("a"),
            });
            var sql = TranslateForSqlServer(q.Expression);

            StringAssert.Contains(sql, "TRIM(");
            StringAssert.Contains(sql, "LTRIM(");
            StringAssert.Contains(sql, "RTRIM(");
            StringAssert.Contains(sql, "UPPER(");
            StringAssert.Contains(sql, "LOWER(");
            StringAssert.Contains(sql, "REPLACE(");
            StringAssert.Contains(sql, "LEN(");
            StringAssert.Contains(sql, "SUBSTRING(");
            StringAssert.Contains(sql, ") + 1,");     // 0-based -> 1-based start adjustment
            StringAssert.Contains(sql, "CHARINDEX(");
            StringAssert.Contains(sql, ") - 1");       // 1-based CHARINDEX -> 0-based IndexOf adjustment
        }
    }
}
