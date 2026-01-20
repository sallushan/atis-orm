using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Atis.Orm
{
    public class DbAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private DbDataReader dataReader;
        private readonly DbCommand command;
        private readonly Func<IDataReader, object> elementFactory;
        private readonly CancellationToken cancellationToken;
        private bool disposed;
        private bool currentIsSet;
        private T current;
        private bool closeConnection;
        private readonly IDbCommunication db;

        public DbAsyncEnumerator(
            string sql, IEnumerable<DbParameter> dbParameters, Func<IDataReader, object> elementFactory,
            IDbCommunication db,
            CancellationToken cancellationToken = default)
        {
            if (elementFactory is null)
                throw new ArgumentNullException(nameof(elementFactory));

            this.db = db ?? throw new ArgumentNullException(nameof(db));
            this.elementFactory = elementFactory;
            this.cancellationToken = cancellationToken;
            this.command = db.CreateCommand(sql, dbParameters, CommandType.Text);
        }

        public async ValueTask<bool> MoveNextAsync()
        {
            ThrowIfDisposed();
            
            if (this.dataReader == null)
            {
                await this.db.OpenConnectionAsync(this.cancellationToken);
                this.dataReader = await this.db.ExecuteReaderAsync(this.command, CommandBehavior.SequentialAccess, this.cancellationToken);
            }
            
            var hasData = await this.dataReader.ReadAsync(this.cancellationToken);
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

                this.command.Dispose();

                this.db.CloseConnection();
            }

            disposed = true;
        }

        ~DbAsyncEnumerator()
        {
            Dispose(false);
        }

        public async ValueTask DisposeAsync()
        {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
            if (this.dataReader != null)
            {
                await this.dataReader.DisposeAsync();
                this.dataReader = null;
            }

            await this.command.DisposeAsync();

            await this.db.CloseConnectionAsync();
            this.disposed = true;
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