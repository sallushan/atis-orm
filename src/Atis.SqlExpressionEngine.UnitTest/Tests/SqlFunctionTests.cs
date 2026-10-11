using System.Linq;
using System.Linq.Expressions;

using Atis.Orm;
using Atis.Orm.Functions;
using Atis.Orm.SqlServer;
using Atis.Orm.Translation;
using Atis.SqlExpressionEngine.SqlExpressions;
using Atis.SqlExpressionEngine.Visitors;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    [TestClass]
    public class SqlFunctionTests : TestBase
    {
        private string TranslateForSqlServer(Expression expression)
        {
            var sqlExpression = ConvertExpressionToSqlExpression(expression, out _);
            Assert.IsNotNull(sqlExpression, "Expected a non-null SqlExpression.");
            var translation = new SqlServerSqlExpressionTranslator().Translate(sqlExpression);
            var nameGenerator = new SqlDbParameterNameGenerator();
            var renderer = new CommandRenderer(new SqlDbParameterFactory(nameGenerator));
            var sql = renderer.Render(translation.Fragments, p => p.InitialValue).Sql;
            System.Console.WriteLine(sql);
            return sql;
        }

        #region node hashing

        [TestMethod]
        public void Hash_tells_apart_template_functions_that_differ_only_in_text()
        {
            var day = SqlFunctionTemplate.Parse("dateadd(day, {0})", 1, "DateAdd");
            var month = SqlFunctionTemplate.Parse("dateadd(month, {0})", 1, "DateAdd");
            var arg = new SqlLiteralExpression(1);
            var a = new SqlTemplateFunctionExpression("DateAdd", day.Parts, new SqlExpression[] { arg }, false);
            var b = new SqlTemplateFunctionExpression("DateAdd", month.Parts, new SqlExpression[] { arg }, false);
            var a2 = new SqlTemplateFunctionExpression("DateAdd", day.Parts, new SqlExpression[] { arg }, false);

            Assert.AreNotEqual(SqlExpressionHashGenerator.GenerateHash(a), SqlExpressionHashGenerator.GenerateHash(b));
            Assert.AreEqual(SqlExpressionHashGenerator.GenerateHash(a), SqlExpressionHashGenerator.GenerateHash(a2));
        }

        [TestMethod]
        public void Hash_tells_apart_window_functions_by_direction_and_clause()
        {
            var col = new SqlLiteralExpression(1);
            SqlExpression Window(SqlOverClauseExpression over)
                => new SqlWindowFunctionExpression(SqlWindowFunction.RowNumber, new SqlExpression[0], over);

            var asc = Window(new SqlOverClauseExpression().AddOrderBy(col, SortDirection.Ascending));
            var desc = Window(new SqlOverClauseExpression().AddOrderBy(col, SortDirection.Descending));
            var partition = Window(new SqlOverClauseExpression().AddPartitionBy(col));

            Assert.AreNotEqual(SqlExpressionHashGenerator.GenerateHash(asc), SqlExpressionHashGenerator.GenerateHash(desc));
            Assert.AreNotEqual(SqlExpressionHashGenerator.GenerateHash(asc), SqlExpressionHashGenerator.GenerateHash(partition));
        }

        #endregion

        #region template parsing

        [TestMethod]
        public void Template_splits_text_arguments_and_literals()
        {
            var t = SqlFunctionTemplate.Parse("dateadd([0], {1}, {2})", 3, "DateAdd");
            var kinds = t.Parts.Select(p => p.Kind).ToArray();
            CollectionAssert.AreEqual(new[]
            {
                SqlFunctionTemplatePartKind.Text, SqlFunctionTemplatePartKind.Literal,
                SqlFunctionTemplatePartKind.Text, SqlFunctionTemplatePartKind.Argument,
                SqlFunctionTemplatePartKind.Text, SqlFunctionTemplatePartKind.Argument,
                SqlFunctionTemplatePartKind.Text,
            }, kinds);
            Assert.AreEqual(0, t.Parts[1].ArgumentIndex);
            Assert.AreEqual(2, t.Parts[5].ArgumentIndex);
        }

        [TestMethod]
        public void Template_keeps_brackets_that_are_not_placeholders()
        {
            var t = SqlFunctionTemplate.Parse("[dbo].f({0})", 1, "F");
            Assert.AreEqual("[dbo].f(", t.Parts[0].Text);
        }

        [TestMethod]
        public void Template_refuses_argument_number_the_method_does_not_have()
        {
            var ex = Assert.ThrowsException<InvalidOperationException>(() => SqlFunctionTemplate.Parse("f({2})", 2, "F"));
            StringAssert.Contains(ex.Message, "argument 2");
        }

        #endregion

        #region template functions

        [TestMethod]
        public void Plain_function_is_written_with_the_method_name_and_translated_arguments()
        {
            var students = new Queryable<Student>(queryProvider);
            var q = students.Select(x => new { C1 = SqlServerFunctions.Ascii(x.Name), C2 = SqlServerFunctions.CharIndex("a", x.Name) });
            var sql = TranslateForSqlServer(q.Expression);
            StringAssert.Contains(sql, "Ascii(t1.Name)");
            StringAssert.Contains(sql, "CharIndex(@p");
        }

        [TestMethod]
        public void Function_that_overlaps_a_built_in_still_translates()
        {
            var students = new Queryable<Student>(queryProvider);
            var q = students.Select(x => new { C1 = SqlServerFunctions.Len(x.Name) });
            var sql = TranslateForSqlServer(q.Expression);
            StringAssert.Contains(sql, "Len(t1.Name)");
        }

        [TestMethod]
        public void Bracket_argument_is_written_into_the_sql_text()
        {
            var students = new Queryable<Student>(queryProvider);
            var q = students.Select(x => new { C1 = SqlServerFunctions.DateAdd(DatePart.day, 5, x.AdmissionDate) });
            var sql = TranslateForSqlServer(q.Expression);
            StringAssert.Contains(sql, "dateadd(day, @p");
            StringAssert.Contains(sql, ", t1.AdmissionDate)");
        }

        [TestMethod]
        public void DatePart_uses_datepart_not_datename()
        {
            var students = new Queryable<Student>(queryProvider);
            var q = students.Select(x => new { C1 = SqlServerFunctions.DatePart(DatePart.month, x.AdmissionDate), C2 = SqlServerFunctions.DateName(DatePart.month, x.AdmissionDate) });
            var sql = TranslateForSqlServer(q.Expression);
            StringAssert.Contains(sql, "datepart(month, t1.AdmissionDate)");
            StringAssert.Contains(sql, "datename(month, t1.AdmissionDate)");
        }

        [TestMethod]
        public void Whole_number_constant_is_written_into_the_sql_text()
        {
            var students = new Queryable<Student>(queryProvider);
            var q = students.Select(x => new { C1 = SqlServerFunctions.CastToVarchar(x.Age, 20) });
            var sql = TranslateForSqlServer(q.Expression);
            StringAssert.Contains(sql, "cast(t1.Age as varchar(20))");
        }

        [TestMethod]
        public void Bracket_argument_that_is_a_variable_is_refused()
        {
            var students = new Queryable<Student>(queryProvider);
            var length = 20;
            var q = students.Select(x => new { C1 = SqlServerFunctions.CastToVarchar(x.Age, length) });
            var ex = Assert.ThrowsException<InvalidOperationException>(() => TranslateForSqlServer(q.Expression));
            StringAssert.Contains(ex.Message, "written into the SQL text");
        }

        [TestMethod]
        public void Convert_with_style_parameter_and_constant_length()
        {
            var students = new Queryable<Student>(queryProvider);
            var q = students.Select(x => new { C1 = SqlServerFunctions.ConvertToVarchar(x.Age, 1, 10) });
            StringAssert.Contains(TranslateForSqlServer(q.Expression), "convert(varchar(10), t1.Age, @p");
        }

        [TestMethod]
        public void Captured_variable_in_a_brace_argument_becomes_a_parameter()
        {
            var students = new Queryable<Student>(queryProvider);
            var days = 7;
            var q = students.Select(x => new { C1 = SqlServerFunctions.DateAdd(DatePart.day, days, x.AdmissionDate) });
            var sql = TranslateForSqlServer(q.Expression);
            StringAssert.Contains(sql, "dateadd(day, @p");
        }

        #endregion

        #region window functions

        [TestMethod]
        public void Unit_test_translator_writes_the_same_shape()
        {
            var employees = new Queryable<Employee>(queryProvider);
            var q = employees.Select(x => new { Rn = Sql.RowNumber(Sql.Over().PartitionBy(x.Department).OrderByDescending(x.Name)), L = SqlServerFunctions.Len(x.Name) });
            Test("window and template through the test translator", q.Expression,
                @"select ROW_NUMBER() OVER (PARTITION BY a_1.Department ORDER BY a_1.Name DESC) as Rn, Len(a_1.Name) as L from Employee as a_1");
        }

        [TestMethod]
        public void RowNumber_with_partition_and_order()
        {
            var employees = new Queryable<Employee>(queryProvider);
            var q = employees.Select(x => new
            {
                x.EmployeeId,
                Rn = Sql.RowNumber(Sql.Over().PartitionBy(x.Department).OrderBy(x.Name).OrderByDescending(x.EmployeeId)),
            });
            var sql = TranslateForSqlServer(q.Expression);
            StringAssert.Contains(sql, "ROW_NUMBER() OVER (PARTITION BY t1.Department ORDER BY t1.Name ASC, t1.EmployeeId DESC)");
        }

        [TestMethod]
        public void Over_with_nothing_in_it()
        {
            var employees = new Queryable<Employee>(queryProvider);
            var q = employees.Select(x => new { Rn = Sql.RowNumber(Sql.Over().OrderBy(x.Name)), R = Sql.Rank(Sql.Over().OrderBy(x.Name)) });
            var sql = TranslateForSqlServer(q.Expression);
            StringAssert.Contains(sql, "ROW_NUMBER() OVER (ORDER BY t1.Name ASC)");
            StringAssert.Contains(sql, "RANK() OVER (ORDER BY t1.Name ASC)");
        }

        [TestMethod]
        public void Ranking_family_names()
        {
            var employees = new Queryable<Employee>(queryProvider);
            var q = employees.Select(x => new
            {
                A = Sql.DenseRank(Sql.Over().OrderBy(x.Name)),
                B = Sql.Ntile(4, Sql.Over().OrderBy(x.Name)),
                C = Sql.PercentRank(Sql.Over().OrderBy(x.Name)),
                D = Sql.CumeDist(Sql.Over().OrderBy(x.Name)),
            });
            var sql = TranslateForSqlServer(q.Expression);
            StringAssert.Contains(sql, "DENSE_RANK() OVER");
            StringAssert.Contains(sql, "NTILE(@p");
            StringAssert.Contains(sql, "PERCENT_RANK() OVER");
            StringAssert.Contains(sql, "CUME_DIST() OVER");
        }

        [TestMethod]
        public void Lag_lead_first_and_last_value()
        {
            var employees = new Queryable<Employee>(queryProvider);
            var q = employees.Select(x => new
            {
                A = Sql.Lag(x.Name, Sql.Over().PartitionBy(x.Department).OrderBy(x.EmployeeId)),
                B = Sql.Lag(x.Name, 2, Sql.Over().OrderBy(x.EmployeeId)),
                C = Sql.Lead(x.Name, 1, "none", Sql.Over().OrderBy(x.EmployeeId)),
                D = Sql.FirstValue(x.Name, Sql.Over().OrderBy(x.EmployeeId)),
                E = Sql.LastValue(x.Name, Sql.Over().OrderBy(x.EmployeeId)),
            });
            var sql = TranslateForSqlServer(q.Expression);
            StringAssert.Contains(sql, "LAG(t1.Name) OVER (PARTITION BY t1.Department ORDER BY t1.EmployeeId ASC)");
            StringAssert.Contains(sql, "LAG(t1.Name, @p");
            StringAssert.Contains(sql, "LEAD(t1.Name, @p");
            StringAssert.Contains(sql, "FIRST_VALUE(t1.Name) OVER");
            StringAssert.Contains(sql, "LAST_VALUE(t1.Name) OVER");
        }

        [TestMethod]
        public void Window_function_result_can_be_filtered_in_a_following_where()
        {
            var employees = new Queryable<Employee>(queryProvider);
            var q = employees
                .Select(x => new { x.EmployeeId, Rn = Sql.RowNumber(Sql.Over().PartitionBy(x.Department).OrderBy(x.Name)) })
                .Where(x => x.Rn == 1);
            var sql = TranslateForSqlServer(q.Expression);
            StringAssert.Contains(sql, "ROW_NUMBER() OVER (PARTITION BY t2.Department ORDER BY t2.Name ASC)");
            StringAssert.Contains(sql, "WHERE (t1.Rn = @p");
        }

        [TestMethod]
        public void Marker_methods_cannot_be_called_directly()
        {
            Assert.ThrowsException<Atis.SqlExpressionEngine.Exceptions.DirectCallNotSupportedException>(() => Sql.Over());
            Assert.ThrowsException<Atis.SqlExpressionEngine.Exceptions.DirectCallNotSupportedException>(() => SqlServerFunctions.Len("a"));
        }

        #endregion
    }
}
