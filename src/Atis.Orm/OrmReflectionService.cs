using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Atis.Orm
{
    public class OrmReflectionService : ReflectionService, IOrmReflectionService
    {
        public OrmReflectionService(IExpressionEvaluator expressionEvaluator)
        : base(expressionEvaluator) { }

        public bool IsAsyncEnumerableType(Type type)
        {
            if (type == typeof(string)) return false;
            return type.GetInterfaces()
                       .Append(type)
                       .Any(x => x.IsGenericType &&
                                 x.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));
        }

        public Type GetAsyncElementType(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
                return type.GetGenericArguments()[0];
            return type.GetInterfaces()
                       .Where(x => x.IsGenericType &&
                                   x.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
                       .Select(x => x.GetGenericArguments()[0])
                       .FirstOrDefault();
        }
    }
}
