﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Atis.LinqToSql.UnitTest
{
    [TestClass]
    public class JoinExtensionMethodTests : TestBase
    {

        [TestMethod]
        public void Join_added_later_in_query_with_new_anonymous_type_created()
        {
            Expression<Func<object>> queryExpression = () =>
            dbc.DataSet<Student>()
            .LeftJoin(dbc.DataSet<StudentGrade>(), (s, sg) => new { s, sg }, j => j.s.StudentId == j.sg.StudentId)
            .LeftJoin(dbc.DataSet<StudentGradeDetail>(), (ps, gd) => new { s1 = ps.s, sg1 = ps.sg, gd }, j2 => j2.sg1.RowId == j2.gd.StudentGradeRowId)
            ;
            var expectedResult = @"
select	a_1.StudentId as StudentId, a_1.Name as Name, a_1.Address as Address, a_1.Age as Age, a_1.AdmissionDate as AdmissionDate, a_1.RecordCreateDate as RecordCreateDate, a_1.RecordUpdateDate as RecordUpdateDate, a_1.StudentType as StudentType, a_1.CountryID as CountryID, a_1.HasScholarship as HasScholarship, a_2.RowId as RowId, a_2.StudentId as StudentId_1, a_2.Grade as Grade, a_3.RowId as RowId_1, a_3.StudentGradeRowId as StudentGradeRowId, a_3.SubjectId as SubjectId, a_3.MarksGained as MarksGained, a_3.TotalMarks as TotalMarks
	from	Student as a_1
		left join StudentGrade as a_2 on (a_1.StudentId = a_2.StudentId)
		left join StudentGradeDetail as a_3 on (a_2.RowId = a_3.StudentGradeRowId)
";
            Test("Simple Join Test", queryExpression.Body, expectedResult);
        }

        [TestMethod]
        public void Join_added_later_in_query_with_anonymous_type_created_using_LambdaParameter()
        {
            Expression<Func<object>> queryExpression = () =>
            dbc.DataSet<Student>()
            .LeftJoin(dbc.DataSet<StudentGrade>(), (s, sg) => new { s, sg }, j => j.s.StudentId == j.sg.StudentId)
            .LeftJoin(dbc.DataSet<StudentGradeDetail>(), (ps, gd) => new { ps, gd }, j2 => j2.ps.sg.RowId == j2.gd.StudentGradeRowId)
            ;
            var expectedResult = @"
select	a_1.StudentId as StudentId, a_1.Name as Name, a_1.Address as Address, a_1.Age as Age, a_1.AdmissionDate as AdmissionDate, a_1.RecordCreateDate as RecordCreateDate, a_1.RecordUpdateDate as RecordUpdateDate, a_1.StudentType as StudentType, a_1.CountryID as CountryID, a_1.HasScholarship as HasScholarship, a_2.RowId as RowId, a_2.StudentId as StudentId_1, a_2.Grade as Grade, a_3.RowId as RowId_1, a_3.StudentGradeRowId as StudentGradeRowId, a_3.SubjectId as SubjectId, a_3.MarksGained as MarksGained, a_3.TotalMarks as TotalMarks
	from	Student as a_1
		left join StudentGrade as a_2 on (a_1.StudentId = a_2.StudentId)
		left join StudentGradeDetail as a_3 on (a_2.RowId = a_3.StudentGradeRowId)
";
            Test("Join Nested Data Sources Test", queryExpression.Body, expectedResult);
        }

        [TestMethod]
        public void Join_added_in_query_later_with_sub_query_as_joined_source()
        {
            Expression<Func<object>> queryExpression = () =>
            dbc.DataSet<Student>()
            .LeftJoin(dbc.DataSet<StudentGrade>(), (s, sg) => new { s, sg }, j => j.s.StudentId == j.sg.StudentId)
            .LeftJoin(dbc.DataSet<StudentGradeDetail>().Where(x => x.TotalMarks > 50).Select(x => new { SGRID = x.StudentGradeRowId, TM = x.TotalMarks }), (ps, gd) => new { ps, gd }, j2 => j2.ps.sg.RowId == j2.gd.SGRID)
            ;
            var expectedResult = @"
select	a_1.StudentId as StudentId, a_1.Name as Name, a_1.Address as Address, a_1.Age as Age, a_1.AdmissionDate as AdmissionDate, a_1.RecordCreateDate as RecordCreateDate, a_1.RecordUpdateDate as RecordUpdateDate, a_1.StudentType as StudentType, a_1.CountryID as CountryID, a_1.HasScholarship as HasScholarship, a_2.RowId as RowId, a_2.StudentId as StudentId_1, a_2.Grade as Grade, a_4.SGRID as SGRID, a_4.TM as TM
	from	Student as a_1
		left join StudentGrade as a_2 on (a_1.StudentId = a_2.StudentId)
		left join (
			select	a_3.StudentGradeRowId as SGRID, a_3.TotalMarks as TM
			from	StudentGradeDetail as a_3
			where	(a_3.TotalMarks > 50)
		) as a_4 on (a_2.RowId = a_4.SGRID)
";
            Test("Join With Sub Query Test", queryExpression.Body, expectedResult);
        }

        [TestMethod]
        public void Join_after_select_should_wrap_the_query_before_joining()
        {
            Expression<Func<object>> queryExpression = () =>
            dbc.DataSet<Student>()
            .LeftJoin(dbc.DataSet<StudentGrade>(), (s, sg) => new { s, sg }, j => j.s.StudentId == j.sg.StudentId)
            .Select(x => new { x.s.StudentId, x.s.Name, StudentGradeRowId = x.sg.RowId, x.sg.Grade })
            .LeftJoin(dbc.DataSet<StudentGradeDetail>().Where(x => x.TotalMarks > 50).Select(x => new { SGRID = x.StudentGradeRowId, TM = x.TotalMarks }), (ps, gd) => new { ps, gd }, j2 => j2.ps.StudentGradeRowId == j2.gd.SGRID)
            ;
            var expectedResult = @"
select	a_3.StudentId as StudentId, a_3.Name as Name, a_3.StudentGradeRowId as StudentGradeRowId, a_3.Grade as Grade, a_5.SGRID as SGRID, a_5.TM as TM
	from	(
		select	a_1.StudentId as StudentId, a_1.Name as Name, a_2.RowId as StudentGradeRowId, a_2.Grade as Grade
		from	Student as a_1
			left join StudentGrade as a_2 on (a_1.StudentId = a_2.StudentId)
	) as a_3
		left join (
			select	a_4.StudentGradeRowId as SGRID, a_4.TotalMarks as TM
			from	StudentGradeDetail as a_4
			where	(a_4.TotalMarks > 50)
		) as a_5 on (a_3.StudentGradeRowId = a_5.SGRID)
";
            Test("Wrap on Join Test", queryExpression.Body, expectedResult);
        }

        [TestMethod]
        public void Join_multiple_data_sources_added_through_From_query_method()
        {
            var q =
            dbc.From(() => new
            {
                s = QueryExtensions.Table<Student>(),
                sg = QueryExtensions.Table<StudentGrade>(),
            })
            .LeftJoin(x => x.sg, j => j.s.StudentId == j.sg.StudentId)
            .LeftJoin(dbc.DataSet<StudentGradeDetail>(), (os, gd) => new { os, gd }, j2 => j2.os.sg.RowId == j2.gd.StudentGradeRowId)
            ;
            var expectedResult = @"
select	a_1.StudentId as StudentId, a_1.Name as Name, a_1.Address as Address, a_1.Age as Age, a_1.AdmissionDate as AdmissionDate, a_1.RecordCreateDate as RecordCreateDate, a_1.RecordUpdateDate as RecordUpdateDate, a_1.StudentType as StudentType, a_1.CountryID as CountryID, a_1.HasScholarship as HasScholarship, a_2.RowId as RowId, a_2.StudentId as StudentId_1, a_2.Grade as Grade, a_3.RowId as RowId_1, a_3.StudentGradeRowId as StudentGradeRowId, a_3.SubjectId as SubjectId, a_3.MarksGained as MarksGained, a_3.TotalMarks as TotalMarks
	from	Student as a_1
		left join StudentGrade as a_2 on (a_1.StudentId = a_2.StudentId)
		left join StudentGradeDetail as a_3 on (a_2.RowId = a_3.StudentGradeRowId)
";
            Test("Multi Data Source Join Test", q.Expression, expectedResult);
        }


        [TestMethod]
        public void CrossApply_query_extension_method_test()
        {
            Expression<Func<object>> temp = () =>
            dbc.DataSet<Equipment>()
            .CrossApply(x1 => dbc.DataSet<ItemBase>().Where(y => y.ItemId == x1.ItemId).Take(1), (e, i) => new { e, i })
            .Select(x2 => new { x2.e.EquipId, x2.i.ItemDescription })
            ;

            string? expectedResult = @"
select  a_1.EquipId as EquipId, a_3.ItemDescription as ItemDescription
from    Equipment as a_1
        cross apply (
                select  top (1) a_2.ItemId as ItemId, a_2.ItemDescription as ItemDescription
                from    ItemBase as a_2
                where   (a_2.ItemId = a_1.ItemId)
        ) as a_3
";
            Test("Cross Apply Test", temp.Body, expectedResult);
        }

        [TestMethod]
        public void OuterApply_query_extension_method_test()
        {
            Expression<Func<object>> temp = () =>
            dbc.DataSet<Equipment>()
            .OuterApply(x => dbc.DataSet<ItemBase>().Where(y => y.ItemId == x.ItemId).Take(1), (e, i) => new { e, i })
            .Select(x => new { x.e.EquipId, x.i.ItemDescription })
            ;

            string? expectedResult = @"
select  a_1.EquipId as EquipId, a_3.ItemDescription as ItemDescription
from    Equipment as a_1
        outer apply (
                select  top (1) a_2.ItemId as ItemId, a_2.ItemDescription as ItemDescription
                from    ItemBase as a_2
                where   (a_2.ItemId = a_1.ItemId)
        ) as a_3
";
            Test("Outer Apply Test", temp.Body, expectedResult);
        }


        [TestMethod]
        public void FullOuterJoin_query_extension_method_test()
        {
            var students = new Queryable<Student>(new QueryProvider());
            var studentGrades = new Queryable<StudentGrade>(new QueryProvider());
            var q = students.FullOuterJoin(studentGrades, (x, y) => new { x, y }, (r) => r.x.StudentId == r.y.StudentId)
                            .Select(f => new { StudentTableStudentId = f.x.StudentId, StudentGradeTableStudentId = f.y.StudentId });
            string? expectedResult = @"select	a_1.StudentId as StudentTableStudentId, a_2.StudentId as StudentGradeTableStudentId
	from	Student as a_1
		full outer join StudentGrade as a_2 on (a_1.StudentId = a_2.StudentId)";
            Test("Full Outer Join Test", q.Expression, expectedResult);
        }

    }
}
