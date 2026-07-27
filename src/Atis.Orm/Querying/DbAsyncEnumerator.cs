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
                await this.db.OpenConnectionAsync(this.cancellationToken);
                var result = await this.db.ExecuteReaderAsync(sql, dbParameters, CommandType.Text, this.cancellationToken);
                this.dataReader = result.DataReader;
                this.dbCommand = result.Command;
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

                if (this.dbCommand != null)
                {
                    this.dbCommand.Dispose();
                    this.dbCommand = null;
                }

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

            if (this.dbCommand != null)
            {
                await this.dbCommand.DisposeAsync();
                this.dbCommand = null;
            }

            await this.db.CloseConnectionAsync();
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