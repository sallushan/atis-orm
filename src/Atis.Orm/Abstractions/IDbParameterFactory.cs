using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Net;

namespace Atis.Orm.Abstractions
{
    public interface IDbParameterFactory
    {
        DbParameter CreateDbParameter(int parameterIndex, IQueryParameter queryParameter, object parameterValue);
        IReadOnlyList<DbParameter> CreateDbParameters(int parameterIndex, IQueryParameter queryParameter, IEnumerable parameterValue);
    }
}