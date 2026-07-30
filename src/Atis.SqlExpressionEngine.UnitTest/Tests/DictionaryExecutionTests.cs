using Atis.Orm.SqlServer;
using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         Covers <c>DbCommunicationBase.ExecuteDictionary</c> against a real SQL Server, driving
    ///         <see cref="SqlDbCommunication"/> directly rather than through a <c>DataContext</c> — the
    ///         behaviour under test belongs to the base class, and there is nothing for the query
    ///         translator to do here.
    ///     </para>
    ///     <para>
    ///         A real server matters for these for the same reason it does in
    ///         <see cref="ScalarExecutionTests"/>: the column names, the CLR types behind the values and
    ///         the <c>DBNull</c> for a null column are all the provider's choice, and a fake reader
    ///         handing back canned rows would only assert what the test already assumed.
    ///     </para>
    /// </summary>
    [TestClass]
    public class DictionaryExecutionTests
    {
        private const string ConnectionString =
            "server=localhost;database=TestDb;integrated security=true;TrustServerCertificate=True";

        [TestInitialize]
        public void ResetTable()
        {
            Execute(@"
if object_id('dbo.DictionaryTest') is null
    create table dbo.DictionaryTest (Id int not null primary key, Tag nvarchar(50) null, Amount decimal(18, 2) null);
delete from dbo.DictionaryTest;
insert into dbo.DictionaryTest (Id, Tag, Amount) values (1, 'One', 10.50), (2, 'Two', null), (3, null, 30.00);");
        }

        // ---------- the rows ----------

        [TestMethod]
        public void ReturnsEveryRow_InOrder_KeyedByColumnName()
        {
            var db = new SqlDbCommunication(ConnectionString);

            var rows = db.ExecuteDictionary(
                "select Id, Tag from dbo.DictionaryTest order by Id", null, CommandType.Text);

            Assert.AreEqual(3, rows.Count, "Every row of the result set must come back, not just the first.");
            Assert.AreEqual(2, rows[0].Count, "Only the selected columns are keys.");
            Assert.AreEqual(1, rows[0]["Id"]);
            Assert.AreEqual("One", rows[0]["Tag"]);
            Assert.AreEqual(3, rows[2]["Id"]);
        }

        /// <summary>
        ///     The server treats identifiers case-insensitively, so a caller writing the column name from
        ///     memory should not have to match the casing the query happened to use.
        /// </summary>
        [TestMethod]
        public void LooksUpColumnNames_CaseInsensitively()
        {
            var db = new SqlDbCommunication(ConnectionString);

            var row = db.ExecuteDictionary(
                "select Tag from dbo.DictionaryTest where Id = 1", null, CommandType.Text).Single();

            Assert.AreEqual("One", row["tag"]);
            Assert.AreEqual("One", row["TAG"]);
            Assert.IsTrue(row.ContainsKey("tAg"));
        }

        /// <summary>
        ///     A null column is <c>DBNull</c> on the wire. It must arrive as a plain <c>null</c> — and the
        ///     key must still be there, so "the column was null" stays distinguishable from "there is no
        ///     such column".
        /// </summary>
        [TestMethod]
        public void NullColumn_ComesBackAsNull_WithTheKeyStillPresent()
        {
            var db = new SqlDbCommunication(ConnectionString);

            var row = db.ExecuteDictionary(
                "select Id, Tag, Amount from dbo.DictionaryTest where Id = 2", null, CommandType.Text).Single();

            Assert.IsTrue(row.ContainsKey("Amount"));
            Assert.IsNull(row["Amount"]);
            Assert.AreEqual("Two", row["Tag"]);
        }

        [TestMethod]
        public void NoRows_ComesBackAsAnEmptyList()
        {
            var db = new SqlDbCommunication(ConnectionString);

            var rows = db.ExecuteDictionary(
                "select Id, Tag from dbo.DictionaryTest where 1 = 0", null, CommandType.Text);

            Assert.IsNotNull(rows, "An empty result set is not the same thing as no result.");
            Assert.AreEqual(0, rows.Count);
        }

        [TestMethod]
        public void AppliesTheParameters()
        {
            var db = new SqlDbCommunication(ConnectionString);

            var rows = db.ExecuteDictionary(
                "select Id, Tag from dbo.DictionaryTest where Id = @id",
                new DbParameter[] { new SqlParameter("@id", 2) },
                CommandType.Text);

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("Two", rows[0]["Tag"]);
        }

        /// <summary>
        ///     One of the two columns would silently overwrite the other, so the caller is told to alias
        ///     them instead — and told which name collided.
        /// </summary>
        [TestMethod]
        public void DuplicateColumnNames_Throw()
        {
            var db = new SqlDbCommunication(ConnectionString);

            var thrown = Assert.ThrowsException<InvalidOperationException>(
                () => db.ExecuteDictionary("select Id, Id from dbo.DictionaryTest", null, CommandType.Text));

            StringAssert.Contains(thrown.Message, "Id", "The message must name the column that collided.");
        }

        /// <summary>
        ///     The seam a provider for a case-sensitive database would use. With
        ///     <see cref="StringComparer.Ordinal"/> the loose lookup stops working and, in exchange, two
        ///     columns differing only in case become two distinct keys instead of a duplicate.
        /// </summary>
        [TestMethod]
        public void DictionaryKeyComparer_CanBeOverridden_ToBeCaseSensitive()
        {
            var db = new CaseSensitiveCommunication(ConnectionString);

            var row = db.ExecuteDictionary(
                "select Tag from dbo.DictionaryTest where Id = 1", null, CommandType.Text).Single();

            Assert.AreEqual("One", row["Tag"]);
            Assert.IsFalse(row.ContainsKey("tag"), "An exact comparer must not match on a different casing.");

            var both = db.ExecuteDictionary(
                "select Id as v, Id as V from dbo.DictionaryTest where Id = 1", null, CommandType.Text).Single();

            Assert.AreEqual(2, both.Count, "'v' and 'V' are distinct columns under an exact comparer.");
        }

        // ---------- connection and transaction ----------

        /// <summary>
        ///     The command owns the connection for its own duration and must hand it back — a leak would
        ///     leave <c>_localConnection</c> set and trip the guard on the second call.
        /// </summary>
        [TestMethod]
        public void RunsAgain_OnTheSameInstance()
        {
            var db = new SqlDbCommunication(ConnectionString);

            Assert.AreEqual(3, db.ExecuteDictionary("select Id from dbo.DictionaryTest", null, CommandType.Text).Count);
            Assert.AreEqual(3, db.ExecuteDictionary("select Id from dbo.DictionaryTest", null, CommandType.Text).Count);
        }

        /// <summary>
        ///     Runs on the transaction's connection, so it sees the transaction's own uncommitted work.
        ///     A command that failed to enlist would either block or read the pre-insert state.
        /// </summary>
        [TestMethod]
        public void EnlistsInTheCurrentTransaction()
        {
            var db = new SqlDbCommunication(ConnectionString);

            db.Transaction(() =>
            {
                db.ExecuteNonQueryCommand(
                    "insert into dbo.DictionaryTest (Id, Tag, Amount) values (4, 'Four', null)", null, CommandType.Text);

                Assert.AreEqual(4, db.ExecuteDictionary(
                    "select Id from dbo.DictionaryTest", null, CommandType.Text).Count);
            });
        }

        /// <summary>
        ///     The guard: a reader still being enumerated holds the instance's own connection open, and
        ///     running here would close it out from under that reader.
        /// </summary>
        [TestMethod]
        public void WhileTheInstanceHoldsItsOwnConnection_Throws()
        {
            var db = new SqlDbCommunication(ConnectionString);

            // What DbEnumerator does before it opens a reader.
            db.OpenConnection();
            try
            {
                var thrown = Assert.ThrowsException<InvalidOperationException>(
                    () => db.ExecuteDictionary("select 1 as One", null, CommandType.Text));

                StringAssert.Contains(thrown.Message, "ExecuteDictionary",
                    "The message must name the method that was refused.");
                StringAssert.Contains(thrown.Message, "data reader");
            }
            finally
            {
                db.CloseConnection();
            }
        }

        // ---------- async ----------

        [TestMethod]
        public async Task Async_ReturnsEveryRow_InOrder_KeyedByColumnName()
        {
            var db = new SqlDbCommunication(ConnectionString);

            var rows = await db.ExecuteDictionaryAsync(
                "select Id, Tag from dbo.DictionaryTest order by Id", null, CommandType.Text, CancellationToken.None);

            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual(1, rows[0]["Id"]);
            Assert.AreEqual("One", rows[0]["tag"]);
        }

        [TestMethod]
        public async Task Async_NullColumn_ComesBackAsNull_WithTheKeyStillPresent()
        {
            var db = new SqlDbCommunication(ConnectionString);

            var rows = await db.ExecuteDictionaryAsync(
                "select Id, Amount from dbo.DictionaryTest where Id = 2", null, CommandType.Text, CancellationToken.None);

            Assert.IsTrue(rows.Single().ContainsKey("Amount"));
            Assert.IsNull(rows.Single()["Amount"]);
        }

        [TestMethod]
        public async Task Async_NoRows_ComesBackAsAnEmptyList()
        {
            var db = new SqlDbCommunication(ConnectionString);

            var rows = await db.ExecuteDictionaryAsync(
                "select Id from dbo.DictionaryTest where 1 = 0", null, CommandType.Text, CancellationToken.None);

            Assert.IsNotNull(rows);
            Assert.AreEqual(0, rows.Count);
        }

        [TestMethod]
        public async Task Async_RunsAgain_OnTheSameInstance()
        {
            var db = new SqlDbCommunication(ConnectionString);

            Assert.AreEqual(3, (await db.ExecuteDictionaryAsync(
                "select Id from dbo.DictionaryTest", null, CommandType.Text, CancellationToken.None)).Count);
            Assert.AreEqual(3, (await db.ExecuteDictionaryAsync(
                "select Id from dbo.DictionaryTest", null, CommandType.Text, CancellationToken.None)).Count);
        }

        [TestMethod]
        public async Task Async_EnlistsInTheCurrentTransaction()
        {
            var db = new SqlDbCommunication(ConnectionString);

            await db.TransactionAsync(async () =>
            {
                await db.ExecuteNonQueryCommandAsync(
                    "insert into dbo.DictionaryTest (Id, Tag, Amount) values (4, 'Four', null)", null, CommandType.Text, CancellationToken.None);

                Assert.AreEqual(4, (await db.ExecuteDictionaryAsync(
                    "select Id from dbo.DictionaryTest", null, CommandType.Text, CancellationToken.None)).Count);
            });
        }

        // ---------- helpers ----------

        /// <summary>
        ///     Stands in for a provider whose database can return two columns differing only in case --
        ///     PostgreSQL and Oracle can, through quoted identifiers.
        /// </summary>
        private sealed class CaseSensitiveCommunication : SqlDbCommunication
        {
            public CaseSensitiveCommunication(string connString) : base(connString) { }

            protected override StringComparer DictionaryKeyComparer => StringComparer.Ordinal;
        }

        private static void Execute(string sql)
        {
            using var connection = new SqlConnection(ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
    }
}
