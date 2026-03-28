using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace Atis.Orm
{
    public class DbEnumerable<T> : IEnumerable<T>
    {
        private readonly IDbCommunication db;
        private readonly string sql;
        private readonly IEnumerable<DbParameter> dbParameters;
        private readonly Func<IDataReader, object> elementFactory;

        public DbEnumerable(string sql, IEnumerable<DbParameter> dbParameters, Func<IDataReader, object> elementFactory, IDbCommunication db)
        {
            this.sql = sql ?? throw new ArgumentNullException(nameof(sql));
            this.dbParameters = dbParameters ?? throw new ArgumentNullException(nameof(dbParameters));
            this.elementFactory = elementFactory ?? throw new ArgumentNullException(nameof(elementFactory));
            this.db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new DbEnumerator<T>(this.sql, this.dbParameters, this.elementFactory, this.db);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}