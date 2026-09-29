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
                                .Concat(this.CreateValueObjectColumns(type))
                                .ToArray();
            return new EntityCrudMetadata(type, columns);
        }

        /// <summary>
        ///     <para>
        ///         The persistence side of every column a <see cref="ValueObjectAttribute"/> maps, in the
        ///         same order the query side lists them. Each is keyed by the dotted path the query side
        ///         uses, and takes its kind from the annotations on the value object's own property.
        ///     </para>
        ///     <para>
        ///         A member the attribute names but the value object does not have is left out, for the
        ///         same reason <c>MutableEntityMetadata.BuildCrud</c> leaves out an unresolvable column: the
        ///         entity may only ever be queried.
        ///     </para>
        /// </summary>
        protected virtual IEnumerable<CrudColumn> CreateValueObjectColumns(Type type)
        {
            foreach (var property in type.GetProperties())
            {
                var attribute = property.GetCustomAttribute<ValueObjectAttribute>();
                if (attribute is null)
                    continue;
                foreach (var memberName in attribute.Properties)
                {
                    var path = ColumnPath.TryResolve(type, property.Name + "." + memberName);
                    if (path is null)
                        continue;
                    yield return new CrudColumn(path, this.GetColumnKind(path.Leaf), isRequired: false, requiredFieldTitle: null);
                }
            }
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
            => ColumnKindAttributes.GetKind(propertyInfo);

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
