using Atis.Orm.Abstractions;
using System;

namespace Atis.Orm.Metadata
{
    /// <inheritdoc />
    public class OrmModelSource : IOrmModelSource
    {
        private readonly IEntityMetadataBuilder entityMetadataBuilder;
        private readonly IEntityCrudMetadataFactory crudMetadataFactory;
        private readonly OrmModel model;

        /// <summary>Constructs the source and the empty model it will populate.</summary>
        public OrmModelSource(IEntityMetadataBuilder entityMetadataBuilder, IEntityCrudMetadataFactory crudMetadataFactory)
        {
            this.entityMetadataBuilder = entityMetadataBuilder ?? throw new ArgumentNullException(nameof(entityMetadataBuilder));
            this.crudMetadataFactory = crudMetadataFactory ?? throw new ArgumentNullException(nameof(crudMetadataFactory));
            this.model = new OrmModel(entityMetadataBuilder);
        }

        /// <inheritdoc />
        public IOrmModel GetModel(DataContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            this.model.EnsureModelInitialized(() => this.BuildModel(context));
            return this.model;
        }

        /// <summary>
        ///     Runs <paramref name="context"/>'s <c>OnModelCreating</c> against a fresh
        ///     <see cref="ModelBuilder"/> and commits the result into the model. Override to change what
        ///     the builder is seeded with.
        /// </summary>
        protected virtual void BuildModel(DataContext context)
        {
            var modelBuilder = new ModelBuilder(this.entityMetadataBuilder, this.crudMetadataFactory, this.model);
            context.OnModelCreating(modelBuilder);
            modelBuilder.Build();
        }
    }
}
