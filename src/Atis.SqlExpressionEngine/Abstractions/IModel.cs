using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Atis.SqlExpressionEngine.Abstractions
{
    public interface IModel
    {
        EntityMetadata GetEntity(Type type);
    }
}