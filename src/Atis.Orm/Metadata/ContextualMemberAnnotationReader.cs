using System.Collections.Concurrent;
using System.Reflection;

using Atis.Orm.Abstractions;
using Atis.Orm.Annotations;

namespace Atis.Orm.Metadata
{
    /// <summary>
    ///     Default reader: looks for <see cref="ContextualValueAttribute"/>. Cached, because value extraction
    ///     asks about every variable on every cache hit.
    /// </summary>
    public class ContextualMemberAnnotationReader : IContextualMemberAnnotationReader
    {
        private readonly ConcurrentDictionary<MemberInfo, string> cache = new ConcurrentDictionary<MemberInfo, string>();

        /// <inheritdoc />
        public string GetKey(MemberInfo member)
        {
            // The empty string stands for "looked, not marked" so the answer is cached either way.
            var key = this.cache.GetOrAdd(member, m =>
            {
                var attribute = m.GetCustomAttribute<ContextualValueAttribute>(inherit: true);
                return attribute == null ? string.Empty : (attribute.Key ?? m.Name);
            });
            return key.Length == 0 ? null : key;
        }
    }
}
