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
    ///         Backlog item 22, against a real SQL Server: an entity whose mapped columns are declared on a
    ///         base class, queried, saved, updated and deleted through every path that reads a member.
    ///         <see cref="BaseClassMemberResolutionTests"/> only looks at the SQL text, and text alone
    ///         cannot show that a value really lands in, or comes back from, the right column.
    ///     </para>
    ///     <para>
    ///         The table is <c>PricedItem</c>. Every row is read back with raw SQL, so what is asserted is
    ///         what the table holds and not what the entity says it holds.
    ///     </para>
    /// </summary>
    [TestClass]
    public class BaseClassExecutionTests
    {
        private const string ServerConnectionString =
            "Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True";

        [TestInitialize]
        public void EnsureDatabase() => new TestDatabaseSetup(ServerConnectionString).Setup();

        public abstract class InhPricedBase : Record
        {
            [PrimaryKey]
            [DbIdentityColumn]
            public int Id { get; set; }

            public string Name { get; set; }

            [DbColumn("NetAmt")]
            public decimal? Net { get; set; }

            // annotated here, overridden without the annotation by the derived class
            [DbColumn("Curr")]
            public virtual string Currency { get; set; }

            // computed by the database: read back, never written
            [DbReadOnlyColumn]
            [DbColumn("GrossAmt")]
            public decimal? Gross { get; set; }
        }

        [DbTable("PricedItem", "dbo")]
        public class InhPricedItem : InhPricedBase
        {
            public override string Currency { get; set; }
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

        private static InhPricedItem Insert(OrmDbContext db, string name, decimal net, string currency)
        {
            var item = new InhPricedItem { Name = name, Net = net, Currency = currency, RecordState = RecordState.Added };
            db.SaveEntity(item);
            return item;
        }

        [TestMethod]
        public void Insert_writes_base_and_overridden_columns_and_reads_back_identity_and_computed_column()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var item = Insert(db, "Inh_Insert", 10m, "USD");

                Assert.IsTrue(item.Id > 0, "The identity declared on the base class comes back.");
                Assert.AreEqual(20m, item.Gross, "The computed column declared on the base class comes back.");
                var row = ReadRow(db, item.Id);
                Assert.AreEqual("Inh_Insert", row["Name"]);
                Assert.AreEqual(10m, row["NetAmt"]);
                Assert.AreEqual("USD", row["Curr"]);
            });
        }

        [TestMethod]
        public void Update_through_save_rewrites_the_columns_and_refreshes_the_computed_column()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var item = Insert(db, "Inh_Update", 10m, "USD");

                item.Net = 15m;
                item.Currency = "EUR";
                item.RecordState = RecordState.Updated;
                db.SaveEntity(item);

                Assert.AreEqual(30m, item.Gross);
                var row = ReadRow(db, item.Id);
                Assert.AreEqual(15m, row["NetAmt"]);
                Assert.AreEqual("EUR", row["Curr"]);
            });
        }

        [TestMethod]
        public void UpdateEntity_selectors_on_base_and_overridden_members_write_the_right_columns()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var item = Insert(db, "Inh_UpdateEntity", 10m, "USD");
                var other = Insert(db, "Inh_UpdateEntity_Other", 10m, "USD");

                var affected = db.UpdateEntity<InhPricedItem>()
                                 .Set(x => x.Net, () => 7m)
                                 .Set(x => x.Currency, () => "GBP")
                                 .Key(x => x.Id, () => item.Id)
                                 .Execute();

                Assert.AreEqual(1, affected);
                var row = ReadRow(db, item.Id);
                Assert.AreEqual(7m, row["NetAmt"]);
                Assert.AreEqual("GBP", row["Curr"]);
                Assert.AreEqual("USD", ReadRow(db, other.Id)["Curr"], "The key, declared on the base class, must keep the update to its own row.");
            });
        }

        [TestMethod]
        public void DeleteEntity_keyed_on_a_base_member_deletes_only_that_row()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var item = Insert(db, "Inh_Delete", 10m, "USD");
                var other = Insert(db, "Inh_Delete_Other", 10m, "USD");

                var affected = db.DeleteEntity<InhPricedItem>()
                                 .Key(x => x.Id, () => item.Id)
                                 .Execute();

                Assert.AreEqual(1, affected);
                var remaining = db.GetDbCommunication().ExecuteDictionary(
                    "select Id from dbo.PricedItem where Id in (@a, @b)",
                    new[] { new SqlParameter("@a", item.Id), new SqlParameter("@b", other.Id) },
                    CommandType.Text);
                CollectionAssert.AreEqual(new object[] { other.Id }, remaining.Select(x => x["Id"]).ToArray());
            });
        }

        [TestMethod]
        public void Query_filters_and_materializes_through_base_and_overridden_members()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var keep = Insert(db, "Inh_Query_Keep", 10m, "USD");
                Insert(db, "Inh_Query_WrongCurrency", 10m, "EUR");
                Insert(db, "Inh_Query_TooSmall", 1m, "USD");

                var rows = db.CreateQuery<InhPricedItem>()
                             .Where(x => x.Name.StartsWith("Inh_Query_") && x.Net > 5m && x.Currency == "USD")
                             .ToList();

                Assert.AreEqual(1, rows.Count);
                Assert.AreEqual(keep.Id, rows[0].Id);
                Assert.AreEqual("Inh_Query_Keep", rows[0].Name);
                Assert.AreEqual(10m, rows[0].Net);
                Assert.AreEqual("USD", rows[0].Currency);
                Assert.AreEqual(20m, rows[0].Gross);

                var grosses = db.CreateQuery<InhPricedItem>()
                                .Where(x => x.Id == keep.Id)
                                .Select(x => x.Gross)
                                .ToList();
                CollectionAssert.AreEqual(new decimal?[] { 20m }, grosses);
            });
        }
    }
}
