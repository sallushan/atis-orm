using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     Joins, applies and left joins that arrive after the query needs a wrap (after a Select or a Take).
    ///     The join converter calls <c>WrapIfRequired</c> early, before it maps lambda parameters; these tests
    ///     cover the apply and the data-source-selection branches, which no other test reaches after a wrap.
    /// </summary>
    [TestClass]
    public class JoinAfterWrapTests : TestBase
    {
        [TestMethod]
        public void CrossApply_after_Select_wraps_before_applying()
        {
            Expression<Func<object>> q = () =>
                queryProvider.DataSet<Equipment>()
                .Select(e => new { e.EquipId, e.ItemId })
                .CrossApply(x => queryProvider.DataSet<ItemBase>().Where(y => y.ItemId == x.ItemId).Take(1), (e, i) => new { e, i })
                .Select(x => new { x.e.EquipId, x.i.ItemDescription });
            const string expected = @"
select a_2.EquipId as EquipId, a_3.ItemDescription as ItemDescription
from (
		select a_1.EquipId as EquipId, a_1.ItemId as ItemId
		from Equipment as a_1
	) as a_2
		cross apply (
			select top (1) a_4.ItemId as ItemId, a_4.ItemDescription as ItemDescription
			from ItemBase as a_4
			where (a_4.ItemId = a_2.ItemId)
		) as a_3
";
            Test("cross apply after select", q.Body, expected);
        }

        [TestMethod]
        public void OuterApply_after_Take_wraps_before_applying()
        {
            Expression<Func<object>> q = () =>
                queryProvider.DataSet<Equipment>()
                .Take(5)
                .OuterApply(x => queryProvider.DataSet<ItemBase>().Where(y => y.ItemId == x.ItemId).Take(1), (e, i) => new { e, i })
                .Select(x => new { x.e.EquipId, x.i.ItemDescription });
            const string expected = @"
select a_2.EquipId as EquipId, a_3.ItemDescription as ItemDescription
from (
		select top (5) a_1.EquipId as EquipId, a_1.Model as Model, a_1.ItemId as ItemId
		from Equipment as a_1
	) as a_2
		outer apply (
			select top (1) a_4.ItemId as ItemId, a_4.ItemDescription as ItemDescription
			from ItemBase as a_4
			where (a_4.ItemId = a_2.ItemId)
		) as a_3
";
            Test("outer apply after take", q.Body, expected);
        }

        [TestMethod]
        public void LeftJoin_with_data_source_selection_after_Take_is_refused_with_a_clear_message()
        {
            // The early WrapIfRequired(Join) is what makes this refuse. Without it the join is applied inside the
            // TOP (5) and the SQL is silently different (the Take would run after the left join).
            var q =
                queryProvider.From(() => new
                {
                    s = QueryExtensions.Table<Student>(),
                    sg = QueryExtensions.Table<StudentGrade>(),
                })
                .Take(5)
                .LeftJoin(x => x.sg, j => j.s.StudentId == j.sg.StudentId);
            var ex = Assert.ThrowsException<InvalidCastException>(() => Test("left join after take", q.Expression, ""));
            StringAssert.Contains(ex.Message, "data source selection in the join method must be a data source");
        }
    }
}
