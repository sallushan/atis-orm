using Atis.Orm;
using Atis.Orm.Annotations;
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
    ///         <c>SaveWithProgress</c> against a real SQL Server. Whether <c>.WRITE</c> appends where the
    ///         persister thinks it does, whether the placeholder clears what an update replaces, and
    ///         whether the chunks and the row really share one transaction are all the database's answer
    ///         to give.
    ///     </para>
    ///     <para>
    ///         The chunk sizes are tiny so that a few kilobytes make several chunks. What comes back is
    ///         read with raw SQL, so a test does not depend on the read path it is not about.
    ///     </para>
    /// </summary>
    [TestClass]
    public class StreamedSaveExecutionTests
    {
        private const string ServerConnectionString =
            "Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True";

        [TestInitialize]
        public void EnsureDatabase() => new TestDatabaseSetup(ServerConnectionString).Setup();

        /// <summary>The <c>Document</c> table, as a <see cref="Record"/> so it can go through the save methods.</summary>
        [DbTable("Document", "dbo")]
        public class StoredDocument : Record
        {
            [PrimaryKey]
            [DbIdentityColumn]
            public int DocumentId { get; set; }
            public string Name { get; set; }
            public byte[] Content { get; set; }
            public long? ContentLength { get; set; }
            public string Body { get; set; }
            public long? BodyLength { get; set; }
        }

        private static byte[] Bytes(int length, int seed = 0)
        {
            var bytes = new byte[length];
            for (var i = 0; i < length; i++)
                bytes[i] = (byte)((i + seed) % 251);
            return bytes;
        }

        private static string Text(int length, char first = 'a')
        {
            var text = new StringBuilder(length);
            for (var i = 0; i < length; i++)
                text.Append((char)(first + i % 26));
            return text.ToString();
        }

        private static IReadOnlyDictionary<string, object> ReadRow(OrmDbContext db, int documentId)
        {
            var rows = db.GetDbCommunication().ExecuteDictionary(
                "select Name, Content, Body from dbo.Document where DocumentId = @id",
                new[] { new SqlParameter("@id", documentId) },
                CommandType.Text);
            Assert.AreEqual(1, rows.Count);
            return rows[0];
        }

        private static string Describe(ColumnWriteProgress p)
            => $"{p.ColumnName} {p.ColumnIndex}/{p.TotalColumns} {p.BytesWritten}/{p.TotalBytes?.ToString() ?? "?"}";

        [TestMethod]
        public void Insert_writes_large_in_memory_columns_in_chunks_and_reports_each_one()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var content = Bytes(2500);
                var body = Text(1200);
                var document = new StoredDocument { Name = "Streamed_Insert", Content = content, Body = body, RecordState = RecordState.Added };
                var progress = new List<string>();

                var rows = db.SaveWithProgress(document, p => progress.Add(Describe(p)), chunkSizeBytes: 1000);

                Assert.AreEqual(1, rows);
                Assert.IsTrue(document.DocumentId > 0, "The identity key is assigned back before the chunks need it.");
                // Content in 1000 byte chunks; Body in 500 character chunks, half the byte size.
                CollectionAssert.AreEqual(
                    new[]
                    {
                        "Content 1/2 1000/2500", "Content 1/2 2000/2500", "Content 1/2 2500/2500",
                        "Body 2/2 500/1200", "Body 2/2 1000/1200", "Body 2/2 1200/1200",
                    },
                    progress);

                var row = ReadRow(db, document.DocumentId);
                CollectionAssert.AreEqual(content, (byte[])row["Content"]);
                Assert.AreEqual(body, row["Body"]);
                Assert.AreSame(content, document.Content, "The entity keeps the value the caller gave it.");
                Assert.AreSame(body, document.Body);
            });
        }

        [TestMethod]
        public void Values_no_larger_than_one_chunk_go_in_the_statement_without_progress()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var content = Bytes(1000);
                var document = new StoredDocument { Name = "Streamed_Small", Content = content, Body = Text(500), RecordState = RecordState.Added };
                var progress = new List<ColumnWriteProgress>();

                db.SaveWithProgress(document, progress.Add, chunkSizeBytes: 1000);

                Assert.AreEqual(0, progress.Count);
                var row = ReadRow(db, document.DocumentId);
                CollectionAssert.AreEqual(content, (byte[])row["Content"]);
                Assert.AreEqual(document.Body, row["Body"]);
            });
        }

        [TestMethod]
        public void Insert_reads_a_mapped_stream_and_leaves_the_member_and_the_stream_alone()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var content = Bytes(2300, seed: 7);
                var source = new MemoryStream(content);
                var document = new StoredDocument { Name = "Streamed_Stream", RecordState = RecordState.Added };
                var progress = new List<string>();

                db.SaveWithProgress(
                    document,
                    p => progress.Add(Describe(p)),
                    map => map.Stream(x => x.Content, source),
                    chunkSizeBytes: 1000);

                CollectionAssert.AreEqual(
                    new[] { "Content 1/1 1000/2300", "Content 1/1 2000/2300", "Content 1/1 2300/2300" },
                    progress);
                CollectionAssert.AreEqual(content, (byte[])ReadRow(db, document.DocumentId)["Content"]);
                Assert.IsNull(document.Content);
                Assert.IsTrue(source.CanRead, "The caller's stream is not disposed.");
                Assert.AreEqual(source.Length, source.Position);
            });
        }

        [TestMethod]
        public void A_stream_that_cannot_seek_reports_no_total()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var content = Bytes(1500);
                var document = new StoredDocument { Name = "Streamed_Forward", RecordState = RecordState.Added };
                var progress = new List<string>();

                db.SaveWithProgress(
                    document,
                    p => progress.Add(Describe(p)),
                    map => map.Stream(x => x.Content, new ForwardOnlyStream(content)),
                    chunkSizeBytes: 1000);

                CollectionAssert.AreEqual(new[] { "Content 1/1 1000/?", "Content 1/1 1500/?" }, progress);
                CollectionAssert.AreEqual(content, (byte[])ReadRow(db, document.DocumentId)["Content"]);
            });
        }

        [TestMethod]
        public void Insert_reads_a_mapped_text_reader()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var body = Text(1100, 'A');
                var document = new StoredDocument { Name = "Streamed_Text", RecordState = RecordState.Added };
                var progress = new List<string>();

                db.SaveWithProgress(
                    document,
                    p => progress.Add(Describe(p)),
                    map => map.Text(x => x.Body, new StringReader(body)),
                    chunkSizeBytes: 1000);

                CollectionAssert.AreEqual(new[] { "Body 1/1 500/?", "Body 1/1 1000/?", "Body 1/1 1100/?" }, progress);
                Assert.AreEqual(body, ReadRow(db, document.DocumentId)["Body"]);
            });
        }

        [TestMethod]
        public void An_empty_stream_writes_an_empty_value_and_reports_once()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var document = new StoredDocument { Name = "Streamed_Empty", RecordState = RecordState.Added };
                var progress = new List<string>();

                db.SaveWithProgress(
                    document,
                    p => progress.Add(Describe(p)),
                    map => map.Stream(x => x.Content, new MemoryStream()),
                    chunkSizeBytes: 1000);

                CollectionAssert.AreEqual(new[] { "Content 1/1 0/0" }, progress);
                CollectionAssert.AreEqual(new byte[0], (byte[])ReadRow(db, document.DocumentId)["Content"]);
            });
        }

        [TestMethod]
        public void Update_replaces_a_longer_old_value_rather_than_appending_to_it()
        {
            var db = new OrmDbContext();
            db.TransactionWithRollback(() =>
            {
                var document = new StoredDocument { Name = "Streamed_Update", Content = Bytes(3000), Body = Text(2000), RecordState = RecordState.Added };
                db.SaveWithProgress(document, null, chunkSizeBytes: 1000);

                var newContent = Bytes(1200, seed: 99);
                document.Content = newContent;
                document.Body = "short";
                document.RecordState = RecordState.Updated;
                var progress = new List<string>();

                var rows = db.SaveWithProgress(document, p => progress.Add(Describe(p)), chunkSizeBytes: 1000);

                Assert.AreEqual(1, rows);
                CollectionAssert.AreEqual(new[] { "Content 1/1 1000/1200", "Content 1/1 1200/1200" }, progress);
                var row = ReadRow(db, document.DocumentId);
                CollectionAssert.AreEqual(newContent, (byte[])row["Content"]);
                Assert.AreEqual("short", row["Body"]);
            });
        }

        [TestMethod]
        public void A_failure_part_way_through_leaves_no_row_behind()
        {
            var db = new OrmDbContext();
            var name = "Streamed_Failure_" + Guid.NewGuid().ToString("N");
            try
            {
                var document = new StoredDocument { Name = name, RecordState = RecordState.Added };

                Assert.ThrowsException<IOException>(() => db.SaveWithProgress(
                    document,
                    null,
                    map => map.Stream(x => x.Content, new ForwardOnlyStream(Bytes(5000), failAfter: 2500)),
                    chunkSizeBytes: 1000));

                var count = db.GetDbCommunication().ExecuteScalarCommand<int>(
                    "select count(*) from dbo.Document where Name = @name",
                    new[] { new SqlParameter("@name", name) },
                    CommandType.Text);
                Assert.AreEqual(0, count, "The row and its chunks are one transaction.");
            }
            finally
            {
                db.GetDbCommunication().ExecuteNonQueryCommand(
                    "delete from dbo.Document where Name = @name",
                    new[] { new SqlParameter("@name", name) },
                    CommandType.Text);
            }
        }

        [TestMethod]
        public async Task SaveWithProgressAsync_streams_and_reports_like_the_synchronous_one()
        {
            var db = new OrmDbContext();
            await db.TransactionWithRollbackAsync(async () =>
            {
                var content = Bytes(2100);
                var body = Text(700);
                var document = new StoredDocument { Name = "Streamed_Async", Body = body, RecordState = RecordState.Added };
                var progress = new List<string>();

                var rows = await db.SaveWithProgressAsync(
                    document,
                    p => progress.Add(Describe(p)),
                    map => map.Stream(x => x.Content, new ForwardOnlyStream(content)),
                    chunkSizeBytes: 1000);

                Assert.AreEqual(1, rows);
                CollectionAssert.AreEqual(
                    new[]
                    {
                        "Content 1/2 1000/?", "Content 1/2 2000/?", "Content 1/2 2100/?",
                        "Body 2/2 500/700", "Body 2/2 700/700",
                    },
                    progress);
                var row = ReadRow(db, document.DocumentId);
                CollectionAssert.AreEqual(content, (byte[])row["Content"]);
                Assert.AreEqual(body, row["Body"]);
            });
        }

        [TestMethod]
        public void Mapping_the_same_column_twice_is_rejected()
        {
            var db = new OrmDbContext();
            var document = new StoredDocument { DocumentId = 1, Name = "x", RecordState = RecordState.Updated };

            var ex = Assert.ThrowsException<ArgumentException>(() => db.SaveWithProgress(
                document,
                null,
                map => map.Text(x => x.Name, new StringReader("x")).Text(x => x.Name, new StringReader("y"))));

            StringAssert.Contains(ex.Message, "more than once");
        }

        /// <summary>
        ///     A stream that cannot seek, so its length is unknown, and that hands out at most 300 bytes
        ///     per read — the short reads the chunk reader has to fill past. It can also fail part way,
        ///     like a network stream dropping.
        /// </summary>
        private sealed class ForwardOnlyStream : Stream
        {
            private readonly byte[] data;
            private readonly int failAfter;
            private int position;

            public ForwardOnlyStream(byte[] data, int failAfter = int.MaxValue)
            {
                this.data = data;
                this.failAfter = failAfter;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (this.position >= this.failAfter)
                    throw new IOException("The source failed.");

                var read = Math.Min(Math.Min(count, 300), this.data.Length - this.position);
                Array.Copy(this.data, this.position, buffer, offset, read);
                this.position += read;
                return read;
            }

            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
