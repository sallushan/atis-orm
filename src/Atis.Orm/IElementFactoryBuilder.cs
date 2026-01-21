using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Data;
using System.Linq.Expressions;

namespace Atis.Orm
{
    public interface IElementFactoryBuilder
    {
        Func<IDataReader, object> CreateElementFactory(Expression expression, SqlExpression sqlExpression);
    }
}