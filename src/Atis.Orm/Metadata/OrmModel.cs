using Atis.SqlExpressionEngine;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using Atis.Orm.Abstractions;
namespace Atis.Orm.Metadata
{
    /// <inheritdoc />
    public class OrmModel : IOrmModel
    {
        private readonly ConcurrentDictionary<Type, EntityMetadata> metadataMap = new ConcurrentDictionary<Type, EntityMetadata>();
        private readonly ConcurrentDictionary<Type, EntityCrudMetadata> crudMetadataMap = new ConcurrentDictionary<Type, EntityCrudMetadata>();
        private readonly IEntityMetadataBuilder entityMetadataBuilder;
        private volatile bool _modelCreated = false;
        private readonly object _modelCreatedLock = new object();

        /// <summary>
        ///     Constructs the model.
        /// </summary>
        /// <param name="entityMetadataBuilder">
        ///     Derives a mapping from annotations for an entity that <c>OnModelCreating</c> never
        ///     configured, which is what <see cref="EnsureEntityMapped"/> falls back to.
        /// </param>
        public OrmModel(IEntityMetadataBuilder entityMetadataBuilder)
        {
            this.entityMetadataBuilder = entityMetadataBuilder ?? throw new ArgumentNullException(nameof(entityMetadataBuilder));
        }

        /// <inheritdoc />
        public EntityMetadata EnsureEntityMapped(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            return this.metadataMap.GetOrAdd(type, this.entityMetadataBuilder.Build);
        }

        /// <inheritdoc />
        public void Add(EntityMetadata metadata)
        {
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));
            this.metadataMap[metadata.ClrType] = metadata;
        }

        /// <summary>
        ///     <para>
        ///         Runs <paramref name="modelInitializer"/> exactly once for the lifetime of this model,
        ///         however many threads arrive together, and returns only once it has finished.
        ///     </para>
        ///     <para>
        ///         Deliberately not on <see cref="IOrmModel"/>. Initialization is the concern of whoever
        ///         owns the model's construction — today only <see cref="OrmModelSource"/>, which holds
        ///         this concrete type. Everyone else receives a model that is already initialized and has
        ///         no business re-initializing it.
        ///     </para>
        /// </summary>
        public void EnsureModelInitialized(Action modelInitializer)
        {
            // this is double-check locking pattern and intentional,
            // first check: fast path, avoids acquiring the lock
            // second check: to block two threads simultaneously going through same code
            if (!_modelCreated)
            {
                lock (_modelCreatedLock)
                {
                    if (!_modelCreated)
                    {
                        modelInitializer();
                        _modelCreated = true;
                    }
                }
            }
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
        public bool TryGet(Type type, out EntityMetadata metadata)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            return this.metadataMap.TryGetValue(type, out metadata);
        }

        /// <inheritdoc />
        public void AddCrud(EntityCrudMetadata crudMetadata)
        {
            if (crudMetadata == null) throw new ArgumentNullException(nameof(crudMetadata));
            this.crudMetadataMap[crudMetadata.ClrType] = crudMetadata;
        }

        /// <inheritdoc />
        public EntityCrudMetadata GetOrAddCrud(Type type, Func<Type, EntityCrudMetadata> factory)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            return this.crudMetadataMap.GetOrAdd(type, factory);
        }
    }
}
