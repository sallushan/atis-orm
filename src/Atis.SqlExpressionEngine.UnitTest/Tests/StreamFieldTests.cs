using Atis.Orm;
using Atis.Orm.Abstractions;
using Atis.Orm.DataAccess;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         The streaming terminals' own behaviour, driven through a stand-in reader rather than a
    ///         database: what happens when there is no row, what happens when the column is null, who
    ///         disposes what, and whether reads are seen by the concurrency detector.
    ///     </para>
    ///     <para>
    ///         These are the things worth testing without a server, because they are all about ownership
    ///         and none of them about SQL. <see cref="StreamFieldExecutionTests"/> covers the other half —
    ///         that a real provider actually streams the bytes back.
    ///     </para>
    /// </summary>
    [TestClass]
    public class StreamFieldTests
    {
        private static IQueryable<TestEntities.Document> Query(FakeReaderSessionProvider provider)
            => new Queryable<TestEntities.Document>(provider);

        // ---------------------------------------------------------------- nothing to read

        /// <summary>
        ///     No row and a null column are one answer to the caller, and both have to give the
        ///     connection back — nobody is handed anything to dispose.
        /// </summary>
        [TestMethod]
        public void No_row_returns_null_and_disposes_the_session()
        {
            var provider = FakeReaderSessionProvider.WithNoRows();

            var stream = Query(provider).StreamField(x => x.Content);

            Assert.IsNull(stream);
            Assert.IsTrue(provider.Session.Disposed, "A session nobody was handed must not keep the connection.");
        }

        [TestMethod]
        public void Null_column_returns_null_and_disposes_the_session()
        {
            var provider = FakeReaderSessionProvider.WithRow(content: null);

            var stream = Query(provider).StreamField(x => x.Content);

            Assert.IsNull(stream);
            Assert.IsTrue(provider.Session.Disposed);
        }

        [TestMethod]
        public void No_row_returns_null_from_the_text_terminal_too()
        {
            var provider = FakeReaderSessionProvider.WithNoRows();

            var reader = Query(provider).StreamTextField(x => x.Body);

            Assert.IsNull(reader);
            Assert.IsTrue(provider.Session.Disposed);
        }

        [TestMethod]
        public async Task No_row_returns_null_asynchronously()
        {
            var provider = FakeReaderSessionProvider.WithNoRows();

            var stream = await Query(provider).StreamFieldAsync(x => x.Content);

            Assert.IsNull(stream);
            Assert.IsTrue(provider.Session.Disposed);
        }

        // ---------------------------------------------------------------- ownership

        /// <summary>
        ///     The point of the whole shape: one thing to dispose, and disposing it is what gives the
        ///     connection back.
        /// </summary>
        [TestMethod]
        public void Disposing_the_stream_disposes_the_session()
        {
            var provider = FakeReaderSessionProvider.WithRow(new byte[] { 1, 2, 3 });

            var stream = Query(provider).StreamField(x => x.Content);
            Assert.IsFalse(provider.Session.Disposed, "The session is held open for as long as the stream is.");

            stream.Dispose();

            Assert.IsTrue(provider.Session.Disposed);
        }

        [TestMethod]
        public void Disposing_the_stream_twice_releases_the_session_once()
        {
            var provider = FakeReaderSessionProvider.WithRow(new byte[] { 1, 2, 3 });

            var stream = Query(provider).StreamField(x => x.Content);
            stream.Dispose();
            stream.Dispose();

            Assert.AreEqual(1, provider.Session.DisposeCount);
        }

        [TestMethod]
        public void Reading_after_dispose_is_reported_as_such()
        {
            var provider = FakeReaderSessionProvider.WithRow(new byte[] { 1, 2, 3 });
            var stream = Query(provider).StreamField(x => x.Content);
            stream.Dispose();

            Assert.ThrowsException<ObjectDisposedException>(() => stream.ReadByte());
        }

        // ---------------------------------------------------------------- the bytes

        [TestMethod]
        public void The_bytes_come_back_unchanged()
        {
            var content = Encoding.UTF8.GetBytes("the quick brown fox");
            var provider = FakeReaderSessionProvider.WithRow(content);

            using (var stream = Query(provider).StreamField(x => x.Content))
            using (var copy = new MemoryStream())
            {
                stream.CopyTo(copy);
                CollectionAssert.AreEqual(content, copy.ToArray());
            }
        }

        [TestMethod]
        public void The_characters_come_back_unchanged()
        {
            var provider = FakeReaderSessionProvider.WithRow(body: "line one\nline two");

            using (var reader = Query(provider).StreamTextField(x => x.Body))
            {
                Assert.AreEqual("line one\nline two", reader.ReadToEnd());
            }
        }

        // ---------------------------------------------------------------- size

        /// <summary>
        ///     A streamed value has no length the driver can report, so asking for one without having
        ///     named a size column has to say so rather than guess.
        /// </summary>
        [TestMethod]
        public void Length_without_a_size_column_says_why_it_is_not_known()
        {
            var provider = FakeReaderSessionProvider.WithRow(new byte[] { 1, 2, 3 });

            using (var stream = Query(provider).StreamField(x => x.Content))
            {
                var error = Assert.ThrowsException<NotSupportedException>(() => _ = stream.Length);
                StringAssert.Contains(error.Message, "StreamField");
            }
        }

        [TestMethod]
        public void Length_reports_the_named_size_column()
        {
            var provider = FakeReaderSessionProvider.WithRow(new byte[] { 1, 2, 3 }, contentLength: 3);

            using (var stream = Query(provider).StreamField(x => x.Content, x => x.ContentLength))
            {
                Assert.AreEqual(3L, stream.Length);
            }
        }

        /// <summary>
        ///     The size column is read before the value, because a sequential reader gives no way back
        ///     once the value has been touched. This asserts the ordinals the projection produced.
        /// </summary>
        [TestMethod]
        public void The_size_column_is_selected_before_the_value()
        {
            var provider = FakeReaderSessionProvider.WithRow(new byte[] { 1, 2, 3 }, contentLength: 3);

            using (Query(provider).StreamField(x => x.Content, x => x.ContentLength))
            {
                CollectionAssert.AreEqual(new[] { 0, 1 }, provider.Session.Reader.OrdinalsRead.Distinct().ToArray());
                Assert.AreEqual(0, provider.Session.Reader.OrdinalsRead[0], "The size has to be read first.");
            }
        }

        // ---------------------------------------------------------------- progress

        [TestMethod]
        public void Progress_reports_the_running_total_of_bytes()
        {
            var provider = FakeReaderSessionProvider.WithRow(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            var reported = new List<long>();

            using (var stream = Query(provider).StreamField(x => x.Content, progress: new ImmediateProgress(reported.Add)))
            {
                var buffer = new byte[4];
                while (stream.Read(buffer, 0, buffer.Length) > 0)
                {
                }
            }

            CollectionAssert.AreEqual(new[] { 4L, 8L, 10L }, reported.ToArray());
        }

        [TestMethod]
        public void Progress_is_optional()
        {
            var provider = FakeReaderSessionProvider.WithRow(new byte[] { 1, 2, 3 });

            using (var stream = Query(provider).StreamField(x => x.Content))
            {
                Assert.AreEqual(1, stream.ReadByte());
                Assert.AreEqual(1L, stream.Position);
            }
        }

        // ---------------------------------------------------------------- the concurrency guard

        /// <summary>
        ///     <para>
        ///         The reason the session is handed to the stream at all. Bytes pulled off a column are
        ///         operations on the connection exactly as reading a row is, and they happen long after
        ///         the element factory returned — for a large value, the longest window in the whole API.
        ///         Without this they would be the one part of the reader protocol a second flow could
        ///         overlap unnoticed.
        ///     </para>
        /// </summary>
        [TestMethod]
        public void Every_read_is_one_operation_on_the_session()
        {
            var provider = FakeReaderSessionProvider.WithRow(new byte[] { 1, 2, 3, 4, 5, 6 });

            int atHandover;
            using (var stream = Query(provider).StreamField(x => x.Content))
            {
                atHandover = provider.Session.CriticalSectionCount;

                var buffer = new byte[2];
                stream.Read(buffer, 0, buffer.Length);
                stream.Read(buffer, 0, buffer.Length);
            }

            Assert.AreEqual(atHandover + 2, provider.Session.CriticalSectionCount,
                "Two reads, two operations -- the section covers a read and nothing longer.");
        }

        [TestMethod]
        public void Every_text_read_is_one_operation_on_the_session()
        {
            var provider = FakeReaderSessionProvider.WithRow(body: "abcdef");

            int atHandover;
            using (var reader = Query(provider).StreamTextField(x => x.Body))
            {
                atHandover = provider.Session.CriticalSectionCount;
                reader.ReadToEnd();
            }

            Assert.AreEqual(atHandover + 1, provider.Session.CriticalSectionCount);
        }

        // ---------------------------------------------------------------- misuse

        [TestMethod]
        public void A_provider_that_cannot_stream_is_told_so()
        {
            var query = new Queryable<TestEntities.Document>(new CannotStreamProvider());

            var error = Assert.ThrowsException<InvalidOperationException>(
                () => query.StreamField(x => x.Content));

            StringAssert.Contains(error.Message, "does not support streaming");
        }

        [TestMethod]
        public void A_field_that_is_not_a_member_is_rejected()
        {
            var provider = FakeReaderSessionProvider.WithRow(new byte[] { 1 });

            Assert.ThrowsException<ArgumentException>(
                () => Query(provider).StreamField(x => x.Content.Reverse().ToArray()));
        }

        // ================================================================ doubles

        /// <summary>Reports on the calling thread, so a test can assert what was reported.</summary>
        private sealed class ImmediateProgress : IProgress<long>
        {
            private readonly Action<long> report;
            public ImmediateProgress(Action<long> report) => this.report = report;
            public void Report(long value) => this.report(value);
        }

        private sealed class CannotStreamProvider : IQueryProvider
        {
            public IQueryable CreateQuery(Expression expression) => throw new NotSupportedException();
            public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
                => new Queryable<TElement>(this, expression);
            public object Execute(Expression expression) => throw new NotSupportedException();
            public TResult Execute<TResult>(Expression expression) => throw new NotSupportedException();
        }

        /// <summary>
        ///     Stands in for the whole compile-and-open path. The expression is not translated at all —
        ///     what is being tested here is what the terminal does with the session it gets back, and the
        ///     ordinals the factory reads, both of which are decided before any SQL exists.
        /// </summary>
        private sealed class FakeReaderSessionProvider : IQueryProvider, IReaderSessionProvider
        {
            private FakeReaderSessionProvider(FakeReaderSession session) => this.Session = session;

            public FakeReaderSession Session { get; }

            public static FakeReaderSessionProvider WithNoRows()
                => new FakeReaderSessionProvider(new FakeReaderSession(new FakeDataReader(null, null, null), hasRow: false));

            public static FakeReaderSessionProvider WithRow(byte[] content = null, string body = null, long? contentLength = null)
                => new FakeReaderSessionProvider(new FakeReaderSession(new FakeDataReader(content, body, contentLength), hasRow: true));

            public IQueryable CreateQuery(Expression expression) => throw new NotSupportedException();
            public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new Queryable<TElement>(this, expression);
            public object Execute(Expression expression) => throw new NotSupportedException();
            public TResult Execute<TResult>(Expression expression) => throw new NotSupportedException();

            public IDbReaderSession OpenReader(Expression expression, Func<IDataReader, object> elementFactory)
            {
                this.Session.ElementFactory = elementFactory;
                return this.Session;
            }

            public Task<IDbReaderSession> OpenReaderAsync(Expression expression, Func<IDataReader, object> elementFactory, CancellationToken cancellationToken)
                => Task.FromResult(this.OpenReader(expression, elementFactory));
        }

        /// <summary>
        ///     One row, or none. Counts what the stream asks of it, which is how the disposal and
        ///     critical-section assertions are made.
        /// </summary>
        private sealed class FakeReaderSession : IDbReaderSession
        {
            private readonly bool hasRow;
            private bool read;
            private object current;
            private bool currentIsSet;

            public FakeReaderSession(FakeDataReader reader, bool hasRow)
            {
                this.Reader = reader;
                this.hasRow = hasRow;
            }

            public FakeDataReader Reader { get; }
            public Func<IDataReader, object> ElementFactory { get; set; }
            public int DisposeCount { get; private set; }
            public bool Disposed => this.DisposeCount > 0;
            public int CriticalSectionCount { get; private set; }

            public bool Read()
            {
                this.CriticalSectionCount++;
                if (this.read)
                    return false;
                this.read = true;
                return this.hasRow;
            }

            public Task<bool> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(this.Read());

            public object Current
            {
                get
                {
                    this.CriticalSectionCount++;
                    if (!this.currentIsSet)
                    {
                        this.current = this.ElementFactory(this.Reader);
                        this.currentIsSet = true;
                    }
                    return this.current;
                }
            }

            // Counted rather than enforced: the assertions are about how often the stream marks an
            // operation, not about detecting a second flow, which ConcurrencyDetectionTests covers.
            public ConcurrencyDetectorCriticalSection EnterCriticalSection()
            {
                this.CriticalSectionCount++;
                return default;
            }

            public void Dispose() => this.DisposeCount++;

            public ValueTask DisposeAsync()
            {
                this.Dispose();
                return default;
            }
        }

        /// <summary>
        ///     A <see cref="DbDataReader"/> over one row of <c>Document</c>, holding only what the
        ///     streaming factory reads: the size at ordinal 0 when there is one, the value after it.
        ///     Everything else throws, so a test that starts reading columns it did not set up fails
        ///     rather than quietly passing.
        /// </summary>
        private sealed class FakeDataReader : DbDataReader
        {
            private readonly byte[] content;
            private readonly string body;
            private readonly long? size;

            public FakeDataReader(byte[] content, string body, long? size)
            {
                this.content = content;
                this.body = body;
                this.size = size;
            }

            /// <summary>Every ordinal touched, in order, so the projection's column order can be asserted.</summary>
            public List<int> OrdinalsRead { get; } = new List<int>();

            private int ValueOrdinal => this.size.HasValue ? 1 : 0;

            public override bool IsDBNull(int ordinal)
            {
                this.OrdinalsRead.Add(ordinal);
                if (ordinal == 0 && this.size.HasValue)
                    return false;
                return this.content is null && this.body is null;
            }

            public override object GetValue(int ordinal)
            {
                this.OrdinalsRead.Add(ordinal);
                if (ordinal == 0 && this.size.HasValue)
                    return this.size.Value;
                throw new InvalidOperationException($"Ordinal {ordinal} was read as a value, not as a stream.");
            }

            public override Stream GetStream(int ordinal)
            {
                this.OrdinalsRead.Add(ordinal);
                Assert.AreEqual(this.ValueOrdinal, ordinal, "The value sits after the size column.");
                return new MemoryStream(this.content, writable: false);
            }

            public override TextReader GetTextReader(int ordinal)
            {
                this.OrdinalsRead.Add(ordinal);
                Assert.AreEqual(this.ValueOrdinal, ordinal, "The value sits after the size column.");
                return new StringReader(this.body);
            }

            public override int FieldCount => this.size.HasValue ? 2 : 1;
            public override bool HasRows => true;
            public override bool IsClosed => false;
            public override int RecordsAffected => -1;
            public override int Depth => 0;

            public override object this[int ordinal] => throw new NotSupportedException();
            public override object this[string name] => throw new NotSupportedException();
            public override bool GetBoolean(int ordinal) => throw new NotSupportedException();
            public override byte GetByte(int ordinal) => throw new NotSupportedException();
            public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length) => throw new NotSupportedException();
            public override char GetChar(int ordinal) => throw new NotSupportedException();
            public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length) => throw new NotSupportedException();
            public override string GetDataTypeName(int ordinal) => throw new NotSupportedException();
            public override DateTime GetDateTime(int ordinal) => throw new NotSupportedException();
            public override decimal GetDecimal(int ordinal) => throw new NotSupportedException();
            public override double GetDouble(int ordinal) => throw new NotSupportedException();
            public override IEnumerator GetEnumerator() => throw new NotSupportedException();
            public override Type GetFieldType(int ordinal) => throw new NotSupportedException();
            public override float GetFloat(int ordinal) => throw new NotSupportedException();
            public override Guid GetGuid(int ordinal) => throw new NotSupportedException();
            public override short GetInt16(int ordinal) => throw new NotSupportedException();
            public override int GetInt32(int ordinal) => throw new NotSupportedException();
            public override long GetInt64(int ordinal) => throw new NotSupportedException();
            public override string GetName(int ordinal) => throw new NotSupportedException();
            public override int GetOrdinal(string name) => throw new NotSupportedException();
            public override string GetString(int ordinal) => throw new NotSupportedException();
            public override int GetValues(object[] values) => throw new NotSupportedException();
            public override bool NextResult() => false;
            public override bool Read() => throw new NotSupportedException();
        }
    }
}
