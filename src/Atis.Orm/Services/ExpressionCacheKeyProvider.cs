using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

using Atis.Orm.Abstractions;
namespace Atis.Orm.Services
{
    public class ExpressionCacheKeyProvider : IExpressionCacheKeyProvider
    {
        public object GetCacheKey(Expression expression)
        {
            return ExpressionEqualityComparer.Instance.GetHashCode(expression);
        }
    }
}
