using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

using Atis.Orm.DataAccess;
namespace Atis.Orm.Querying
{
    public class DbAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private readonly string sql;
        private readonly IEnumerable<DbParameter> dbParameters;
        private readonly Func<IDataReader, object> elementFactory;
        private readonly CancellationToken cancellationToken;
        private readonly IDbCommunication db;
        // The one thing this enumerator owns. Opened lazily on the first MoveNextAsync, so null means
        // nothing was ever opened and there is correspondingly nothing to release -- an enumerator created
        // and disposed without being enumerated must not give up a connection claim it never took.
        private IDbReaderSession session;
        private bool disposed;

        public DbAsyncEnumerator(
            string sql, IEnumerable<DbParameter> dbParameters, Func<IDataReader, object> elementFactory,
            IDbCommunication db,
            CancellationToken cancellationToken = default)
        {
            if (elementFactory is null)
                throw new ArgumentNullException(nameof(elementFactory));

            this.db = db ?? throw new ArgumentNullException(nameof(db));
            this.sql = sql;
            this.dbParameters = dbParameters;
            this.elementFactory = elementFactory;
            this.cancellationToken = cancellationToken;
        }

        public async ValueTask<bool> MoveNextAsync()
        {
            ThrowIfDisposed();

            if (this.session == null)
            {
                this.session = await this.db.OpenReaderAsync(sql, dbParameters, CommandType.Text, this.elementFactory, this.cancellationToken).ConfigureAwait(false);
            }

            return await this.session.ReadAsync(this.cancellationToken).ConfigureAwait(false);
        }

        // The session builds the row and remembers it, so this is only the cast onto T.
        public T Current
        {
            get
            {
                ThrowIfDisposed();
                return (T)this.session.Current;
            }
        }

        // No finalizer: the session holds the only unmanaged resources here and releases them itself.
        public void Dispose()
        {
            if (this.disposed)
                return;
            // Set first, so Dispose followed by DisposeAsync -- or either one twice -- cannot release the
            // session, and with it the connection claim, more than once.
            this.disposed = true;

            var readerSession = this.session;
            this.session = null;
            readerSession?.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (this.disposed)
                return;
            this.disposed = true;

            var readerSession = this.session;
            this.session = null;
            if (readerSession != null)
            {
                await readerSession.DisposeAsync().ConfigureAwait(false);
            }
        }

        protected void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}
