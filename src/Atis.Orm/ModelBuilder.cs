using Atis.SqlExpressionEngine;
using System;
using System.Collections.Generic;

namespace Atis.Orm
{
    public class ModelBuilder
    {
        private readonly IEntityMetadataBuilder _entityMetadataBuilder;
        private readonly IOrmModel _ormModel;
        private readonly Dictionary<Type, MutableEntityMetadata> _mutableEntityMetadata = new Dictionary<Type, MutableEntityMetadata>();

        public bool AllClrProperties { get; set; } = false;

        public ModelBuilder(IEntityMetadataBuilder entityMetadataBuilder, IOrmModel ormModel)
        {
            _entityMetadataBuilder = entityMetadataBuilder ?? throw new ArgumentNullException(nameof(entityMetadataBuilder));
            _ormModel = ormModel ?? throw new ArgumentNullException(nameof(ormModel));
        }

        public EntityBuilder<T> Entity<T>()
        {
            if (!this._mutableEntityMetadata.TryGetValue(typeof(T), out var existing))
            {
                var seeded = _entityMetadataBuilder.Build(typeof(T));
                var mutable = new MutableEntityMetadata(seeded);
                _mutableEntityMetadata.Add(typeof(T), mutable);
                existing = mutable;
            }
            var builder = new EntityBuilder<T>(existing);
            return builder;
        }

        public EntityBuilder<T> Entity<T>(Action<EntityBuilder<T>> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var builder = Entity<T>();
            configure(builder);
            return builder;
        }

        internal void Build()
        {
            foreach (var mutableMetadata in _mutableEntityMetadata.Values)
            {
                var metadata = mutableMetadata.Build();
                _ormModel.Add(metadata);
            }
        }
    }
}