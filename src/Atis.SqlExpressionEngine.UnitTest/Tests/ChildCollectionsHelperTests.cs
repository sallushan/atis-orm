using Atis.Orm.Querying;
using System;
using System.Data;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     Plain tests of the small static helpers of <see cref="ChildCollections"/>. No database is needed.
    /// </summary>
    [TestClass]
    public class ChildCollectionsHelperTests
    {
        private static IDataReader ReaderOfOneRow(params object[] values)
        {
            var table = new DataTable();
            for (var i = 0; i < values.Length; i++)
                table.Columns.Add("C" + i, typeof(object));
            table.Rows.Add(values);
            var reader = table.CreateDataReader();
            Assert.IsTrue(reader.Read());
            return reader;
        }

        // ReadKeyValues -----------------------------------------------------------------------------------------------

        [TestMethod]
        public void ReadKeyValues_reads_the_asked_columns_in_the_order_given_and_turns_DBNull_into_null()
        {
            using (var reader = ReaderOfOneRow(1, "a", DBNull.Value, 4m))
            {
                var values = ChildCollections.ReadKeyValues(reader, new[] { 0, 2, 3 });
                Assert.AreEqual(3, values.Length);
                Assert.AreEqual(1, values[0]);
                Assert.IsNull(values[1]);
                Assert.AreEqual(4m, values[2]);
            }
        }

        [TestMethod]
        public void ReadKeyValues_with_no_ordinals_gives_an_empty_array()
        {
            using (var reader = ReaderOfOneRow(1))
                Assert.AreEqual(0, ChildCollections.ReadKeyValues(reader, new int[0]).Length);
        }

        [TestMethod]
        public void ReadKeyValues_rejects_null_arguments()
        {
            using (var reader = ReaderOfOneRow(1))
            {
                Assert.ThrowsException<ArgumentNullException>(() => ChildCollections.ReadKeyValues(null, new[] { 0 }));
                Assert.ThrowsException<ArgumentNullException>(() => ChildCollections.ReadKeyValues(reader, null));
            }
        }

        // MakeKey -----------------------------------------------------------------------------------------------------

        [TestMethod]
        public void MakeKey_with_one_position_returns_that_value_itself()
        {
            Assert.AreEqual(7, ChildCollections.MakeKey(new object[] { 1, 7 }, new[] { 1 }));
        }

        [TestMethod]
        public void MakeKey_with_one_position_and_a_null_value_returns_null()
        {
            Assert.IsNull(ChildCollections.MakeKey(new object[] { null }, new[] { 0 }));
        }

        [TestMethod]
        public void MakeKey_with_no_positions_returns_the_same_marker_every_time()
        {
            var first = ChildCollections.MakeKey(new object[] { 1 }, new int[0]);
            var second = ChildCollections.MakeKey(new object[] { 2 }, new int[0]);
            Assert.IsNotNull(first);
            Assert.AreSame(first, second);
        }

        [TestMethod]
        public void MakeKey_with_several_positions_gives_keys_that_are_equal_when_the_parts_are_equal()
        {
            var a = ChildCollections.MakeKey(new object[] { 1, "x", 99 }, new[] { 0, 1 });
            var b = ChildCollections.MakeKey(new object[] { 1, "x", 5 }, new[] { 0, 1 });
            var otherPart = ChildCollections.MakeKey(new object[] { 1, "y" }, new[] { 0, 1 });
            var otherOrder = ChildCollections.MakeKey(new object[] { 1, "x" }, new[] { 1, 0 });

            Assert.IsNotNull(a);
            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.AreNotEqual(a, otherPart);
            Assert.AreNotEqual(a, otherOrder, "The order of the parts matters.");
        }

        [TestMethod]
        public void MakeKey_with_several_positions_and_any_null_part_returns_null()
        {
            Assert.IsNull(ChildCollections.MakeKey(new object[] { 1, null }, new[] { 0, 1 }));
            Assert.IsNull(ChildCollections.MakeKey(new object[] { null, 1 }, new[] { 0, 1 }));
        }

        [TestMethod]
        public void MakeKey_ignores_values_at_positions_it_was_not_asked_for()
        {
            // the null at index 2 is not part of this key
            Assert.IsNotNull(ChildCollections.MakeKey(new object[] { 1, 2, null }, new[] { 0, 1 }));
        }

        [TestMethod]
        public void MakeKey_rejects_null_arguments()
        {
            Assert.ThrowsException<ArgumentNullException>(() => ChildCollections.MakeKey(null, new[] { 0 }));
            Assert.ThrowsException<ArgumentNullException>(() => ChildCollections.MakeKey(new object[0], null));
        }

        // Add / GetList / GetArray ------------------------------------------------------------------------------------

        [TestMethod]
        public void GetList_returns_the_items_added_under_the_key_in_the_order_they_were_added()
        {
            var collections = new ChildCollections(1);
            collections.Add(0, new ChildRow("a", 1));
            collections.Add(0, new ChildRow("b", 2));
            collections.Add(0, new ChildRow("c", 1));

            CollectionAssert.AreEqual(new[] { "a", "c" }, collections.GetList<string>(0, 1));
            CollectionAssert.AreEqual(new[] { "b" }, collections.GetList<string>(0, 2));
        }

        [TestMethod]
        public void GetList_returns_a_new_empty_list_for_an_unknown_key_and_for_a_null_key()
        {
            var collections = new ChildCollections(1);
            collections.Add(0, new ChildRow("a", 1));

            var unknown = collections.GetList<string>(0, 99);
            Assert.IsNotNull(unknown);
            Assert.AreEqual(0, unknown.Count);

            var nullKey = collections.GetList<string>(0, null);
            Assert.IsNotNull(nullKey);
            Assert.AreEqual(0, nullKey.Count);
            Assert.AreNotSame(unknown, nullKey);
        }

        [TestMethod]
        public void GetList_returns_a_new_list_each_time_so_changing_it_does_not_change_the_stored_rows()
        {
            var collections = new ChildCollections(1);
            collections.Add(0, new ChildRow("a", 1));

            var first = collections.GetList<string>(0, 1);
            first.Add("changed");
            first.Clear();

            CollectionAssert.AreEqual(new[] { "a" }, collections.GetList<string>(0, 1));
        }

        [TestMethod]
        public void GetArray_returns_the_items_added_under_the_key_and_a_new_empty_array_otherwise()
        {
            var collections = new ChildCollections(1);
            collections.Add(0, new ChildRow(10, "k"));
            collections.Add(0, new ChildRow(20, "k"));

            CollectionAssert.AreEqual(new[] { 10, 20 }, collections.GetArray<int>(0, "k"));
            Assert.AreEqual(0, collections.GetArray<int>(0, "other").Length);
            Assert.AreEqual(0, collections.GetArray<int>(0, null).Length);
        }

        [TestMethod]
        public void Rows_with_a_null_key_are_dropped_and_each_child_index_is_kept_apart()
        {
            var collections = new ChildCollections(2);
            collections.Add(0, new ChildRow("zero", 1));
            collections.Add(1, new ChildRow("one", 1));
            collections.Add(0, new ChildRow("nobody", null));

            CollectionAssert.AreEqual(new[] { "zero" }, collections.GetList<string>(0, 1));
            CollectionAssert.AreEqual(new[] { "one" }, collections.GetList<string>(1, 1));
        }

        [TestMethod]
        public void Null_items_are_kept()
        {
            var collections = new ChildCollections(1);
            collections.Add(0, new ChildRow(null, 1));
            collections.Add(0, new ChildRow("a", 1));

            CollectionAssert.AreEqual(new string[] { null, "a" }, collections.GetList<string>(0, 1));
        }

        [TestMethod]
        public void Rows_added_under_a_composite_key_are_found_by_an_equal_composite_key()
        {
            var collections = new ChildCollections(1);
            var values = new object[] { 1, "x" };
            collections.Add(0, new ChildRow("hit", ChildCollections.MakeKey(values, new[] { 0, 1 })));
            collections.Add(0, new ChildRow("miss", ChildCollections.MakeKey(new object[] { 1, "y" }, new[] { 0, 1 })));

            var lookup = ChildCollections.MakeKey(new object[] { 1, "x" }, new[] { 0, 1 });
            CollectionAssert.AreEqual(new[] { "hit" }, collections.GetList<string>(0, lookup));
        }

        [TestMethod]
        public void Add_rejects_a_null_row()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new ChildCollections(1).Add(0, null));
        }

        [TestMethod]
        public void The_no_key_marker_gives_every_caller_every_row()
        {
            var collections = new ChildCollections(1);
            var noKey = ChildCollections.MakeKey(new object[0], new int[0]);
            collections.Add(0, new ChildRow("a", noKey));
            collections.Add(0, new ChildRow("b", noKey));

            CollectionAssert.AreEqual(new[] { "a", "b" }, collections.GetList<string>(0, ChildCollections.MakeKey(new object[] { 5 }, new int[0])));
        }
    }
}
