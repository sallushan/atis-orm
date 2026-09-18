using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Atis.Orm.DataAccess
{
    /// <summary>
    ///     <para>
    ///         One binary column being read straight off the database, and everything holding it open. The
    ///         bytes are pulled across the connection as they are asked for, so a column of any size costs
    ///         the size of the caller's buffer rather than the size of the value.
    ///     </para>
    ///     <para>
    ///         <strong>It owns the reader session, so disposing it is not optional.</strong> Until it is
    ///         disposed the reader, the command and a claim on the connection are all still held. This is
    ///         the whole difference from every other result the ORM returns: an ordinary row is a finished
    ///         object and the connection is already back, whereas this is a live window onto the row it was
    ///         read from.
    ///     </para>
    ///     <para>
    ///         Forward-only. The underlying reader is opened with
    ///         <see cref="System.Data.CommandBehavior.SequentialAccess"/> -- which is what makes it stream
    ///         rather than buffer -- and that gives no way back to bytes already read, so
    ///         <see cref="CanSeek"/> is <c>false</c> and <see cref="Position"/> reports how far it has got
    ///         rather than being somewhere to move to.
    ///     </para>
    ///     <para>
    ///         <see cref="Length"/> answers only when the caller named a column holding the value's size,
    ///         because the database does not otherwise say how long a streamed value is until it ends. It
    ///         is therefore the caller's number, reported back unchecked -- a size column that disagrees
    ///         with its blob produces a wrong <see cref="Length"/> and correct bytes.
    ///     </para>
    /// </summary>
    public sealed class DbFieldStream : Stream
    {
        private readonly IDbReaderSession session;
        private readonly Stream inner;
        private readonly IProgress<long> progress;
        private readonly long? length;
        private long position;
        private bool disposed;

        internal DbFieldStream(IDbReaderSession session, Stream inner, long? length, IProgress<long> progress)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.length = length;
            this.progress = progress;
        }

        /// <inheritdoc/>
        public override bool CanRead => !this.disposed;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override bool CanWrite => false;

        /// <summary>
        ///     The value's size in bytes, when the terminal was given a column to read it from.
        /// </summary>
        /// <exception cref="NotSupportedException">No size column was named.</exception>
        public override long Length
            => this.length
               ?? throw new NotSupportedException(
                   "The length of this column is not known. Pass the column that holds the value's size to " +
                   "StreamField if the caller needs it -- the database does not report the size of a value " +
                   "it is streaming.");

        /// <summary>Bytes read so far. Assigning to it is not supported; this stream cannot seek.</summary>
        public override long Position
        {
            get => this.position;
            set => throw new NotSupportedException("A column stream is forward-only and cannot be repositioned.");
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            this.ThrowIfDisposed();
            // The section covers the read and nothing else, exactly as it does for a row: it is one
            // operation on the connection, and holding it for the life of the stream would reject the
            // commands the caller is entitled to run between reads.
            using (this.session.EnterCriticalSection())
            {
                var read = this.inner.Read(buffer, offset, count);
                this.Advance(read);
                return read;
            }
        }

        /// <inheritdoc/>
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            this.ThrowIfDisposed();
            using (this.session.EnterCriticalSection())
            {
                var read = await this.inner.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
                this.Advance(read);
                return read;
            }
        }

        /// <summary>Nothing is buffered on the way out of a read-only stream, so this does nothing.</summary>
        public override void Flush()
        {
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException("A column stream is forward-only and cannot seek.");

        /// <inheritdoc/>
        public override void SetLength(long value)
            => throw new NotSupportedException("A column stream is read-only.");

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException("A column stream is read-only.");

        /// <summary>
        ///     Counts what was read and reports it. Progress is the running total rather than the size of
        ///     the last read, so a caller handing this stream to something that copies it -- which is the
        ///     reason the progress hook exists at all -- gets a number it can show without keeping its own.
        /// </summary>
        private void Advance(int read)
        {
            if (read <= 0)
                return;
            this.position += read;
            this.progress?.Report(this.position);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (this.disposed)
                return;
            // Set first, so a second Dispose cannot release the session twice.
            this.disposed = true;

            if (disposing)
            {
                try
                {
                    this.inner.Dispose();
                }
                finally
                {
                    // The session goes whatever the column stream did on its way out; it is what holds the
                    // connection, and a stream that throws while closing must not keep it.
                    this.session.Dispose();
                }
            }

            base.Dispose(disposing);
        }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
        /// <inheritdoc/>
        public override async ValueTask DisposeAsync()
        {
            if (this.disposed)
                return;
            this.disposed = true;

            try
            {
                await this.inner.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                await this.session.DisposeAsync().ConfigureAwait(false);
            }
        }
#endif

        private void ThrowIfDisposed()
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(DbFieldStream));
        }
    }
}
