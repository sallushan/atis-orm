using System;

namespace Atis.Orm.Annotations
{
    /// <summary>
    ///     <para>
    ///         Excludes a property from the entity's column set entirely. The property takes no part in
    ///         query translation and is neither written nor read by entity level Insert / Update /
    ///         Delete.
    ///     </para>
    ///     <para>
    ///         Every public property that is not a navigation or a calculated property is otherwise
    ///         treated as a column, so this is the way to keep a purely in-memory property — the record
    ///         state of an entity, a cached display string — out of the mapping.
    ///     </para>
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Honored by <c>EntityMetadataBuilder.IsSchemaRelatedProperty</c>. A derived metadata
    ///         builder that overrides that method is responsible for honoring it too.
    ///     </para>
    ///     <para>
    ///         Named with the <c>Db</c> prefix, like the rest of this namespace, so that it does not
    ///         collide with <c>System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute</c> in
    ///         entity files that import both.
    ///     </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class DbNotMappedAttribute : Attribute
    {
    }
}
