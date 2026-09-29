using System.Reflection;

namespace Atis.Orm.Abstractions
{
    /// <summary>
    ///     <para>
    ///         Reads the annotation that marks a member as a context value. Kept behind an interface, like
    ///         <see cref="IEntityMetadataBuilder"/>, so a consumer with their own annotations replaces the
    ///         reader and not the model.
    ///     </para>
    /// </summary>
    public interface IContextualMemberAnnotationReader
    {
        /// <summary>Returns the context key <paramref name="member"/> is annotated with, or <c>null</c>.</summary>
        string GetKey(MemberInfo member);
    }
}
