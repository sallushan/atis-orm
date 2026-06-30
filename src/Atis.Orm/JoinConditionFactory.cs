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
            if (parentKey == null) throw new ArgumentNullException(nameof(parentKey));
            if (childKey == null) throw new ArgumentNullException(nameof(childKey));

            return Create(parentType, new[] { parentKey }, childType, new[] { childKey });
        }

        public static LambdaExpression Create(Type parentType, string[] parentKeys, Type childType, string[] childKeys)
        {
            if (parentType == null) throw new ArgumentNullException(nameof(parentType));
            if (parentKeys == null) throw new ArgumentNullException(nameof(parentKeys));
            if (childType == null) throw new ArgumentNullException(nameof(childType));
            if (childKeys == null) throw new ArgumentNullException(nameof(childKeys));

            if (parentKeys.Length == 0 || childKeys.Length == 0)
                throw new InvalidOperationException($"Join between '{parentType.Name}' and '{childType.Name}' must specify at least one key on each side.");

            if (parentKeys.Length != childKeys.Length)
                throw new InvalidOperationException($"Composite key mismatch between '{parentType.Name}' ({parentKeys.Length} keys) and '{childType.Name}' ({childKeys.Length} keys).");

            var parentParameter = Expression.Parameter(parentType, "auto_p");
            var childParameter = Expression.Parameter(childType, "auto_c");

            Expression joinExpression = null;
            for (var i = 0; i < parentKeys.Length; i++)
            {
                var parentProperty = parentType.GetProperty(parentKeys[i])
                                        ?? throw new InvalidOperationException($"Property '{parentKeys[i]}' not found in '{parentType.Name}'.");
                var childProperty = childType.GetProperty(childKeys[i])
                                        ?? throw new InvalidOperationException($"Property '{childKeys[i]}' not found in '{childType.Name}'.");

                var equality = Expression.Equal(
                    Expression.Property(parentParameter, parentProperty),
                    Expression.Property(childParameter, childProperty));

                joinExpression = joinExpression == null ? (Expression)equality : Expression.AndAlso(joinExpression, equality);
            }

            return Expression.Lambda(joinExpression, parentParameter, childParameter);
        }
    }
}
