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
