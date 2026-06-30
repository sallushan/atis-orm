using System.Data;
using System.Data.Common;
using System.Net;

namespace Atis.Orm.Abstractions
{
    public interface IDbParameterFactory
    {
        DbParameter CreateDbParameter(IQueryParameter queryParameter, object parameterValue);
    }
}