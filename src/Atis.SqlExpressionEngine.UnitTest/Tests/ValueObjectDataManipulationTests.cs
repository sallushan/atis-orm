using System.Linq.Expressions;
using Atis.Orm.Services;
using Atis.SqlExpressionEngine.SqlExpressions;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     Insert and Update assignments through a value object: <c>Vo = new V { Leaf = value }</c> lands on the
    ///     value object's own mapped columns, and <c>Vo = null</c> writes NULL to every column it owns.
    /// </summary>
    [TestClass]
    public class ValueObjectDataManipulationTests : TestBase
    {
        private static readonly DateTime Moment = new DateTime(2026, 1, 2);

        [TestMethod]
        public void Insert_writes_a_nested_value_object_to_its_mapped_columns()
        {
            var entities = new Queryable<ValueObjectAnnotatedEntity>(this.queryProvider);
            Expression<Func<int>> expr = () => entities.Insert(() => new ValueObjectAnnotatedEntity
            {
                Id = 1,
                ValObjOutTime = new ZonedDateTimeValueObject { ZoneCode = "UTC", LocalDateTime = Moment, ZuluDateTime = Moment },
            });

            string expectedResult = @"
insert into VALUE_OBJECT_ANNOTATED_ENTITY (Id, OUT_DT_TM_FROM, OUT_DT_TM, OUT_DT_TM_ZULU)
values (1, 'UTC', '2026-01-02 00:00:00', '2026-01-02 00:00:00')
";
            Test("Value Object Insert Nested Test", expr.Body, expectedResult);
        }

        [TestMethod]
        public void Insert_of_a_null_value_object_writes_null_to_every_column_it_owns()
        {
            var entities = new Queryable<ValueObjectAnnotatedEntity>(this.queryProvider);
            Expression<Func<int>> expr = () => entities.Insert(() => new ValueObjectAnnotatedEntity
            {
                Id = 1,
                ValObjOutTime = null,
            });

            string expectedResult = @"
insert into VALUE_OBJECT_ANNOTATED_ENTITY (Id, OUT_DT_TM_FROM, OUT_DT_TM, OUT_DT_TM_ZULU)
values (1, null, null, null)
";
            Test("Value Object Insert Null Test", expr.Body, expectedResult);
        }

        [TestMethod]
        public void Update_writes_a_nested_value_object_and_outputs_it_under_its_whole_path()
        {
            var entities = new Queryable<ValueObjectAnnotatedEntity>(this.queryProvider);
            Expression<Func<IReadOnlyList<IReadOnlyDictionary<string, object>>>> expr = () => entities.Update(
                x => new ValueObjectAnnotatedEntity
                {
                    ValObjInTime = new ZonedDateTimeValueObject { ZoneCode = "PST", LocalDateTime = Moment, ZuluDateTime = Moment },
                    ValObjOutTime = null,
                },
                x => x.Id == 1,
                x => new object[] { x.ValObjOutTime.ZoneCode, x.ValObjInTime.ZoneCode });

            string expectedResult = @"
update a_1
	set IN_DT_TM_TO = 'PST',
		IN_DT_TM = '2026-01-02 00:00:00',
		IN_DT_TM_ZULU = '2026-01-02 00:00:00',
		OUT_DT_TM_FROM = null,
		OUT_DT_TM = null,
		OUT_DT_TM_ZULU = null
output inserted.OUT_DT_TM_FROM as ValObjOutTime_ZoneCode, inserted.IN_DT_TM_TO as ValObjInTime_ZoneCode
from VALUE_OBJECT_ANNOTATED_ENTITY as a_1
	where (a_1.Id = 1)
";
            Test("Value Object Update Nested And Output Test", expr.Body, expectedResult);
        }
    }
}
