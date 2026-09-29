using System.Reflection;

using Atis.SqlExpressionEngine.Abstractions;

namespace Atis.SqlExpressionEngine.Services
{
    /// <summary>
    ///     An <see cref="IContextualMemberProvider"/> that marks nothing, for hand-wired code that uses no
    ///     context values.
    /// </summary>
    public sealed class NoContextualMembers : IContextualMemberProvider
    {
        /// <inheritdoc />
        public bool TryGetContextualKey(MemberInfo member, out string key)
        {
            key = null;
            return false;
        }
    }
}
