using System;
using System.Linq;
using System.Reflection;

namespace Atis.Orm.Metadata
{
    /// <summary>
    ///     <para>
    ///         Lists the public instance properties of an entity type, the way the mapping should see
    ///         them.
    ///     </para>
    ///     <para>
    ///         <c>Type.GetProperties()</c> returns <i>both</i> a base property and the derived property
    ///         that hides it with <c>new</c> when their types differ. Mapping both would give the entity
    ///         two columns under one property name, so only the most-derived one is kept. An
    ///         <c>override</c> never shows up twice, so it is not affected.
    ///     </para>
    /// </summary>
    internal static class EntityProperties
    {
        public static PropertyInfo[] Get(Type type)
        {
            var all = type.GetProperties();
            return all.Where(p => !IsHidden(p, all)).ToArray();
        }

        /// <summary>
        ///     Finds a static property or field by name, starting at <paramref name="type"/> and walking up
        ///     its base classes. <c>GetMember</c> does not look in base classes for static members unless
        ///     told to, and even then it skips private ones; walking the chain finds both, and a member
        ///     declared nearer the entity wins over one further up.
        /// </summary>
        public static MemberInfo FindStaticMember(Type type, string name)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;
            for (var current = type; current != null; current = current.BaseType)
            {
                var member = current.GetMember(name, flags).FirstOrDefault();
                if (member != null)
                    return member;
            }
            return null;
        }

        private static bool IsHidden(PropertyInfo property, PropertyInfo[] all)
            => all.Any(other => !ReferenceEquals(other, property)
                                && other.Name == property.Name
                                && other.DeclaringType != property.DeclaringType
                                && other.DeclaringType.IsSubclassOf(property.DeclaringType));
    }
}
