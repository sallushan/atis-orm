using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Atis.Orm.DataManipulation
{
    /// <summary>
    ///     <para>
    ///         Where a streamed column's value comes from: a caller's <see cref="Stream"/> or
    ///         <see cref="TextReader"/>, or an in-memory <c>byte[]</c> or <c>string</c> too large to send in
    ///         one statement. Each kind cuts itself into the chunks that are sent to the database.
    ///     </para>
    ///     <para>
    ///         The set of kinds is closed — the constructor is internal — so a source is always one the
    ///         persister knows how to write. A binary source is a <see cref="ColumnWriteSource{TChunk}"/>
    ///         of <c>byte[]</c> and a text source one of <c>string</c>, which is how the persister picks the
    ///         provider's binary or text chunk method without inspecting a value.
    ///     </para>
    ///     <para>
    ///         A source is read once, from where it stands to its end, one chunk at a time, and is never
    ///         held in memory beyond one chunk. A caller's stream or reader stays the caller's: it is not
    ///         disposed.
    ///     </para>
    /// </summary>
    public abstract class ColumnWriteSource
    {
        internal ColumnWriteSource()
        {
        }

        /// <summary>A source that reads a binary column's value from <paramref name="stream"/>.</summary>
        public static ColumnWriteSource FromStream(Stream stream)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead)
                throw new ArgumentException("The stream cannot be read.", nameof(stream));
            return new StreamSource(stream);
        }

        /// <summary>A source that reads a text column's value from <paramref name="reader"/>.</summary>
        public static ColumnWriteSource FromReader(TextReader reader)
        {
            if (reader is null)
                throw new ArgumentNullException(nameof(reader));
            return new TextReaderSource(reader);
        }

        /// <summary>A source over a binary value already in memory.</summary>
        internal static ColumnWriteSource FromBytes(byte[] value) => new BytesSource(value);

        /// <summary>A source over a text value already in memory.</summary>
        internal static ColumnWriteSource FromText(string value) => new TextSource(value);

        /// <summary>Whether this source writes a <c>string</c> member; otherwise a <c>byte[]</c> member.</summary>
        public abstract bool IsText { get; }

        /// <summary>
        ///     The length to be written — bytes for binary, characters for text — or <c>null</c> when it
        ///     cannot be known in advance: a stream that cannot seek, or any reader.
        /// </summary>
        internal abstract long? TotalLength { get; }

        /// <summary>The characters in a text chunk of <paramref name="chunkSizeBytes"/>: two bytes to a character.</summary>
        internal static int CharsPerChunk(int chunkSizeBytes) => Math.Max(1, chunkSizeBytes / 2);

        private sealed class BytesSource : ColumnWriteSource<byte[]>
        {
            private readonly byte[] value;
            private int position;

            public BytesSource(byte[] value) => this.value = value;

            public override bool IsText => false;
            internal override long? TotalLength => this.value.Length;
            internal override int LengthOf(byte[] chunk) => chunk.Length;

            internal override byte[] ReadNext(int chunkSizeBytes)
            {
                var length = Math.Min(chunkSizeBytes, this.value.Length - this.position);
                if (length <= 0)
                    return null;

                var chunk = new byte[length];
                Buffer.BlockCopy(this.value, this.position, chunk, 0, length);
                this.position += length;
                return chunk;
            }
        }

        private sealed class TextSource : ColumnWriteSource<string>
        {
            private readonly string value;
            private int position;

            public TextSource(string value) => this.value = value;

            public override bool IsText => true;
            internal override long? TotalLength => this.value.Length;
            internal override int LengthOf(string chunk) => chunk.Length;

            internal override string ReadNext(int chunkSizeBytes)
            {
                var length = Math.Min(CharsPerChunk(chunkSizeBytes), this.value.Length - this.position);
                if (length <= 0)
                    return null;

                var chunk = this.value.Substring(this.position, length);
                this.position += length;
                return chunk;
            }
        }

        private sealed class StreamSource : ColumnWriteSource<byte[]>
        {
            private readonly Stream stream;
            private byte[] buffer;

            public StreamSource(Stream stream)
            {
                this.stream = stream;
                // Read now, before any chunk moves the position.
                this.TotalLength = stream.CanSeek ? Math.Max(0, stream.Length - stream.Position) : (long?)null;
            }

            public override bool IsText => false;
            internal override long? TotalLength { get; }
            internal override int LengthOf(byte[] chunk) => chunk.Length;

            internal override byte[] ReadNext(int chunkSizeBytes)
            {
                var buffer = this.GetBuffer(chunkSizeBytes);
                // A single Read may return less than asked long before the end; a chunk cut short that way
                // would only mean more round trips, so keep reading until the buffer is full or the stream ends.
                var total = 0;
                int read;
                while (total < buffer.Length && (read = this.stream.Read(buffer, total, buffer.Length - total)) > 0)
                    total += read;
                return ToChunk(buffer, total);
            }

            internal override async Task<byte[]> ReadNextAsync(int chunkSizeBytes, CancellationToken cancellationToken)
            {
                var buffer = this.GetBuffer(chunkSizeBytes);
                var total = 0;
                int read;
                while (total < buffer.Length &&
                       (read = await this.stream.ReadAsync(buffer, total, buffer.Length - total, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    total += read;
                }
                return ToChunk(buffer, total);
            }

            /// <summary>
            ///     The buffer is reused for every full chunk: a chunk has been sent, and its parameter is
            ///     finished with, before the next is read.
            /// </summary>
            private byte[] GetBuffer(int chunkSizeBytes)
                => this.buffer != null && this.buffer.Length == chunkSizeBytes ? this.buffer : (this.buffer = new byte[chunkSizeBytes]);

            /// <summary>A short final read is copied to an array of its own length, since the parameter sends the whole array.</summary>
            private static byte[] ToChunk(byte[] buffer, int read)
            {
                if (read == 0)
                    return null;
                if (read == buffer.Length)
                    return buffer;

                var chunk = new byte[read];
                Buffer.BlockCopy(buffer, 0, chunk, 0, read);
                return chunk;
            }
        }

        private sealed class TextReaderSource : ColumnWriteSource<string>
        {
            private readonly TextReader reader;
            private char[] buffer;

            public TextReaderSource(TextReader reader) => this.reader = reader;

            public override bool IsText => true;
            internal override long? TotalLength => null;
            internal override int LengthOf(string chunk) => chunk.Length;

            internal override string ReadNext(int chunkSizeBytes)
            {
                var buffer = this.GetBuffer(chunkSizeBytes);
                return ToChunk(buffer, this.reader.ReadBlock(buffer, 0, buffer.Length));
            }

            internal override async Task<string> ReadNextAsync(int chunkSizeBytes, CancellationToken cancellationToken)
            {
                // TextReader has no cancellable ReadBlockAsync on every target, so cancellation is
                // observed between chunks instead.
                cancellationToken.ThrowIfCancellationRequested();
                var buffer = this.GetBuffer(chunkSizeBytes);
                return ToChunk(buffer, await this.reader.ReadBlockAsync(buffer, 0, buffer.Length).ConfigureAwait(false));
            }

            private char[] GetBuffer(int chunkSizeBytes)
            {
                var length = CharsPerChunk(chunkSizeBytes);
                return this.buffer != null && this.buffer.Length == length ? this.buffer : (this.buffer = new char[length]);
            }

            private static string ToChunk(char[] buffer, int read) => read == 0 ? null : new string(buffer, 0, read);
        }
    }

    /// <summary>
    ///     A <see cref="ColumnWriteSource"/> whose chunks are <typeparamref name="TChunk"/> — <c>byte[]</c>
    ///     for a binary column, <c>string</c> for a text column.
    /// </summary>
    internal abstract class ColumnWriteSource<TChunk> : ColumnWriteSource
        where TChunk : class
    {
        /// <summary>The next chunk of at most <paramref name="chunkSizeBytes"/>, or <c>null</c> once the value is exhausted.</summary>
        internal abstract TChunk ReadNext(int chunkSizeBytes);

        /// <summary>
        ///     The asynchronous <see cref="ReadNext"/>. An in-memory source has nothing to wait for and
        ///     keeps this default.
        /// </summary>
        internal virtual Task<TChunk> ReadNextAsync(int chunkSizeBytes, CancellationToken cancellationToken)
            => Task.FromResult(this.ReadNext(chunkSizeBytes));

        /// <summary>The length of a chunk, in the unit of <see cref="ColumnWriteSource.TotalLength"/>.</summary>
        internal abstract int LengthOf(TChunk chunk);
    }
}
