using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
// ReSharper disable InvertIf

namespace Atis.Orm
{
    public class DbEnumerator<T> : IEnumerator<T>
    {
        private IDataReader dataReader;
        private DbCommand command;
        private readonly Func<IDataReader, object> elementFactory;
        private readonly string sql;
        private readonly IEnumerable<DbParameter> dbParameters;
        private bool disposed;
        private bool currentIsSet;
        private T current;
        private bool closeConnection;

        private readonly IDbCommunication db;
        //private readonly ConnectionInfo connectionInfo;

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
            
            if (this.dataReader == null)
            {
                this.db.OpenConnection();
                //this.dataReader = this.db.ExecuteReader(this.command, CommandBehavior.SequentialAccess);
                this.command = this.db.CreateCommand(this.sql, this.dbParameters, CommandType.Text);
                this.dataReader = this.command.ExecuteReader(CommandBehavior.SequentialAccess);
            }
            
            var hasData = this.dataReader.Read();
            this.currentIsSet = false;
            return hasData;
        }

        public void Reset()
        {
            throw new NotSupportedException("Reset is not supported on DbDataReaderEnumerator");
        }

        public T Current
        {
            get
            {
                ThrowIfDisposed();
                
                if (!currentIsSet)
                {
                    current = (T)elementFactory(dataReader);
                    currentIsSet = true;
                }
                return current;
            }
        }

        object IEnumerator.Current => Current;

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

                this.command?.Dispose();

                this.db.CloseConnection();
            }

            disposed = true;
        }

        ~DbEnumerator()
        {
            Dispose(false);
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