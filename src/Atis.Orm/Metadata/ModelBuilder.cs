using Atis.SqlExpressionEngine;
using System;
using System.Collections.Generic;

using Atis.Orm.Abstractions;
namespace Atis.Orm.Metadata
{
    public class ModelBuilder
    {
        private readonly IEntityMetadataBuilder _entityMetadataBuilder;
        private readonly IEntityCrudMetadataFactory _entityCrudMetadataFactory;
        private readonly IOrmModel _ormModel;
        private readonly Dictionary<Type, MutableEntityMetadata> _mutableEntityMetadata = new Dictionary<Type, MutableEntityMetadata>();

        public bool AllClrProperties { get; set; } = false;

        public ModelBuilder(IEntityMetadataBuilder entityMetadataBuilder, IOrmModel ormModel)
            : this(entityMetadataBuilder, crudMetadataFactory: null, ormModel: ormModel)
        {
        }

        /// <param name="crudMetadataFactory">
        ///     Seeds the persistence side of each mapping from annotations, so that a fluent call
        ///     overrides an annotation rather than the other way round. When <c>null</c>, every column
        ///     starts out as <see cref="ColumnKind.Regular"/> and only fluent configuration applies.
        /// </param>
        public ModelBuilder(IEntityMetadataBuilder entityMetadataBuilder, IEntityCrudMetadataFactory crudMetadataFactory, IOrmModel ormModel)
        {
            _entityMetadataBuilder = entityMetadataBuilder ?? throw new ArgumentNullException(nameof(entityMetadataBuilder));
            _entityCrudMetadataFactory = crudMetadataFactory;
            _ormModel = ormModel ?? throw new ArgumentNullException(nameof(ormModel));
        }

        public EntityBuilder<T> Entity<T>()
        {
            if (!this._mutableEntityMetadata.TryGetValue(typeof(T), out var existing))
            {
                var seeded = _entityMetadataBuilder.Build(typeof(T));
                var crudSeeded = _entityCrudMetadataFactory?.Build(typeof(T));
                var mutable = new MutableEntityMetadata(seeded, crudSeeded);
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
                _ormModel.Add(mutableMetadata.Build());
                _ormModel.AddCrud(mutableMetadata.BuildCrud());
            }
        }
    }
}