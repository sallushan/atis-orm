using System.Collections.Generic;
using System.Linq.Expressions;

using Atis.SqlExpressionEngine.Abstractions;

namespace Atis.SqlExpressionEngine.Services
{
    /// <summary>
    ///     <para>
    ///         Default <see cref="IVariableIdentityProvider"/>: for a member-access chain the identity is the
    ///         dotted member path rooted at the captured container's <em>type</em>, e.g.
    ///         <c>"Some.Ns+&lt;&gt;c__DisplayClass0_0.i1.q1"</c>. The type prefix is a compile-time constant
    ///         (identical on every invocation of the same compiled query) and distinguishes same-named locals
    ///         captured in different scopes (different display classes), so the identity is effectively
    ///         collision-free without needing a positional tiebreaker. Anything that is not a pure member chain
    ///         falls back to a structural string.
    ///     </para>
    /// </summary>
    public class VariableIdentityProvider : IVariableIdentityProvider
    {
        /// <inheritdoc />
        public string GetIdentity(Expression variableNode)
        {
            if (variableNode is MemberExpression member)
            {
                var parts = new List<string>();
                Expression current = member;
                while (current is MemberExpression m)
                {
                    parts.Add(m.Member.Name);
                    current = m.Expression;
                }
                parts.Reverse();

                string root;
                if (current is ConstantExpression constant)
                {
                    // Captured-local closure: the display-class type distinguishes different capture scopes.
                    root = constant.Type.FullName;
                }
                else if (current == null)
                {
                    // Static member access (no container instance).
                    root = member.Member.DeclaringType?.FullName ?? "static";
                }
                else
                {
                    // Root is a dynamic expression (method call, index, etc.) - fall back to a structural key.
                    return Structural(variableNode);
                }

                return root + "." + string.Join(".", parts);
            }

            return Structural(variableNode);
        }

        private static string Structural(Expression node)
        {
            // Deterministic and self-contained; used only for the uncommon non-member variable shapes
            // (e.g. GetThing().Prop, arr[0].Value). Expression.ToString() renders captured-container types
            // rather than instance values, so it stays stable across invocations of the same compiled query.
            return node.NodeType + ":" + node.ToString();
        }
    }
}
