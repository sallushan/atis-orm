using System;
using System.IO;
using System.Threading.Tasks;

namespace Atis.Orm.DataAccess
{
    /// <summary>
    ///     <para>
    ///         One character column being read straight off the database, and everything holding it open.
    ///         The text counterpart of <see cref="DbFieldStream"/>, and everything said there applies here:
    ///         it owns the reader session and must be disposed, it is forward-only, and the characters
    ///         cross the connection as they are asked for rather than all at once.
    ///     </para>
    ///     <para>
    ///         <see cref="ReadToEnd"/> is available and is sometimes the right thing, but note that it
    ///         gives up what this class is for -- it builds one string as large as the column. A caller
    ///         that only wants the whole value as a string is better served by selecting the column
    ///         normally.
    ///     </para>
    /// </summary>
    public sealed class DbFieldTextReader : TextReader
    {
        private readonly IDbReaderSession session;
        private readonly TextReader inner;
        private readonly IProgress<long> progress;
        private readonly long? length;
        private long charactersRead;
        private bool disposed;

        internal DbFieldTextReader(IDbReaderSession session, TextReader inner, long? length, IProgress<long> progress)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.length = length;
            this.progress = progress;
        }

        /// <summary>
        ///     The value's length, when the terminal was given a column to read it from, and <c>null</c>
        ///     otherwise. Whatever the named column holds is reported unchecked, so a column counting bytes
        ///     rather than characters is reported as-is.
        /// </summary>
        public long? Length => this.length;

        /// <summary>Characters handed back so far, which is what progress reports.</summary>
        /// <remarks>
        ///     <see cref="ReadLine"/> does not include the line terminator in what it returns, so a caller
        ///     reading by line sees this fall slightly behind the characters actually consumed.
        /// </remarks>
        public long CharactersRead => this.charactersRead;

        /// <inheritdoc/>
        public override int Peek()
        {
            this.ThrowIfDisposed();
            using (this.session.EnterCriticalSection())
                return this.inner.Peek();
        }

        /// <inheritdoc/>
        public override int Read()
        {
            this.ThrowIfDisposed();
            using (this.session.EnterCriticalSection())
            {
                var value = this.inner.Read();
                this.Advance(value < 0 ? 0 : 1);
                return value;
            }
        }

        /// <inheritdoc/>
        public override int Read(char[] buffer, int index, int count)
        {
            this.ThrowIfDisposed();
            using (this.session.EnterCriticalSection())
            {
                var read = this.inner.Read(buffer, index, count);
                this.Advance(read);
                return read;
            }
        }

        /// <inheritdoc/>
        public override async Task<int> ReadAsync(char[] buffer, int index, int count)
        {
            this.ThrowIfDisposed();
            using (this.session.EnterCriticalSection())
            {
                var read = await this.inner.ReadAsync(buffer, index, count).ConfigureAwait(false);
                this.Advance(read);
                return read;
            }
        }

        /// <inheritdoc/>
        public override int ReadBlock(char[] buffer, int index, int count)
        {
            this.ThrowIfDisposed();
            using (this.session.EnterCriticalSection())
            {
                var read = this.inner.ReadBlock(buffer, index, count);
                this.Advance(read);
                return read;
            }
        }

        /// <inheritdoc/>
        public override async Task<int> ReadBlockAsync(char[] buffer, int index, int count)
        {
            this.ThrowIfDisposed();
            using (this.session.EnterCriticalSection())
            {
                var read = await this.inner.ReadBlockAsync(buffer, index, count).ConfigureAwait(false);
                this.Advance(read);
                return read;
            }
        }

        // ReadLine and ReadToEnd are delegated rather than left to the base class, which would drive them
        // one character at a time through Read() -- a critical section per character.

        /// <inheritdoc/>
        public override string ReadLine()
        {
            this.ThrowIfDisposed();
            using (this.session.EnterCriticalSection())
                return this.Advance(this.inner.ReadLine());
        }

        /// <inheritdoc/>
        public override async Task<string> ReadLineAsync()
        {
            this.ThrowIfDisposed();
            using (this.session.EnterCriticalSection())
                return this.Advance(await this.inner.ReadLineAsync().ConfigureAwait(false));
        }

        /// <inheritdoc/>
        public override string ReadToEnd()
        {
            this.ThrowIfDisposed();
            using (this.session.EnterCriticalSection())
                return this.Advance(this.inner.ReadToEnd());
        }

        /// <inheritdoc/>
        public override async Task<string> ReadToEndAsync()
        {
            this.ThrowIfDisposed();
            using (this.session.EnterCriticalSection())
                return this.Advance(await this.inner.ReadToEndAsync().ConfigureAwait(false));
        }

        private void Advance(int read)
        {
            if (read <= 0)
                return;
            this.charactersRead += read;
            this.progress?.Report(this.charactersRead);
        }

        private string Advance(string read)
        {
            if (read != null)
                this.Advance(read.Length);
            return read;
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
                    this.session.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        private void ThrowIfDisposed()
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(DbFieldTextReader));
        }
    }
}
