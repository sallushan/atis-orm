using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using Atis.Orm.DataAccess;
namespace Atis.Orm.Querying
{
    public class DbAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private DbDataReader dataReader;
        private DbCommand dbCommand;
        private readonly string sql;
        private readonly IEnumerable<DbParameter> dbParameters;
        private readonly Func<IDataReader, object> elementFactory;
        private readonly CancellationToken cancellationToken;
        private bool disposed;
        private bool currentIsSet;
        private T current;
        // The connection is opened lazily on the first MoveNextAsync, so an enumerator that is created
        // and disposed without being enumerated never opened one. Closing regardless would release a
        // claim this enumerator never took, and drop the connection under whoever does hold it.
        private bool connectionOpened;
        private readonly IDbCommunication db;

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
            
            if (this.dataReader == null)
            {
                await this.db.OpenConnectionAsync(this.cancellationToken).ConfigureAwait(false);
                // Set only once the claim is actually taken: if OpenConnectionAsync throws there is
                // nothing to release.
                this.connectionOpened = true;
                var result = await this.db.ExecuteReaderAsync(sql, dbParameters, CommandType.Text, this.cancellationToken).ConfigureAwait(false);
                this.dataReader = result.DataReader;
                this.dbCommand = result.Command;
            }
            
            var hasData = await this.dataReader.ReadAsync(this.cancellationToken).ConfigureAwait(false);
            this.currentIsSet = false;
            return hasData;
        }

        public T Current
        {
            get
            {
                ThrowIfDisposed();

                if (currentIsSet) return current;
                current = (T)elementFactory(dataReader);
                currentIsSet = true;
                return current;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
                return;

            if (disposing)
            {
                if (this.dataReader != null)
                {
                    this.dataReader.Dispose();
                    this.dataReader = null;
                }

                if (this.dbCommand != null)
                {
                    this.dbCommand.Dispose();
                    this.dbCommand = null;
                }

                if (this.connectionOpened)
                {
                    this.connectionOpened = false;
                    this.db.CloseConnection();
                }
            }

            disposed = true;
        }

        ~DbAsyncEnumerator()
        {
            Dispose(false);
        }

        public async ValueTask DisposeAsync()
        {
            // Dispose(bool) guards on this flag but this method did not, so Dispose() followed by
            // DisposeAsync() released the connection twice for one claim.
            if (this.disposed)
                return;

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
            if (this.dataReader != null)
            {
                await this.dataReader.DisposeAsync().ConfigureAwait(false);
                this.dataReader = null;
            }

            if (this.dbCommand != null)
            {
                await this.dbCommand.DisposeAsync().ConfigureAwait(false);
                this.dbCommand = null;
            }

            if (this.connectionOpened)
            {
                this.connectionOpened = false;
                await this.db.CloseConnectionAsync().ConfigureAwait(false);
            }
            this.disposed = true;
            GC.SuppressFinalize(this);
#else
            Dispose();
#endif
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