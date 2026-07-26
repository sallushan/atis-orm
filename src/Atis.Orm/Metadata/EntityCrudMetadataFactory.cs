using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Atis.Orm.Abstractions;
using Atis.Orm.Annotations;

namespace Atis.Orm.Metadata
{
    /// <inheritdoc />
    public class EntityCrudMetadataFactory : IEntityCrudMetadataFactory
    {
        private readonly IEntityMetadataBuilder entityMetadataBuilder;

        /// <summary>
        ///     Constructs the factory.
        /// </summary>
        /// <param name="entityMetadataBuilder">
        ///     Supplies the column set, so that the persistence side and the query side of a mapping
        ///     always describe the same properties.
        /// </param>
        public EntityCrudMetadataFactory(IEntityMetadataBuilder entityMetadataBuilder)
        {
            this.entityMetadataBuilder = entityMetadataBuilder ?? throw new ArgumentNullException(nameof(entityMetadataBuilder));
        }

        /// <inheritdoc />
        public EntityCrudMetadata Build(Type type)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            var columns = this.entityMetadataBuilder
                                .GetColumnProperties(type)
                                .Select(x => this.CreateColumn(type, x))
                                .ToArray();
            return new EntityCrudMetadata(type, columns);
        }

        /// <summary>
        ///     Creates the persistence side of a single column. Override to change how any of it is
        ///     derived.
        /// </summary>
        protected virtual CrudColumn CreateColumn(Type type, PropertyInfo propertyInfo)
        {
            var isRequired = this.IsRequired(propertyInfo, out var requiredFieldTitle);
            return new CrudColumn(propertyInfo, this.GetColumnKind(propertyInfo), isRequired, requiredFieldTitle);
        }

        /// <summary>
        ///     <para>
        ///         Determines how a column participates in Insert and Update.
        ///     </para>
        ///     <para>
        ///         The kinds are mutually exclusive, so a property carrying more than one of the
        ///         annotations is a mapping error. That is not diagnosed here — this runs for every
        ///         entity, including ones that are only ever queried, and a query has no business
        ///         failing over an annotation it does not use. It is diagnosed when the entity is first
        ///         used for Insert / Update / Delete.
        ///     </para>
        /// </summary>
        protected virtual ColumnKind GetColumnKind(PropertyInfo propertyInfo)
        {
            if (propertyInfo.GetCustomAttribute<DbIdentityColumnAttribute>() != null)
                return ColumnKind.Identity;
            if (propertyInfo.GetCustomAttribute<DbRowVersionAttribute>() != null)
                return ColumnKind.RowVersion;
            if (propertyInfo.GetCustomAttribute<DbReadOnlyColumnAttribute>() != null)
                return ColumnKind.ReadOnly;
            if (propertyInfo.GetCustomAttribute<DbInsertOnlyAttribute>() != null)
                return ColumnKind.InsertOnly;
            if (propertyInfo.GetCustomAttribute<DbUpdateOnlyAttribute>() != null)
                return ColumnKind.UpdateOnly;
            return ColumnKind.Regular;
        }

        /// <summary>
        ///     Determines whether a value must be supplied for this column before a write, and under
        ///     what name a failure is reported.
        /// </summary>
        protected virtual bool IsRequired(PropertyInfo propertyInfo, out string requiredFieldTitle)
        {
            var attribute = propertyInfo.GetCustomAttribute<RequiredFieldValidationAttribute>();
            if (attribute is null)
            {
                requiredFieldTitle = null;
                return false;
            }
            requiredFieldTitle = string.IsNullOrWhiteSpace(attribute.FieldTitle) ? null : attribute.FieldTitle;
            return true;
        }
    }
}
