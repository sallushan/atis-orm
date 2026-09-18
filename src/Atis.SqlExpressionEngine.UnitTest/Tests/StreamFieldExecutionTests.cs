using Atis.Orm;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         The streaming terminals against a real SQL Server — the half
    ///         <see cref="StreamFieldTests"/> cannot cover. Whether a column actually comes back through
    ///         <c>GetStream</c>, whether the sequential reader lets the size column be read before the
    ///         value, and whether <c>SelectFields(...).Take(1)</c> translates at all are all the
    ///         provider's answer to give, and a stand-in reader would only confirm what the test assumed.
    ///     </para>
    ///     <para>
    ///         Every test seeds its own rows inside <c>TransactionWithRollback</c>, so nothing is left
    ///         behind and each one knows exactly which row it is reading.
    ///     </para>
    /// </summary>
    [TestClass]
    public class StreamFieldExecutionTests
    {
        private const string ServerConnectionString =
            "Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True";

        [TestInitialize]
        public void EnsureDatabase() => new TestDatabaseSetup(ServerConnectionString).Setup();

        /// <summary>A payload big enough that streaming it is several reads rather than one.</summary>
        private static byte[] LargeContent(int length = 300_000)
        {
            var content = new byte[length];
            for (var i = 0; i < length; i++)
                content[i] = (byte)(i % 251);
            return content;
        }

        /// <summary>
        ///     <para>
        ///         Seeds one document with raw SQL rather than through <c>InsertEntity</c>. Not a
        ///         preference: the fluent insert cannot write a <c>byte[]</c> member at all today —
        ///         <c>CompositeMemberAssignmentConverterBase</c> sees a non-string <c>IEnumerable</c> and
        ///         demands a <c>SqlDerivedTableExpression</c>, so the assignment fails to convert. That is
        ///         a bug in the insert path, not in anything these tests assert, and seeding around it
        ///         keeps them about streaming.
        ///     </para>
        /// </summary>
        private static int Seed(OrmDbContext db, string name, byte[] content = null, string body = null)
        {
            var db2 = db.GetDbCommunication();
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@name", SqlDbType.NVarChar, 200) { Value = name },
                new SqlParameter("@content", SqlDbType.VarBinary, -1) { Value = (object)content ?? DBNull.Value },
                new SqlParameter("@contentLength", SqlDbType.BigInt) { Value = content is null ? (object)DBNull.Value : (long)content.Length },
                new SqlParameter("@body", SqlDbType.NVarChar, -1) { Value = (object)body ?? DBNull.Value },
                new SqlParameter("@bodyLength", SqlDbType.BigInt) { Value = body is null ? (object)DBNull.Value : (long)body.Length },
            };

            var id = db2.ExecuteScalarCommand<decimal?>(
                @"insert into dbo.Document (Name, Content, ContentLength, Body, BodyLength)
                  values (@name, @content, @contentLength, @body, @bodyLength);
                  select SCOPE_IDENTITY();",
                parameters,
                CommandType.Text);

            return (int)id.Value;
        }

        private static IQueryable<TestEntities.Document> Documents(OrmDbContext db, string name)
            => db.CreateQuery<TestEntities.Document>().Where(x => x.Name == name);

        // ---------------------------------------------------------------- the bytes

        [TestMethod]
        public void Streams_a_binary_column_back_unchanged()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var content = LargeContent();
                Seed(db, "stream.binary", content);

                using (var stream = Documents(db, "stream.binary").StreamField(x => x.Content))
                using (var copy = new MemoryStream())
                {
                    Assert.IsNotNull(stream);
                    stream.CopyTo(copy);
                    CollectionAssert.AreEqual(content, copy.ToArray());
                }
            });
        }

        [TestMethod]
        public void Streams_a_text_column_back_unchanged()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var body = string.Join("\n", Enumerable.Range(0, 20000).Select(i => $"line {i}"));
                Seed(db, "stream.text", body: body);

                using (var reader = Documents(db, "stream.text").StreamTextField(x => x.Body))
                {
                    Assert.IsNotNull(reader);
                    Assert.AreEqual(body, reader.ReadToEnd());
                }
            });
        }

        [TestMethod]
        public async Task Streams_a_binary_column_back_unchanged_asynchronously()
        {
            var db = new OrmDbContext();
            await db.TransactionWithRollbackAsync(async () =>
            {
                var content = LargeContent();
                Seed(db, "stream.binary.async", content);

                using (var stream = await Documents(db, "stream.binary.async").StreamFieldAsync(x => x.Content))
                using (var copy = new MemoryStream())
                {
                    Assert.IsNotNull(stream);
                    await stream.CopyToAsync(copy);
                    CollectionAssert.AreEqual(content, copy.ToArray());
                }
            });
        }

        [TestMethod]
        public async Task Streams_a_text_column_back_unchanged_asynchronously()
        {
            var db = new OrmDbContext();
            await db.TransactionWithRollbackAsync(async () =>
            {
                Seed(db, "stream.text.async", body: "a short body");

                using (var reader = await Documents(db, "stream.text.async").StreamTextFieldAsync(x => x.Body))
                {
                    Assert.IsNotNull(reader);
                    Assert.AreEqual("a short body", await reader.ReadToEndAsync());
                }
            });
        }

        // ---------------------------------------------------------------- size

        /// <summary>
        ///     The size column is selected first and read before the value, which is the only order a
        ///     sequential reader allows. A wrong order would not merely report a wrong number — the read
        ///     would fail outright, which is what makes this worth asserting against a real reader.
        /// </summary>
        [TestMethod]
        public void Reports_the_length_from_the_named_size_column()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var content = LargeContent(12345);
                Seed(db, "stream.sized", content);

                using (var stream = Documents(db, "stream.sized").StreamField(x => x.Content, x => x.ContentLength))
                {
                    Assert.AreEqual(12345L, stream.Length);

                    using (var copy = new MemoryStream())
                    {
                        stream.CopyTo(copy);
                        Assert.AreEqual(12345, copy.Length, "The value still reads in full after the size.");
                    }
                }
            });
        }

        [TestMethod]
        public void Reports_the_length_for_a_text_column_too()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                Seed(db, "stream.text.sized", body: new string('x', 5000));

                using (var reader = Documents(db, "stream.text.sized").StreamTextField(x => x.Body, x => x.BodyLength))
                {
                    Assert.AreEqual(5000L, reader.Length);
                    Assert.AreEqual(5000, reader.ReadToEnd().Length);
                }
            });
        }

        // ---------------------------------------------------------------- nothing to read

        [TestMethod]
        public void A_null_column_gives_null()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                Seed(db, "stream.nullcolumn", content: null);

                Assert.IsNull(Documents(db, "stream.nullcolumn").StreamField(x => x.Content));
            });
        }

        [TestMethod]
        public void No_matching_row_gives_null()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                Assert.IsNull(Documents(db, "stream.nosuchrow").StreamField(x => x.Content));
            });
        }

        // ---------------------------------------------------------------- the query is a real query

        /// <summary>
        ///     The reason this is a query terminal rather than a keyed <c>Get</c>: whatever the query can
        ///     express is available, sub-queries included.
        /// </summary>
        [TestMethod]
        public void The_row_can_be_chosen_through_a_sub_query()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var content = LargeContent(4096);
                var id = Seed(db, "stream.subquery", content);

                var all = db.CreateQuery<TestEntities.Document>();
                var query = all.Where(d => all.Any(o => o.DocumentId == d.DocumentId && o.Name == "stream.subquery"));

                using (var stream = query.StreamField(x => x.Content))
                using (var copy = new MemoryStream())
                {
                    Assert.IsNotNull(stream);
                    stream.CopyTo(copy);
                    CollectionAssert.AreEqual(content, copy.ToArray());
                }

                Assert.IsTrue(id > 0);
            });
        }

        /// <summary>
        ///     Two rows match and one stream comes back. The limit is in the statement, because whether a
        ///     second row exists cannot be checked afterwards without advancing the reader past the row
        ///     the stream is sitting on.
        /// </summary>
        [TestMethod]
        public void Several_matching_rows_still_give_one_value()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var first = LargeContent(2048);
                var second = LargeContent(4096);
                Seed(db, "stream.many", first);
                Seed(db, "stream.many", second);

                using (var stream = Documents(db, "stream.many").StreamField(x => x.Content))
                using (var copy = new MemoryStream())
                {
                    Assert.IsNotNull(stream);
                    stream.CopyTo(copy);
                    var read = copy.ToArray();
                    Assert.IsTrue(read.SequenceEqual(first) || read.SequenceEqual(second),
                        "One of the matching rows, in full.");
                }
            });
        }

        // ---------------------------------------------------------------- the connection comes back

        /// <summary>
        ///     A stream holds the reader, the command and a claim on the connection until it is disposed.
        ///     The claim being given back is what lets the next query run at all, so it is asserted by
        ///     running one.
        /// </summary>
        [TestMethod]
        public void Disposing_the_stream_frees_the_connection_for_the_next_query()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                Seed(db, "stream.release", LargeContent(1024));

                using (var stream = Documents(db, "stream.release").StreamField(x => x.Content))
                {
                    Assert.IsNotNull(stream);
                }

                var names = Documents(db, "stream.release").Select(x => x.Name).ToList();
                Assert.AreEqual(1, names.Count);
            });
        }

        /// <summary>Progress runs off the real reader, not a memory stream, so the chunking is the driver's.</summary>
        [TestMethod]
        public void Progress_reports_the_running_total_while_copying()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var content = LargeContent(200_000);
                Seed(db, "stream.progress", content);

                var reported = new List<long>();
                using (var stream = Documents(db, "stream.progress")
                                        .StreamField(x => x.Content, x => x.ContentLength, new SynchronousProgress(reported.Add)))
                using (var copy = new MemoryStream())
                {
                    stream.CopyTo(copy);
                }

                Assert.IsTrue(reported.Count > 1, "A 200 KB value is more than one read.");
                Assert.AreEqual(200_000L, reported[reported.Count - 1], "The last report is the whole value.");
                CollectionAssert.AreEqual(reported.OrderBy(x => x).ToList(), reported, "Progress only goes up.");
            });
        }

        private sealed class SynchronousProgress : IProgress<long>
        {
            private readonly Action<long> report;
            public SynchronousProgress(Action<long> report) => this.report = report;
            public void Report(long value) => this.report(value);
        }
    }
}
