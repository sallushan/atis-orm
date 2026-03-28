using Atis.SqlExpressionEngine.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Atis.Orm
{
    public interface IOrmReflectionService : IReflectionService
    {
        bool IsAsyncEnumerableType(Type type);
        Type GetAsyncElementType(Type type);
    }
}
