using Atis.SqlExpressionEngine;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Atis.Orm
{
    /// <inheritdoc />
    public class OrmModel : IOrmModel
    {
        private readonly ConcurrentDictionary<Type, EntityMetadata> metadataMap = new ConcurrentDictionary<Type, EntityMetadata>();

        /// <inheritdoc />
        public void Add(EntityMetadata metadata)
        {
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));
            this.metadataMap[metadata.ClrType] = metadata;
        }

        /// <inheritdoc />
        public bool Contains(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            return this.metadataMap.ContainsKey(type);
        }

        /// <inheritdoc/>
        public EntityMetadata GetEntity(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            if (this.TryGet(type, out var metadata))
            {
                return metadata;
            }
            return null;
        }

        /// <inheritdoc />
        public EntityMetadata GetOrAdd(Type type, Func<Type, EntityMetadata> factory)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            return this.metadataMap.GetOrAdd(type, factory);
        }

        /// <inheritdoc />
        public bool TryGet(Type type, out EntityMetadata metadata)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            return this.metadataMap.TryGetValue(type, out metadata);
        }
    }
}
