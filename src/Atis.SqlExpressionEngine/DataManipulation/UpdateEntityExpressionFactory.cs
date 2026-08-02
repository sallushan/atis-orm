using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Atis.SqlExpressionEngine.ExpressionExtensions;

namespace Atis.SqlExpressionEngine
{

    /// <summary>
    ///     Turns what the fluent stages collected into the <see cref="UpdateEntityExpression"/> the
    ///     converter consumes.
    /// </summary>
    internal static class UpdateEntityExpressionFactory
    {
        public static UpdateEntityExpression Create(Type resultType, Type entityType, IReadOnlyList<FieldValuePair> setters, IReadOnlyList<FieldValuePair> keys, IReadOnlyList<Expression> outputs)
        {
            var query = new QueryRootExpression(entityType);
            var setterLambda = CreateSetterLambda(entityType, setters);

            var keyExpressions = new List<Expression>(keys.Count);
            foreach (var key in keys)
            {
                // Equality is the actual semantics here, unlike the SET list.
                var equalExpression = Expression.Equal(key.FieldSelector.Body, key.ValueSelector.Body);
                keyExpressions.Add(Expression.Lambda(equalExpression, key.FieldSelector.Parameters[0]));
            }

            // An update without an output clause carries an empty list rather than null, so that the
            // converter can treat all of them alike.
            var outputFields = new CollectionExpression(outputs ?? (IReadOnlyList<Expression>)new Expression[0]);
            return new UpdateEntityExpression(resultType, query, setterLambda, new CollectionExpression(keyExpressions), outputFields);
        }

        /// <summary>
        ///     <para>
        ///         Builds the SET list as <c>x =&gt; new T { Member = value, ... }</c> — the same shape the
        ///         query-based <c>Update</c> API passes, so the converter resolves columns through the
        ///         entity's mapping metadata rather than inferring them from an already-converted
        ///         expression.
        ///     </para>
        ///     <para>
        ///         The field selector is only mined for the member it names; its body is never re-hosted in
        ///         the tree, so each <c>Set</c> call's own lambda parameter simply falls away. The value
        ///         expression, by contrast, is placed in the tree verbatim, which is what keeps a captured
        ///         variable visible for cache-hit rebinding.
        ///     </para>
        /// </summary>
        private static LambdaExpression CreateSetterLambda(Type entityType, IReadOnlyList<FieldValuePair> setters)
        {
            if (setters.Count == 0)
                throw new InvalidOperationException("At least one Set call is required to update an entity.");
            if (entityType.GetConstructor(Type.EmptyTypes) is null)
                throw new InvalidOperationException($"Entity '{entityType.Name}' needs a parameterless constructor to be updated through UpdateEntity.");

            var bindings = new List<MemberBinding>(setters.Count);
            foreach (var setter in setters)
            {
                var member = GetSelectedMember(setter.FieldSelector);
                try
                {
                    bindings.Add(Expression.Bind(member, setter.ValueSelector.Body));
                }
                catch (ArgumentException ex)
                {
                    // Most often a get-only member: a calculated property is not a column and cannot be set.
                    throw new InvalidOperationException(
                        $"'{entityType.Name}.{member.Name}' cannot be assigned by Set. A calculated or read-only member is not a stored column.", ex);
                }
            }

            return Expression.Lambda(
                Expression.MemberInit(Expression.New(entityType), bindings),
                Expression.Parameter(entityType, "x"));
        }

        private static MemberInfo GetSelectedMember(LambdaExpression selector)
        {
            var body = selector.Body;
            while (body is UnaryExpression unary &&
                   (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
            {
                body = unary.Operand;
            }

            return (body as MemberExpression)?.Member
                ?? throw new ArgumentException($"Expected a member selector such as 'x => x.LastName', but got '{selector.Body}'.", nameof(selector));
        }
    }
}
