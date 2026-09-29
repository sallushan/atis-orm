using Atis.Orm;
using Atis.Orm.Annotations;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         Saving an entity that owns a value object, against a real SQL Server. The persister's own
    ///         tests assert the statements it builds; what only a database can answer is whether the
    ///         computed column really comes back onto the value object, and whether a null value object
    ///         really lands as NULLs.
    ///     </para>
    ///     <para>
    ///         Every row is read back with raw SQL, so what is asserted is what the table holds and not
    ///         what the entity says it holds.
    ///     </para>
    /// </summary>
    [TestClass]
    public class ValueObjectSaveExecutionTests
    {
        private const string ServerConnectionString =
            "Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True";

        [TestInitialize]
        public void EnsureDatabase() => new TestDatabaseSetup(ServerConnectionString).Setup();

        /// <summary><c>Gross</c> is a computed column, so it is read back and never written.</summary>
        public class StoredPrice
        {
            public decimal Net { get; set; }

            public string Currency { get; set; }

            [DbReadOnlyColumn]
            public decimal? Gross { get; set; }
        }

        [DbTable("PricedItem", "dbo")]
        public class StoredPricedItem : Record
        {
            [PrimaryKey]
            [DbIdentityColumn]
            public int Id { get; set; }

            public string Name { get; set; }

            [ValueObject(
                new[] { nameof(StoredPrice.Net), nameof(StoredPrice.Currency), nameof(StoredPrice.Gross) },
                new[] { "NetAmt", "Curr", "GrossAmt" })]
            public StoredPrice Price { get; set; }
        }

        private static IReadOnlyDictionary<string, object> ReadRow(OrmDbContext db, int id)
        {
            var rows = db.GetDbCommunication().ExecuteDictionary(
                "select Name, NetAmt, Curr, GrossAmt from dbo.PricedItem where Id = @id",
                new[] { new SqlParameter("@id", id) },
                CommandType.Text);
            Assert.AreEqual(1, rows.Count);
            return rows[0];
        }

        // The row dictionaries carry a real null for a NULL column; DBNull is accepted too so this does not
        // depend on which the reader hands over.
        private static void AssertNull(IReadOnlyDictionary<string, object> row, string column)
            => Assert.IsTrue(row[column] is null || row[column] is DBNull, $"{column} should be NULL but was '{row[column]}'.");

        [TestMethod]
        public void Insert_writes_the_value_object_and_reads_back_the_computed_column()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var item = new StoredPricedItem
                {
                    Name = "VO_Insert",
                    Price = new StoredPrice { Net = 10m, Currency = "USD" },
                    RecordState = RecordState.Added,
                };

                db.SaveEntity(item);

                Assert.IsTrue(item.Id > 0);
                Assert.AreEqual(20m, item.Price.Gross, "The computed column comes back onto the value object.");
                var row = ReadRow(db, item.Id);
                Assert.AreEqual(10m, row["NetAmt"]);
                Assert.AreEqual("USD", row["Curr"]);
                Assert.AreEqual(20m, row["GrossAmt"]);
            });
        }

        [TestMethod]
        public void Insert_of_a_null_value_object_writes_nulls_and_leaves_it_null()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var item = new StoredPricedItem { Name = "VO_NullInsert", Price = null, RecordState = RecordState.Added };

                db.SaveEntity(item);

                Assert.IsNull(item.Price, "The database computed NULL, so nothing needs a value object to sit in.");
                var row = ReadRow(db, item.Id);
                AssertNull(row, "NetAmt");
                AssertNull(row, "Curr");
                AssertNull(row, "GrossAmt");
            });
        }

        [TestMethod]
        public void Update_rewrites_the_value_object_and_refreshes_the_computed_column()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var item = new StoredPricedItem
                {
                    Name = "VO_Update",
                    Price = new StoredPrice { Net = 10m, Currency = "USD" },
                    RecordState = RecordState.Added,
                };
                db.SaveEntity(item);

                item.Price.Net = 15m;
                item.Price.Currency = "EUR";
                item.RecordState = RecordState.Updated;
                db.SaveEntity(item);

                Assert.AreEqual(30m, item.Price.Gross);
                var row = ReadRow(db, item.Id);
                Assert.AreEqual(15m, row["NetAmt"]);
                Assert.AreEqual("EUR", row["Curr"]);
                Assert.AreEqual(30m, row["GrossAmt"]);
            });
        }

        [TestMethod]
        public void Update_to_a_null_value_object_writes_nulls_over_the_old_values()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var item = new StoredPricedItem
                {
                    Name = "VO_UpdateNull",
                    Price = new StoredPrice { Net = 10m, Currency = "USD" },
                    RecordState = RecordState.Added,
                };
                db.SaveEntity(item);

                item.Price = null;
                item.RecordState = RecordState.Updated;
                db.SaveEntity(item);

                var row = ReadRow(db, item.Id);
                AssertNull(row, "NetAmt");
                AssertNull(row, "Curr");
                AssertNull(row, "GrossAmt");
            });
        }

        /// <summary>
        ///     What was saved is what a query gets back: the value object is built from its columns,
        ///     including the computed one.
        /// </summary>
        [TestMethod]
        public void A_saved_value_object_is_read_back_by_a_query()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var item = new StoredPricedItem
                {
                    Name = "VO_Query",
                    Price = new StoredPrice { Net = 7m, Currency = "GBP" },
                    RecordState = RecordState.Added,
                };
                db.SaveEntity(item);
                var id = item.Id;

                var loaded = db.CreateQuery<StoredPricedItem>().Where(x => x.Id == id).First();

                Assert.AreEqual(7m, loaded.Price.Net);
                Assert.AreEqual("GBP", loaded.Price.Currency);
                Assert.AreEqual(14m, loaded.Price.Gross);
            });
        }
    }
}
