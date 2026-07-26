using System;

using Atis.Orm.Metadata;

namespace Atis.Orm.Abstractions
{
    /// <summary>
    ///     <para>
    ///         Builds the persistence side of an entity's mapping — column kinds and required field
    ///         information — from the annotations on the entity type.
    ///     </para>
    ///     <para>
    ///         This is the counterpart of <see cref="IEntityMetadataBuilder"/>, which builds the query
    ///         side. Both are driven from the same column set, so the two can never disagree about
    ///         which properties are columns.
    ///     </para>
    /// </summary>
    public interface IEntityCrudMetadataFactory
    {
        /// <summary>
        ///     Builds the persistence side metadata for <paramref name="type"/>.
        /// </summary>
        EntityCrudMetadata Build(Type type);
    }
}
