using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;

namespace Atis.Orm
{
    public class ExecutionContext : IExecutionContext
    {
        public ExecutionContext(string sql, IReadOnlyList<DbParameter> dbParameters, bool isNonQuery, Func<IDataReader, object> elementFactory)
        {
            this.Sql = sql ?? throw new ArgumentNullException(nameof(sql));
            this.DbParameters = dbParameters;
            this.ElementFactory = elementFactory;
            this.IsNonQuery = isNonQuery;
        }

        public string Sql { get; }

        public IReadOnlyList<DbParameter> DbParameters { get; }

        public Func<IDataReader, object> ElementFactory { get; }

        public bool IsNonQuery { get; }
    }
}
