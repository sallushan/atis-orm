using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Atis.Orm
{
    public class ExpressionCacheKeyProvider : IExpressionCacheKeyProvider
    {
        public object GetCacheKey(Expression expression)
        {
            return ExpressionEqualityComparer.Instance.GetHashCode(expression);
        }
    }
}
