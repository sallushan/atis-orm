using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

namespace Atis.Orm.Abstractions
{
    public interface IQueryParameter
    {
        string Name { get; }
        object InitialValue { get; }
        bool IsLiteral { get; }
        SqlExpression SqlParameterExpression { get; }
    }
}
