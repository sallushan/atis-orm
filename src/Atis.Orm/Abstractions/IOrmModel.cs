using Atis.SqlExpressionEngine;
using Atis.SqlExpressionEngine.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Atis.Orm.Abstractions
{
    public interface IOrmModel : IModel
    {
        EntityMetadata GetOrAdd(Type type, Func<Type, EntityMetadata> factory);
        void Add(EntityMetadata metadata);
        bool Contains(Type type);
        bool TryGet(Type type, out EntityMetadata metadata);
        void EnsureModelInitialized(Action modelInitializer);
    }
}
