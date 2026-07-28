using Atis.Orm.SqlServer;
using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         Covers <c>DbCommunicationBase.ExecuteScalarCommand</c> against a real SQL Server, driving
    ///         <see cref="SqlDbCommunication"/> directly rather than through a <c>DataContext</c> — the
    ///         behaviour under test belongs to the base class, and there is nothing for the query
    ///         translator to do here.
    ///     </para>
    ///     <para>
    ///         A real server matters for these: what <c>ExecuteScalar</c> hands back is the provider's
    ///         choice, and the conversion is only worth testing against the types SQL Server actually
    ///         returns — <c>COUNT</c> as <c>int</c>, an aggregate over no rows as <c>DBNull</c>, no row at
    ///         all as a null reference. A fake command returning canned values would be asserting the
    ///         test's own assumptions.
    ///     </para>
    /// </summary>
    [TestClass]
    public class ScalarExecutionTests
    {
        private const string ConnectionString =
            "server=localhost;database=TestDb;integrated security=true;TrustServerCertificate=True";

        private enum Tag
        {
            One = 1,
            Two = 2,
            Three = 3,
        }

        [TestInitialize]
        public void ResetTable()
        {
            Execute(@"
if object_id('dbo.ScalarTest') is null
    create table dbo.ScalarTest (Id int not null primary key, Tag varchar(50) not null);
delete from dbo.ScalarTest;
insert into dbo.ScalarTest (Id, Tag) values (1, 'One'), (2, 'Two'), (3, 'Three');");
        }

        // ---------- the value ----------

        [TestMethod]
        public void ReturnsTheFirstColumnOfTheFirstRow()
        {
            var db = new SqlDbCommunication(ConnectionString);

            var tag = db.ExecuteScalarCommand<string>(
                "select Tag, Id from dbo.ScalarTest order by Id", null, CommandType.Text);

            Assert.AreEqual("One", tag, "Later rows and later columns must be ignored.");
        }

        /// <summary>
        ///     <c>COUNT</c> comes back as <c>int</c>, so asking for <c>long</c> is a conversion rather than
        ///     a cast — the case a plain <c>(T)value</c> would fail on.
        /// </summary>
        [TestMethod]
        public void ConvertsToTheRequestedType_WhenItIsNotWhatTheProviderReturned()
        {
            var db = new SqlDbCommunication(ConnectionString);

            var count = db.ExecuteScalarCommand<long>(
                "select count(*) from dbo.ScalarTest", null, CommandType.Text);

            Assert.AreEqual(3L, count);
        }

        /// <summary>
        ///     An aggregate over no rows yields one row holding <c>NULL</c>, which is a real
        ///     <c>DBNull</c> on the wire — not the same shape as the no-row case below.
        /// </summary>
        [TestMethod]
        public void NullValue_ComesBackAsDefault()
        {
            var db = new SqlDbCommunication(ConnectionString);

            var max = db.ExecuteScalarCommand<int>(
                "select max(Id) from dbo.ScalarTest where 1 = 0", null, CommandType.Text);

            Assert.AreEqual(0, max);
        }

        [TestMethod]
        public void NullValue_ComesBackAsNull_ForANullableType()
        {
            var db = new SqlDbCommunication(ConnectionString);

            var max = db.ExecuteScalarCommand<int?>(
                "select max(Id) from dbo.ScalarTest where 1 = 0", null, CommandType.Text);

            Assert.IsNull(max, "A nullable T must be able to tell NULL apart from zero.");
        }

        /// <summary>
        ///     No row at all: <c>ExecuteScalar</c> returns a null reference rather than <c>DBNull</c>.
        /// </summary>
        [TestMethod]
        public void NoRow_ComesBackAsDefault()
        {
            var db = new SqlDbCommunication(ConnectionString);

            Assert.AreEqual(0, db.ExecuteScalarCommand<int>(
                "select Id from dbo.ScalarTest where Id = 99", null, CommandType.Text));
            Assert.IsNull(db.ExecuteScalarCommand<string>(
                "select Tag from dbo.ScalarTest where Id = 99", null, CommandType.Text));
        }

        [TestMethod]
        public void AppliesTheParameters()
        {
            var db = new SqlDbCommunication(ConnectionString);

            var tag = db.ExecuteScalarCommand<string>(
                "select Tag from dbo.ScalarTest where Id = @id",
                new DbParameter[] { new SqlParameter("@id", 2) },
                CommandType.Text);

            Assert.AreEqual("Two", tag);
        }

        /// <summary>
        ///     Both storage shapes for an enum, neither of which <c>Convert.ChangeType</c> can produce on
        ///     its own: the underlying number, and the member name from a varchar column.
        /// </summary>
        [TestMethod]
        public void ConvertsToAnEnum_FromEitherItsNumberOrItsName()
        {
            var db = new SqlDbCommunication(ConnectionString);

            Assert.AreEqual(Tag.Two, db.ExecuteScalarCommand<Tag>(
                "select Id from dbo.ScalarTest where Id = 2", null, CommandType.Text));
            Assert.AreEqual(Tag.Two, db.ExecuteScalarCommand<Tag>(
                "select Tag from dbo.ScalarTest where Id = 2", null, CommandType.Text));
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

            Assert.AreEqual(3, db.ExecuteScalarCommand<int>("select count(*) from dbo.ScalarTest", null, CommandType.Text));
            Assert.AreEqual(3, db.ExecuteScalarCommand<int>("select count(*) from dbo.ScalarTest", null, CommandType.Text));
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
                    "insert into dbo.ScalarTest (Id, Tag) values (4, 'Four')", null, CommandType.Text);

                Assert.AreEqual(4, db.ExecuteScalarCommand<int>(
                    "select count(*) from dbo.ScalarTest", null, CommandType.Text));
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
                    () => db.ExecuteScalarCommand<int>("select 1", null, CommandType.Text));

                StringAssert.Contains(thrown.Message, "ExecuteScalarCommand",
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
        public async Task Async_ReturnsTheValue_Converted()
        {
            var db = new SqlDbCommunication(ConnectionString);

            var count = await db.ExecuteScalarCommandAsync<long>(
                "select count(*) from dbo.ScalarTest", null, CommandType.Text, CancellationToken.None);

            Assert.AreEqual(3L, count);
        }

        [TestMethod]
        public async Task Async_NullValue_ComesBackAsDefault()
        {
            var db = new SqlDbCommunication(ConnectionString);

            var max = await db.ExecuteScalarCommandAsync<int?>(
                "select max(Id) from dbo.ScalarTest where 1 = 0", null, CommandType.Text, CancellationToken.None);

            Assert.IsNull(max);
        }

        [TestMethod]
        public async Task Async_EnlistsInTheCurrentTransaction()
        {
            var db = new SqlDbCommunication(ConnectionString);

            await db.TransactionAsync(async () =>
            {
                await db.ExecuteNonQueryCommandAsync(
                    "insert into dbo.ScalarTest (Id, Tag) values (4, 'Four')", null, CommandType.Text, CancellationToken.None);

                Assert.AreEqual(4, await db.ExecuteScalarCommandAsync<int>(
                    "select count(*) from dbo.ScalarTest", null, CommandType.Text, CancellationToken.None));
            });
        }

        // ---------- helpers ----------

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
