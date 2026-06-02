using System;
using System.Collections.Generic;
using System.Text;

namespace Atis.Expressions
{
    public interface IExpressionConverterDependencyProvider
    {
        object GetDependencyRequired(Type type);
        T GetDependencyRequired<T>();
    }
}
