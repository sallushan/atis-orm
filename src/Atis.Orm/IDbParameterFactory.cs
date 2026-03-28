using System.Data;
using System.Data.Common;
using System.Net;

namespace Atis.Orm
{
    public interface IDbParameterFactory
    {
        DbParameter CreateDbParameter(IQueryParameter queryParameter, object parameterValue);
    }
}