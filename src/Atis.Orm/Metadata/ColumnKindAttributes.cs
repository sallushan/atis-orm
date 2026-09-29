using System.Reflection;

using Atis.Orm.Annotations;

namespace Atis.Orm.Metadata
{
    /// <summary>
    ///     Reads the column kind annotations off a property. Shared by the annotation based metadata
    ///     factory and by the fluent value object mapping, so a leaf of a value object is annotated the
    ///     same way as a property of an entity.
    /// </summary>
    internal static class ColumnKindAttributes
    {
        public static ColumnKind GetKind(PropertyInfo propertyInfo)
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
    }
}
