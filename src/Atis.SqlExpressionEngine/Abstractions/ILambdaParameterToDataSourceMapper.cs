using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.Abstractions
{
    public interface ILambdaParameterToDataSourceMapper
    {
        SqlExpression GetDataSourceByParameterExpression(ParameterExpression parameterExpression);
        void RemoveParameterMap(ParameterExpression parameterExpression);
        bool TrySetParameterMap(ParameterExpression parameterExpression, Func<SqlExpression> sqlExpressionExtractor);

        // we had GetQueryByParameterName, but it was not used and it was not efficient, so we removed it.
    }
}