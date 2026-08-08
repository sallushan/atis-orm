using Atis.SqlExpressionEngine;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using Atis.Orm.Abstractions;
using Atis.Orm.Annotations;
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
        ///     configured, and decides which types are entities at all — see <see cref="CanBeEntity"/>.
        /// </param>
        public OrmModel(IEntityMetadataBuilder entityMetadataBuilder)
        {
            this.entityMetadataBuilder = entityMetadataBuilder ?? throw new ArgumentNullException(nameof(entityMetadataBuilder));
        }

        /// <inheritdoc />
        /// <remarks>
        ///     <para>
        ///         The caller has established that <paramref name="type"/> is an entity, so this derives
        ///         the mapping from annotations rather than failing. That is what lets a navigation reach
        ///         a target no entry point ever named: the metadata builder emits a bare
        ///         <c>QueryRootExpression</c> for the target, and this is where the converter turns it
        ///         into a table.
        ///     </para>
        ///     <para>
        ///         Never overwrites a mapping <c>OnModelCreating</c> configured — the model is fully
        ///         built before it can be resolved, so a configured entry is always already present.
        ///     </para>
        /// </remarks>
        public EntityMetadata GetRequiredEntity(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (!this.CanBeEntity(type))
                throw new InvalidOperationException(
                    $"'{type.Name}' is not an entity. Mark it with [{nameof(DbTableAttribute)}] or configure " +
                    $"it in OnModelCreating.");
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

        /// <inheritdoc />
        /// <remarks>
        ///     <para>
        ///         Two ways to be an entity, one per clause. Already in the map covers everything
        ///         <c>OnModelCreating</c> configured and everything already derived. Otherwise the builder
        ///         decides, because recognising an entity is a question about annotations and the builder
        ///         is what reads them — keeping that half here would put the annotation vocabulary inside
        ///         the store, and force a consumer with their own annotations to replace the model as well
        ///         as the builder.
        ///     </para>
        /// </remarks>
        public bool CanBeEntity(Type type)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));
            return this.metadataMap.ContainsKey(type) || this.entityMetadataBuilder.CanBuild(type);
        }
    }
}
