using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Atis.Orm
{
    public interface IExpressionVariableValuesExtractor
    {
        IReadOnlyList<object> ExtractVariableValues(Expression sqlExpression);
    }
}
