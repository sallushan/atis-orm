using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Atis.Orm.DataAccess
{
    /// <summary>
    ///     The <see cref="IDbReaderSession"/> that <see cref="DbCommunicationBase.OpenReader"/> hands out.
    ///     Public so a provider overriding <c>OpenReader</c> has something to return; it takes the three
    ///     pieces already opened and is responsible for mapping rows and for giving all three up again.
    /// </summary>
    public sealed class DbReaderSession : IDbReaderSession
    {
        private readonly IDbCommunication db;
        private readonly Func<IDataReader, object> elementFactory;
        private readonly ConcurrencyDetector concurrencyDetector;
        private DbDataReader reader;
        private DbCommand command;
        private bool disposed;
        private bool currentIsSet;
        private object current;

        /// <summary>
        ///     Takes ownership of an open reader, the command behind it, and one claim on
        ///     <paramref name="db"/>'s connection -- the claim the caller took before running the command.
        ///     All three are released together when this session is disposed.
        /// </summary>
        /// <param name="reader">The open reader, positioned before the first row.</param>
        /// <param name="command">The command it came from, which has to outlive it.</param>
        /// <param name="db">The instance whose connection claim this session holds.</param>
        /// <param name="elementFactory">
        ///     Maps the row the reader is positioned on to the object <see cref="Current"/> returns.
        /// </param>
        public DbReaderSession(DbDataReader reader, DbCommand command, IDbCommunication db, Func<IDataReader, object> elementFactory)
            : this(reader, command, db, elementFactory, null)
        {
        }

        /// <inheritdoc cref="DbReaderSession(DbDataReader, DbCommand, IDbCommunication, Func{IDataReader, object})"/>
        /// <param name="reader">The open reader, positioned before the first row.</param>
        /// <param name="command">The command it came from, which has to outlive it.</param>
        /// <param name="db">The instance whose connection claim this session holds.</param>
        /// <param name="elementFactory">
        ///     Maps the row the reader is positioned on to the object <see cref="Current"/> returns.
        /// </param>
        /// <param name="concurrencyDetector">
        ///     <paramref name="db"/>'s own detector, so that reading a row here and running a command there
        ///     are seen as what they are -- two operations on one connection -- and a second flow touching
        ///     either is reported rather than left to corrupt the reader. <c>null</c> leaves the session
        ///     unchecked, which is what a caller constructing one by hand gets unless it passes one.
        /// </param>
        public DbReaderSession(DbDataReader reader, DbCommand command, IDbCommunication db, Func<IDataReader, object> elementFactory, ConcurrencyDetector concurrencyDetector)
        {
            this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
            this.command = command ?? throw new ArgumentNullException(nameof(command));
            this.db = db ?? throw new ArgumentNullException(nameof(db));
            this.elementFactory = elementFactory ?? throw new ArgumentNullException(nameof(elementFactory));
            this.concurrencyDetector = concurrencyDetector;
        }

        /// <summary>
        ///     Marks the start of one operation on the connection this session is reading over. Cheap
        ///     enough to sit on the per-row path, and <c>default</c> -- guarding nothing -- when the session
        ///     was built without a detector.
        /// </summary>
        private ConcurrencyDetectorCriticalSection EnterCriticalSection()
        {
            return this.concurrencyDetector is null
                    ? default
                    : this.concurrencyDetector.EnterCriticalSection();
        }

        /// <inheritdoc/>
        public bool Read()
        {
            using (this.EnterCriticalSection())
            {
                this.ThrowIfDisposed();
                var hasRow = this.reader.Read();
                // Whatever was built for the previous row is stale now, whether or not there is a new one.
                this.currentIsSet = false;
                this.current = null;
                return hasRow;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> ReadAsync(CancellationToken cancellationToken)
        {
            using (this.EnterCriticalSection())
            {
                this.ThrowIfDisposed();
                var hasRow = await this.reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                this.currentIsSet = false;
                this.current = null;
                return hasRow;
            }
        }

        /// <inheritdoc/>
        public object Current
        {
            get
            {
                // Guarded as well as Read, and not only for symmetry: this is where the row is actually
                // pulled off the reader, so it is a second window in which another flow can arrive.
                using (this.EnterCriticalSection())
                {
                    this.ThrowIfDisposed();

                    if (!this.currentIsSet)
                    {
                        // The reader goes to the factory and no further: it is positioned on this row for
                        // the duration of the call and is not reachable from anywhere else on this class.
                        this.current = this.elementFactory(this.reader);
                        this.currentIsSet = true;
                    }
                    return this.current;
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (this.disposed)
                return;
            // Set first, so a re-entrant or second call cannot release the connection claim twice.
            this.disposed = true;

            try
            {
                this.DisposeReaderAndCommand();
            }
            finally
            {
                // The claim goes back whatever happened above: a reader that throws on its way out would
                // otherwise hold the connection open for as long as the instance lives.
                this.db.CloseConnection();
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (this.disposed)
                return;
            this.disposed = true;

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
            try
            {
                // Reader before command: the command it came from has to outlive it. Each field is
                // cleared before its await so nothing can be disposed twice.
                if (this.reader != null)
                {
                    var dataReader = this.reader;
                    this.reader = null;
                    await dataReader.DisposeAsync().ConfigureAwait(false);
                }

                if (this.command != null)
                {
                    var dbCommand = this.command;
                    this.command = null;
                    await dbCommand.DisposeAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                await this.db.CloseConnectionAsync().ConfigureAwait(false);
            }
#else
            // No DisposeAsync on DbDataReader or DbCommand before netstandard2.1, so the whole disposal
            // is synchronous here.
            try
            {
                this.DisposeReaderAndCommand();
            }
            finally
            {
                this.db.CloseConnection();
            }
#endif
        }

        private void DisposeReaderAndCommand()
        {
            // Reader before command: the command it came from has to outlive it.
            if (this.reader != null)
            {
                var dataReader = this.reader;
                this.reader = null;
                dataReader.Dispose();
            }

            if (this.command != null)
            {
                var dbCommand = this.command;
                this.command = null;
                dbCommand.Dispose();
            }
        }

        private void ThrowIfDisposed()
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(DbReaderSession));
        }
    }
}
