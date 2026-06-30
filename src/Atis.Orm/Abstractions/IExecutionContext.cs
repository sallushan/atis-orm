using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace Atis.Orm.Abstractions
{
    public interface IExecutionContext
    {
        string Sql { get; }
        IReadOnlyList<DbParameter> DbParameters { get; }
        Func<IDataReader, object> ElementFactory { get; }
        bool IsNonQuery { get; }
    }
}