using Atis.SqlExpressionEngine;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Atis.Orm.Abstractions
{
    /// <summary>
    ///
    /// </summary>
    public interface IEntityMetadataBuilder
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        EntityMetadata Build(Type type);

        /// <summary>
        ///     <para>
        ///         Whether <paramref name="type"/> is a type this builder recognises as an entity — for
        ///         the default builder, whether it carries the annotation that marks one.
        ///     </para>
        ///     <para>
        ///         Deliberately here rather than on the model. Recognising an entity and mapping one read
        ///         the same annotations, so a consumer who redefines the first must redefine the second;
        ///         keeping both on this interface is what stops the two from drifting apart. The model
        ///         combines this with what it already holds — see <c>IModel.CanBeEntity</c>.
        ///     </para>
        ///     <para>
        ///         Not consulted when an entity is configured through <c>OnModelCreating</c>. Configuring
        ///         a type fluently is itself a declaration that it is an entity, and those types are
        ///         seeded through <see cref="Build(Type)"/> without passing here.
        ///     </para>
        /// </summary>
        bool CanBuild(Type type);

        /// <summary>
        ///     <para>
        ///         Returns the properties of <paramref name="type"/> that map to table columns — that
        ///         is, everything <see cref="Build(Type)"/> would turn into a column.
        ///     </para>
        ///     <para>
        ///         Exposed so that the persistence side of the mapping
        ///         (<see cref="IEntityCrudMetadataFactory"/>) can be built from the same column set and
        ///         cannot drift from the query side.
        ///     </para>
        /// </summary>
        IReadOnlyList<PropertyInfo> GetColumnProperties(Type type);
    }
}
