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
    ///         Backlog item 23, against a real SQL Server: C# <c>record</c> types as entities. A record cannot
    ///         derive from <see cref="Record"/>, so it carries its write intent through <see cref="IRecord"/>.
    ///     </para>
    ///     <para>
    ///         The table is <c>PricedItem</c>. Rows are read back with raw SQL, so what is asserted is what the
    ///         table holds.
    ///     </para>
    /// </summary>
    [TestClass]
    public class RecordTypeExecutionTests
    {
        private const string ServerConnectionString =
            "Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True";

        [TestInitialize]
        public void EnsureDatabase() => new TestDatabaseSetup(ServerConnectionString).Setup();

        /// <summary>A record with settable, <c>init</c> and computed members, implementing <see cref="IRecord"/>.</summary>
        [DbTable("PricedItem", "dbo")]
        public record RecItem : IRecord
        {
            [PrimaryKey]
            [DbIdentityColumn]
            public int Id { get; init; }

            public string Name { get; init; }

            [DbColumn("NetAmt")]
            public decimal? Net { get; init; }

            [DbColumn("Curr")]
            public string Currency { get; init; }

            [DbReadOnlyColumn]
            [DbColumn("GrossAmt")]
            public decimal? Gross { get; init; }

            // no [DbNotMapped]: IRecord's state is never a column
            public RecordState RecordState { get; init; }
        }

        /// <summary>A positional record: no parameterless constructor.</summary>
        [DbTable("PricedItem", "dbo")]
        public record PositionalItem(
            [property: PrimaryKey] [property: DbIdentityColumn] int Id,
            string Name,
            [property: DbColumn("NetAmt")] decimal? Net);

        private static IReadOnlyDictionary<string, object> ReadRow(OrmDbContext db, int id)
        {
            var rows = db.GetDbCommunication().ExecuteDictionary(
                "select Name, NetAmt, Curr, GrossAmt from dbo.PricedItem where Id = @id",
                new[] { new SqlParameter("@id", id) },
                CommandType.Text);
            Assert.AreEqual(1, rows.Count);
            return rows[0];
        }

        [TestMethod]
        public void Save_inserts_a_record_and_writes_the_generated_values_back_through_init_setters()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var item = new RecItem { Name = "Rec_Insert", Net = 10m, Currency = "USD", RecordState = RecordState.Added };

                db.SaveEntity(item);

                Assert.IsTrue(item.Id > 0, "The identity comes back into the init property.");
                Assert.AreEqual(20m, item.Gross, "The computed column comes back into the init property.");
                var row = ReadRow(db, item.Id);
                Assert.AreEqual("Rec_Insert", row["Name"]);
                Assert.AreEqual(10m, row["NetAmt"]);
                Assert.AreEqual("USD", row["Curr"]);
            });
        }

        [TestMethod]
        public void Save_updates_and_deletes_a_record_using_with_to_change_its_state()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var item = new RecItem { Name = "Rec_Update", Net = 10m, Currency = "USD", RecordState = RecordState.Added };
                db.SaveEntity(item);

                var changed = item with { Net = 15m, Currency = "EUR", RecordState = RecordState.Updated };
                db.SaveEntity(changed);

                var row = ReadRow(db, item.Id);
                Assert.AreEqual(15m, row["NetAmt"]);
                Assert.AreEqual("EUR", row["Curr"]);

                db.SaveEntity(changed with { RecordState = RecordState.Deleted });
                var remaining = db.GetDbCommunication().ExecuteDictionary(
                    "select Id from dbo.PricedItem where Id = @id",
                    new[] { new SqlParameter("@id", item.Id) },
                    CommandType.Text);
                Assert.AreEqual(0, remaining.Count);
            });
        }

        [TestMethod]
        public void Query_materializes_a_record_with_init_properties()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var saved = new RecItem { Name = "Rec_Query", Net = 10m, Currency = "USD", RecordState = RecordState.Added };
                db.SaveEntity(saved);

                var rows = db.CreateQuery<RecItem>().Where(x => x.Id == saved.Id).ToList();

                Assert.AreEqual(1, rows.Count);
                Assert.AreEqual("Rec_Query", rows[0].Name);
                Assert.AreEqual(20m, rows[0].Gross);
                Assert.AreEqual(RecordState.Unchanged, rows[0].RecordState);
            });
        }

        [TestMethod]
        public void Query_materializes_a_positional_record_through_its_constructor()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var saved = new RecItem { Name = "Rec_Positional", Net = 10m, Currency = "USD", RecordState = RecordState.Added };
                db.SaveEntity(saved);

                var rows = db.CreateQuery<PositionalItem>().Where(x => x.Id == saved.Id).ToList();

                Assert.AreEqual(1, rows.Count);
                Assert.AreEqual(new PositionalItem(saved.Id, "Rec_Positional", 10m), rows[0]);
            });
        }

        /// <summary>A constructor argument is mapped to the property of the same name, so the projection can be filtered on afterwards.</summary>
        [TestMethod]
        public void Select_into_a_positional_record_constructor_maps_arguments_by_name()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var saved = new RecItem { Name = "Rec_Ctor", Net = 10m, Currency = "USD", RecordState = RecordState.Added };
                db.SaveEntity(saved);

                var rows = db.CreateQuery<RecItem>()
                             .Where(x => x.Id == saved.Id)
                             .Select(x => new PositionalItem(x.Id, x.Name + "!", x.Net * 2))
                             .Where(x => x.Name == "Rec_Ctor!")
                             .ToList();

                Assert.AreEqual(1, rows.Count);
                Assert.AreEqual(new PositionalItem(saved.Id, "Rec_Ctor!", 20m), rows[0]);
            });
        }

        public class NoMatchingMember
        {
            public NoMatchingMember(int id, string label) { Id = id; }
            public int Id { get; }
        }

        [TestMethod]
        public void Select_into_a_constructor_with_an_unmatched_parameter_says_which_one()
        {
            var db = new OrmDbContext();
            var thrown = Assert.ThrowsException<InvalidOperationException>(
                () => db.CreateQuery<RecItem>().Select(x => new NoMatchingMember(x.Id, x.Name)).ToList());

            StringAssert.Contains(thrown.ToString(), "label");
        }

        [TestMethod]
        public void A_record_that_implements_IRecord_has_no_RecordState_column()
        {
            var metadata = new Atis.Orm.Metadata.EntityMetadataBuilder(new Atis.Orm.Services.OrmReflectionService())
                .Build(typeof(RecItem));

            Assert.IsFalse(metadata.SqlColumns.Any(x => x.ModelPropertyName == nameof(RecItem.RecordState)));
            Assert.IsTrue(metadata.SqlColumns.Any(x => x.ModelPropertyName == nameof(RecItem.Name)));
        }
    }
}
