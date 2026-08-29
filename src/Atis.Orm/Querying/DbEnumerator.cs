using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
// ReSharper disable InvertIf

using Atis.Orm.DataAccess;
namespace Atis.Orm.Querying
{
    public class DbEnumerator<T> : IEnumerator<T>
    {
        private readonly Func<IDataReader, object> elementFactory;
        private readonly string sql;
        private readonly IEnumerable<DbParameter> dbParameters;
        private readonly IDbCommunication db;
        // The one thing this enumerator owns. Opened lazily on the first MoveNext, so null means nothing
        // was ever opened and there is correspondingly nothing to release -- an enumerator created and
        // disposed without being enumerated must not give up a connection claim it never took.
        private IDbReaderSession session;
        private bool disposed;

        public DbEnumerator(string sql, IEnumerable<DbParameter> dbParameters, Func<IDataReader, object> elementFactory, IDbCommunication db)
        {
            this.db = db ?? throw new ArgumentNullException(nameof(db));
            this.elementFactory = elementFactory ?? throw new ArgumentNullException(nameof(elementFactory));
            this.sql = sql;
            this.dbParameters = dbParameters;
        }

        public bool MoveNext()
        {
            ThrowIfDisposed();

            if (this.session == null)
            {
                this.session = this.db.OpenReader(this.sql, this.dbParameters, CommandType.Text, this.elementFactory);
            }

            return this.session.Read();
        }

        public void Reset()
        {
            throw new NotSupportedException("Reset is not supported on DbDataReaderEnumerator");
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

        object IEnumerator.Current => Current;

        // No finalizer: the session holds the only unmanaged resources here and releases them itself.
        public void Dispose()
        {
            if (this.disposed)
                return;
            // Set first, so a re-entrant or second call cannot release the session twice.
            this.disposed = true;

            var readerSession = this.session;
            this.session = null;
            readerSession?.Dispose();
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
