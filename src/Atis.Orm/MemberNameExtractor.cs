using System;
using System.Linq.Expressions;

namespace Atis.Orm
{
    internal static class MemberNameExtractor
    {
        public static string GetMemberName(LambdaExpression property)
        {
            if (property == null) throw new ArgumentNullException(nameof(property));

            var memberExpression = property.Body as MemberExpression
                                   ?? (property.Body is UnaryExpression unary ? unary.Operand as MemberExpression : null);

            if (memberExpression == null)
                throw new ArgumentException("Expression must be a simple member access.", nameof(property));

            return memberExpression.Member.Name;
        }
    }
}