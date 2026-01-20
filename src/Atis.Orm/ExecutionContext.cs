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
        public ExecutionContext(string sql, IReadOnlyList<DbParameter> dbParameters, Func<IDataReader, object> elementFactory)
        {
            Sql = sql;
            DbParameters = dbParameters;
            ElementFactory = elementFactory;
        }

        public string Sql { get; }

        public IReadOnlyList<DbParameter> DbParameters { get; }

        public Func<IDataReader, object> ElementFactory { get; }
    }
}
