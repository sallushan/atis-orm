using Atis.Expressions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    public interface ILinqToSqlConverterFactoryProvider
    {
        IReadOnlyList<IExpressionConverterFactory<Expression, SqlExpression>> GetFactories();
    }
}