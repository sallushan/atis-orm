using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Atis.Orm.Abstractions
{
    public interface IExpressionCacheKeyProvider
    {
        object GetCacheKey(Expression expression);
    }
}
