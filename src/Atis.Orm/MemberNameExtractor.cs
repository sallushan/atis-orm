using System;
using System.Linq.Expressions;

namespace Atis.Orm
{
    internal static class MemberNameExtractor
    {
        public static string GetMemberName(LambdaExpression property)
        {
            if (property == null) throw new ArgumentNullException(nameof(property));

            return GetMemberName(property.Body, nameof(property));
        }

        /// <summary>
        ///     <para>
        ///         Extracts one or more member names from a key selector. A single member access
        ///         (<c>x =&gt; x.Key</c>) yields a one-element array; an anonymous-object body
        ///         (<c>x =&gt; new { x.A, x.B }</c>) yields one entry per member, in declaration order.
        ///     </para>
        /// </summary>
        public static string[] GetMemberNames(LambdaExpression property)
        {
            if (property == null) throw new ArgumentNullException(nameof(property));

            if (property.Body is NewExpression newExpression)
            {
                if (newExpression.Arguments.Count == 0)
                    throw new ArgumentException("Composite key selector must contain at least one member.", nameof(property));

                var names = new string[newExpression.Arguments.Count];
                for (var i = 0; i < newExpression.Arguments.Count; i++)
                    names[i] = GetMemberName(newExpression.Arguments[i], nameof(property));
                return names;
            }

            return new[] { GetMemberName(property.Body, nameof(property)) };
        }

        private static string GetMemberName(Expression expression, string paramName)
        {
            var memberExpression = expression as MemberExpression
                                   ?? (expression is UnaryExpression unary ? unary.Operand as MemberExpression : null);

            if (memberExpression == null)
                throw new ArgumentException("Expression must be a simple member access.", paramName);

            return memberExpression.Member.Name;
        }
    }
}