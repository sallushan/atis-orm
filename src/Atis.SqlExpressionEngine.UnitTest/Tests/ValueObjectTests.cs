using Atis.Orm.Metadata;
using Atis.Orm.Services;
using Atis.SqlExpressionEngine.SqlExpressions;
using System.Collections;
using System.Data;
using System.Linq;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         Covers a value-object-typed property (<see cref="ValueObjectAnnotatedEntity.ValObjOutTime"/>)
    ///         mapped onto several columns of its own entity's table via
    ///         <see cref="Atis.Orm.Annotations.ValueObjectAttribute"/>, and the fluent counterpart
    ///         (<see cref="EntityBuilder{T}.ValueObject{TVo}(System.Linq.Expressions.Expression{System.Func{T, TVo}})"/>).
    ///     </para>
    ///     <para>
    ///         The feature needs no preprocessor or new <c>SqlExpression</c> node: a value-object member
    ///         becomes a dotted-path <c>TableColumn</c> (<c>"ValObjOutTime.LocalDateTime"</c>), and
    ///         <c>SqlTableExpression.CreateQueryShape</c> groups those into a nested
    ///         <c>SqlMemberInitExpression</c> binding, so <c>x.ValObjOutTime.LocalDateTime</c> resolves
    ///         through two ordinary <c>MemberExpressionConverter</c> lookups — the same mechanism any
    ///         other nested projection already uses. These tests exercise that path end to end.
    ///     </para>
    /// </summary>
    [TestClass]
    public class ValueObjectTests : TestBase
    {
        private string Render(System.Linq.Expressions.Expression queryExpression)
        {
            var sqlExpression = this.ConvertExpressionToSqlExpression(queryExpression, out _);
            var translator = new SqlExpressionTranslator { IsRowNumberSupported = false };
            return translator.Translate(sqlExpression);
        }

        private static string Squash(string sql) => sql.Replace(" ", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");

        /// <summary>A value-object member in a WHERE predicate resolves to its own mapped column.</summary>
        [TestMethod]
        public void Where_on_a_value_object_member_resolves_to_its_mapped_column()
        {
            var cutoff = new DateTime(2026, 1, 1);
            var q = new Queryable<ValueObjectAnnotatedEntity>(this.queryProvider)
                        .Where(x => x.ValObjOutTime.LocalDateTime > cutoff);

            var rendered = Squash(this.Render(q.Expression));

            StringAssert.Contains(rendered, "a_1.OUT_DT_TM>",
                "value-object member access in WHERE must resolve to its own mapped column (OUT_DT_TM), not ValObjOutTime.LocalDateTime.");
        }

        /// <summary>Ordering by a value-object member resolves to its own mapped column.</summary>
        [TestMethod]
        public void OrderBy_on_a_value_object_member_resolves_to_its_mapped_column()
        {
            var q = new Queryable<ValueObjectAnnotatedEntity>(this.queryProvider)
                        .OrderBy(x => x.ValObjOutTime.ZoneCode);

            var rendered = Squash(this.Render(q.Expression));

            StringAssert.Contains(rendered, Squash("order by a_1.OUT_DT_TM_FROM"),
                "ORDER BY on a value-object member must resolve to its own mapped column (OUT_DT_TM_FROM).");
        }

        /// <summary>
        ///     A projection that selects a value-object member directly aliases to the mapped column —
        ///     proving the two-level member chain resolves through the ordinary projection converter too,
        ///     not only inside WHERE/ORDER BY.
        /// </summary>
        [TestMethod]
        public void Select_of_a_value_object_member_resolves_to_its_mapped_column()
        {
            var q = new Queryable<ValueObjectAnnotatedEntity>(this.queryProvider)
                        .Select(x => x.ValObjOutTime.LocalDateTime);

            var rendered = Squash(this.Render(q.Expression));

            StringAssert.Contains(rendered, Squash("select a_1.OUT_DT_TM"));
        }

        /// <summary>
        ///     <see cref="ValueObjectAnnotatedEntity"/> reuses the same value-object CLR type
        ///     (<see cref="ZonedDateTimeValueObject"/>) on two properties with different column mappings —
        ///     proving the two do not collide (their leaf member names, e.g. <c>LocalDateTime</c>, are
        ///     identical; only the dotted path distinguishes them).
        /// </summary>
        [TestMethod]
        public void Two_properties_of_the_same_value_object_type_resolve_to_distinct_columns()
        {
            var outQ = new Queryable<ValueObjectAnnotatedEntity>(this.queryProvider)
                        .Select(x => x.ValObjOutTime.LocalDateTime);
            var inQ = new Queryable<ValueObjectAnnotatedEntity>(this.queryProvider)
                        .Select(x => x.ValObjInTime.LocalDateTime);

            var outRendered = Squash(this.Render(outQ.Expression));
            var inRendered = Squash(this.Render(inQ.Expression));

            StringAssert.Contains(outRendered, "a_1.OUT_DT_TM");
            StringAssert.Contains(inRendered, "a_1.IN_DT_TM");
        }

        /// <summary>
        ///     <para>
        ///         A full-entity fetch (no explicit projection) auto-projects the whole shape, which
        ///         includes the value-object property as a <em>nested</em> binding
        ///         (<c>SqlTableExpression.CreateQueryShape</c> groups its dotted columns), rather than a
        ///         flat one. All six mapped columns across both value-object properties must appear in
        ///         the select list, proving the nested shape survives auto-projection — the same shape
        ///         <c>ElementFactoryBuilder</c> recurses into when materializing the entity.
        ///     </para>
        /// </summary>
        [TestMethod]
        public void Full_entity_fetch_includes_all_value_object_columns()
        {
            var q = new Queryable<ValueObjectAnnotatedEntity>(this.queryProvider)
                        .Where(x => x.Id == 1);

            var rendered = Squash(this.Render(q.Expression));

            StringAssert.Contains(rendered, "a_1.OUT_DT_TM_FROMasZoneCode");
            StringAssert.Contains(rendered, "a_1.OUT_DT_TMasLocalDateTime");
            StringAssert.Contains(rendered, "a_1.OUT_DT_TM_ZULUasZuluDateTime");
            StringAssert.Contains(rendered, "a_1.IN_DT_TM_TOasZoneCode");
            StringAssert.Contains(rendered, "a_1.IN_DT_TMasLocalDateTime");
            StringAssert.Contains(rendered, "a_1.IN_DT_TM_ZULUasZuluDateTime");
        }

        /// <summary>
        ///     <para>
        ///         Executes the element factory <see cref="ElementFactoryBuilder"/> compiles for a
        ///         full-entity fetch against a fake row, and asserts that both nested value-object
        ///         properties come back populated with the correct per-column values — not just that the
        ///         SQL text looks right. Positions in <see cref="FakeDataReader"/>'s row match the
        ///         rendered select list asserted by <see cref="Full_entity_fetch_includes_all_value_object_columns"/>:
        ///         Id, then ValObjOutTime's three columns, then ValObjInTime's three columns.
        ///     </para>
        /// </summary>
        [TestMethod]
        public void Materializing_a_row_populates_both_nested_value_object_properties()
        {
            var q = new Queryable<ValueObjectAnnotatedEntity>(this.queryProvider)
                        .Where(x => x.Id == 1);

            var sqlExpression = this.ConvertExpressionToSqlExpression(q.Expression, out var updatedExpression);
            var derivedTable = sqlExpression as SqlDerivedTableExpression
                                ?? throw new InvalidOperationException($"Expected {nameof(SqlDerivedTableExpression)}, got {sqlExpression?.GetType().Name}.");

            var elementFactory = new ElementFactoryBuilder().CreateElementFactory(updatedExpression, derivedTable);

            var outLocal = new DateTime(2026, 3, 1, 10, 0, 0);
            var outZulu = new DateTime(2026, 3, 1, 15, 0, 0);
            var inLocal = new DateTime(2026, 3, 2, 8, 0, 0);
            var inZulu = new DateTime(2026, 3, 2, 16, 0, 0);
            var reader = new FakeDataReader(new object[]
            {
                1, "UTC", outLocal, outZulu, "PST", inLocal, inZulu,
            });

            var element = (ValueObjectAnnotatedEntity)elementFactory(reader);

            Assert.IsNotNull(element.ValObjOutTime, "ValObjOutTime must be constructed, not left null.");
            Assert.AreEqual("UTC", element.ValObjOutTime.ZoneCode);
            Assert.AreEqual(outLocal, element.ValObjOutTime.LocalDateTime);
            Assert.AreEqual(outZulu, element.ValObjOutTime.ZuluDateTime);

            Assert.IsNotNull(element.ValObjInTime, "ValObjInTime must be constructed, not left null.");
            Assert.AreEqual("PST", element.ValObjInTime.ZoneCode);
            Assert.AreEqual(inLocal, element.ValObjInTime.LocalDateTime);
            Assert.AreEqual(inZulu, element.ValObjInTime.ZuluDateTime);
        }

        /// <summary>
        ///     Minimal positional <see cref="IDataReader"/> double: values are read by ordinal only
        ///     (<see cref="ElementFactoryBuilder"/> never calls <c>GetOrdinal</c>), so this needs no
        ///     column-name lookup — just an <c>object[]</c> row and the handful of typed getters the
        ///     compiled element factory actually calls.
        /// </summary>
        private sealed class FakeDataReader : IDataReader
        {
            private readonly object[] row;
            public FakeDataReader(object[] row) => this.row = row;

            public object GetValue(int i) => this.row[i];
            public bool IsDBNull(int i) => this.row[i] is null || this.row[i] == DBNull.Value;
            public string GetString(int i) => (string)this.row[i];
            public DateTime GetDateTime(int i) => (DateTime)this.row[i];
            public int GetInt32(int i) => (int)this.row[i];
            public bool GetBoolean(int i) => (bool)this.row[i];
            public long GetInt64(int i) => (long)this.row[i];
            public decimal GetDecimal(int i) => (decimal)this.row[i];
            public double GetDouble(int i) => (double)this.row[i];
            public float GetFloat(int i) => (float)this.row[i];
            public Guid GetGuid(int i) => (Guid)this.row[i];
            public short GetInt16(int i) => (short)this.row[i];
            public byte GetByte(int i) => (byte)this.row[i];
            public char GetChar(int i) => (char)this.row[i];

            public int FieldCount => this.row.Length;
            public object this[int i] => this.row[i];
            public object this[string name] => throw new NotImplementedException();
            public int Depth => 0;
            public bool IsClosed => false;
            public int RecordsAffected => -1;

            public string GetName(int i) => throw new NotImplementedException();
            public int GetOrdinal(string name) => throw new NotImplementedException();
            public int GetValues(object[] values) => throw new NotImplementedException();
            public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length) => throw new NotImplementedException();
            public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length) => throw new NotImplementedException();
            public IDataReader GetData(int i) => throw new NotImplementedException();
            public string GetDataTypeName(int i) => throw new NotImplementedException();
            public Type GetFieldType(int i) => throw new NotImplementedException();
            public DataTable GetSchemaTable() => throw new NotImplementedException();
            public bool NextResult() => false;
            public bool Read() => throw new NotImplementedException();
            public void Close() { }
            public void Dispose() { }
            public IEnumerator GetEnumerator() => throw new NotImplementedException();
        }

        /// <summary>
        ///     <para>
        ///         <c>.Select(x =&gt; new { x.Id, VO = x.ValObjOutTime, Other = x.ValObjInTime.ZoneCode })</c>
        ///         — selecting the whole value-object property, not just one of its members.
        ///     </para>
        ///     <para>
        ///         The rendered select list flattens <c>VO</c>'s three columns to the top level under
        ///         their own leaf names (<c>ZoneCode</c>, <c>LocalDateTime</c>, <c>ZuluDateTime</c>) rather
        ///         than keeping a <c>VO_</c>-prefixed alias — this is the engine's ordinary, deliberate
        ///         behaviour for selecting a whole composite object, already covered by
        ///         <c>ComplexProjectionTests</c> (e.g.
        ///         <c>Multiple_data_sources_selected_in_1_property_and_then_that_1_property_selected_in_projection_in_anonymous_type_...</c>,
        ///         where <c>t = x.o</c> hoists every column of the joined shape <c>o</c> to the top level
        ///         with <c>_1</c>/<c>_2</c> suffixes on collision). A value object is, from the engine's
        ///         point of view, just another <c>SqlMemberInitExpression</c>-shaped member, so it goes
        ///         through the exact same flattening — nothing new needed here.
        ///     </para>
        ///     <para>
        ///         What this test actually checks is materialization: <c>ElementFactoryBuilder</c> maps
        ///         columns to the target type by the <em>ordinal position</em> of the
        ///         <c>SqlExpression</c> object it was built from (<c>ReferenceEqualityComparer</c> on
        ///         <c>SqlDataSourceColumnExpression</c> instances), not by rendered alias text, so the
        ///         anonymous type's <c>VO</c> member should still come back correctly constructed. This
        ///         proves it by executing the compiled factory, not by re-reasoning about it.
        ///     </para>
        /// </summary>
        [TestMethod]
        public void Selecting_the_whole_value_object_still_materializes_correctly()
        {
            var q = new Queryable<ValueObjectAnnotatedEntity>(this.queryProvider)
                        .Select(x => new { x.Id, VO = x.ValObjOutTime, Other = x.ValObjInTime.ZoneCode });

            var sqlExpression = this.ConvertExpressionToSqlExpression(q.Expression, out var updated);
            var translator = new SqlExpressionTranslator { IsRowNumberSupported = false };
            var rendered = Squash(translator.Translate(sqlExpression));

            // Documents the flattening described above: VO's alias is gone, its three columns sit at the
            // top level under their own leaf names.
            StringAssert.Contains(rendered, "a_1.IdasId,a_1.OUT_DT_TM_FROMasZoneCode,a_1.OUT_DT_TMasLocalDateTime,a_1.OUT_DT_TM_ZULUasZuluDateTime,a_1.IN_DT_TM_TOasOther");

            var derivedTable = sqlExpression as SqlDerivedTableExpression
                                ?? throw new InvalidOperationException($"Expected {nameof(SqlDerivedTableExpression)}, got {sqlExpression?.GetType().Name}.");
            var elementFactory = new ElementFactoryBuilder().CreateElementFactory(updated, derivedTable);

            var local = new DateTime(2026, 3, 1, 10, 0, 0);
            var zulu = new DateTime(2026, 3, 1, 15, 0, 0);
            var reader = new FakeDataReader(new object[] { 1, "UTC", local, zulu, "PST" });

            dynamic element = elementFactory(reader);

            Assert.AreEqual(1, (int)element.Id);
            Assert.IsNotNull(element.VO, "the whole value-object member must still materialize as a real instance.");
            Assert.AreEqual("UTC", (string)element.VO.ZoneCode);
            Assert.AreEqual(local, (DateTime)element.VO.LocalDateTime);
            Assert.AreEqual(zulu, (DateTime)element.VO.ZuluDateTime);
            Assert.AreEqual("PST", (string)element.Other);
        }

        /// <summary>The fluent path (ModelBuilder / EntityBuilder&lt;T&gt;.ValueObject) seeds the same dotted TableColumns as the attribute path.</summary>
        [TestMethod]
        public void Fluent_configuration_produces_the_same_dotted_columns_as_the_attribute()
        {
            using var context = new OrmDbContext();

            var mapping = context.GetEntityMetadata<ValueObjectFluentEntity>();

            Assert.IsNotNull(mapping);
            Assert.IsTrue(mapping.SqlColumns.Any(x => x.ModelPropertyName == "ValObjOutTime.LocalDateTime" && x.DatabaseColumnName == "OUT_DT_TM"));
            Assert.IsTrue(mapping.SqlColumns.Any(x => x.ModelPropertyName == "ValObjOutTime.ZoneCode" && x.DatabaseColumnName == "OUT_DT_TM_FROM"));
            Assert.IsTrue(mapping.SqlColumns.Any(x => x.ModelPropertyName == "ValObjInTime.LocalDateTime" && x.DatabaseColumnName == "IN_DT_TM"));
            Assert.IsFalse(mapping.SqlColumns.Any(x => x.ModelPropertyName == "ValObjOutTime"),
                "the value-object property itself must not also appear as a flat column.");
        }
    }
}
