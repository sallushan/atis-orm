using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;

namespace Atis.Orm
{
    public class DbAsyncEnumerable<T> : IAsyncEnumerable<T>
    {
        private readonly IDbCommunication db;
        private readonly string sql;
        private readonly IEnumerable<DbParameter> dbParameters;
        private readonly Func<IDataReader, object> elementFactory;

        public DbAsyncEnumerable(string sql, IEnumerable<DbParameter> dbParameters, Func<IDataReader, object> elementFactory, IDbCommunication db)
        {
            this.sql = sql ?? throw new ArgumentNullException(nameof(sql));
            this.dbParameters = dbParameters;
            this.elementFactory = elementFactory ?? throw new ArgumentNullException(nameof(elementFactory));
            this.db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new DbAsyncEnumerator<T>(this.sql, this.dbParameters, this.elementFactory, this.db, cancellationToken);
        }
    }
}