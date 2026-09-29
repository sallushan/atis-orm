using System.Reflection;

namespace Atis.SqlExpressionEngine.Abstractions
{
    /// <summary>
    ///     <para>
    ///         Tells the engine which members stand for a value the execution context supplies (the logged-in
    ///         user, the current tenant) rather than one the query carries. Such a member is never read: it is
    ///         replaced by a contextual node and its value is asked for, by key, on every execution.
    ///     </para>
    ///     <para>
    ///         How a member comes to be marked — an annotation, a fluent call — is the implementation's
    ///         business, the same as for navigations and calculated properties. Deliberately separate from
    ///         <see cref="IModel"/>: this is not about entity mapping.
    ///     </para>
    /// </summary>
    public interface IContextualMemberProvider
    {
        /// <summary>
        ///     Whether <paramref name="member"/> is a context value; when it is, <paramref name="key"/> is the
        ///     name to ask the context for.
        /// </summary>
        bool TryGetContextualKey(MemberInfo member, out string key);
    }
}
