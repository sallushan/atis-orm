using System;
using System.Linq.Expressions;

namespace Atis.Orm
{
    /// <summary>
    ///     <para>
    ///         Builds a navigation join-condition lambda of the form
    ///         <c>(parent, child) =&gt; parent.ParentKey == child.ChildKey</c> from key property names.
    ///     </para>
    ///     <para>
    ///         The parameter order is always <c>(parent, child)</c>, matching <c>NavigationInfo.JoinCondition</c>.
    ///         Composite-key joins are expected to use the explicit-lambda overloads instead.
    ///     </para>
    /// </summary>
    internal static class JoinConditionFactory
    {
        public static LambdaExpression Create(Type parentType, string parentKey, Type childType, string childKey)
        {
            if (parentType == null) throw new ArgumentNullException(nameof(parentType));
            if (parentKey == null) throw new ArgumentNullException(nameof(parentKey));
            if (childType == null) throw new ArgumentNullException(nameof(childType));
            if (childKey == null) throw new ArgumentNullException(nameof(childKey));

            var parentParameter = Expression.Parameter(parentType, "auto_p");
            var childParameter = Expression.Parameter(childType, "auto_c");

            var parentProperty = parentType.GetProperty(parentKey)
                                    ?? throw new InvalidOperationException($"Property '{parentKey}' not found in '{parentType.Name}'.");
            var childProperty = childType.GetProperty(childKey)
                                    ?? throw new InvalidOperationException($"Property '{childKey}' not found in '{childType.Name}'.");

            var joinExpression = Expression.Equal(
                Expression.Property(parentParameter, parentProperty),
                Expression.Property(childParameter, childProperty));

            return Expression.Lambda(joinExpression, parentParameter, childParameter);
        }
    }
}
