using Atis.SqlExpressionEngine;
using Atis.SqlExpressionEngine.ExpressionExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Atis.Orm
{
    /// <summary>
    ///     Converts the state collected by the ORM fluent API into the same QueryExtensions.Update
    ///     method-call expression used by the SQL expression engine's query-based API.
    /// </summary>
    internal static class UpdateEntityMethodCallFactory
    {
        public static MethodCallExpression CreateAffectedRowsCall<T>(IReadOnlyList<FieldValuePair> setters, IReadOnlyList<FieldValuePair> keys)
        {
            var updateMethod = new Func<IQueryable<T>, Expression<Func<T, T>>, Expression<Func<T, bool>>, int>(
                QueryExtensions.Update).Method;

            return Expression.Call(
                null,
                updateMethod,
                new QueryRootExpression(typeof(T)),
                Expression.Quote(CreateSetterLambda<T>(setters)),
                Expression.Quote(CreatePredicateLambda<T>(keys)));
        }

        public static MethodCallExpression CreateOutputCall<T>(IReadOnlyList<FieldValuePair> setters, IReadOnlyList<FieldValuePair> keys, IReadOnlyList<LambdaExpression> outputs)
        {
            var updateMethod = new Func<IQueryable<T>, Expression<Func<T, T>>, Expression<Func<T, bool>>, Expression<Func<T, object[]>>, IReadOnlyList<IReadOnlyDictionary<string, object>>>(
                QueryExtensions.Update).Method;

            return Expression.Call(
                null,
                updateMethod,
                new QueryRootExpression(typeof(T)),
                Expression.Quote(CreateSetterLambda<T>(setters)),
                Expression.Quote(CreatePredicateLambda<T>(keys)),
                Expression.Quote(CreateOutputLambda<T>(outputs)));
        }

        private static Expression<Func<T, T>> CreateSetterLambda<T>(IReadOnlyList<FieldValuePair> setters)
        {
            if (setters is null)
                throw new ArgumentNullException(nameof(setters));
            if (setters.Count == 0)
                throw new InvalidOperationException("At least one Set call is required to update an entity.");

            var entityType = typeof(T);
            if (!entityType.IsValueType && entityType.GetConstructor(Type.EmptyTypes) is null)
                throw new InvalidOperationException($"Entity '{entityType.Name}' needs a parameterless constructor to be updated through UpdateEntity.");

            var bindings = new List<MemberBinding>(setters.Count);
            foreach (var setter in setters)
            {
                var member = GetSelectedMember(setter.FieldSelector);
                var memberType = GetSettableMemberType(entityType, member);

                // Set accepts Expression<Func<FT>>, so the value body has no entity parameter.
                // The parameter on FieldSelector only identifies the member being initialized.
                var value = setter.ValueSelector.Body;
                if (!memberType.IsAssignableFrom(value.Type))
                {
                    // Reachable because GetSelectedMember strips Convert nodes off the field
                    // selector, so FT can be wider than the member it names.
                    throw new InvalidOperationException(
                        $"Set gave '{entityType.Name}.{member.Name}' a value of type '{value.Type}', which cannot be assigned to a member of type '{memberType}'.");
                }

                bindings.Add(Expression.Bind(member, value));
            }

            var parameter = Expression.Parameter(entityType, "x");
            // Update requires Expression<Func<T, T>> even though entity-value setters do not read x.
            return Expression.Lambda<Func<T, T>>(
                Expression.MemberInit(Expression.New(entityType), bindings),
                parameter);
        }

        private static Expression<Func<T, bool>> CreatePredicateLambda<T>(IReadOnlyList<FieldValuePair> keys)
        {
            if (keys is null)
                throw new ArgumentNullException(nameof(keys));
            if (keys.Count == 0)
                throw new InvalidOperationException("At least one Key call is required to update an entity.");

            var parameter = Expression.Parameter(typeof(T), "x");
            Expression predicate = null;
            foreach (var key in keys)
            {
                var field = ReplaceParameter(key.FieldSelector, parameter);
                var equality = Expression.Equal(field, key.ValueSelector.Body);
                predicate = predicate is null ? equality : Expression.AndAlso(predicate, equality);
            }
            return Expression.Lambda<Func<T, bool>>(predicate, parameter);
        }

        private static Expression<Func<T, object[]>> CreateOutputLambda<T>(IReadOnlyList<LambdaExpression> outputs)
        {
            if (outputs is null)
                throw new ArgumentNullException(nameof(outputs));
            if (outputs.Count == 0)
                throw new InvalidOperationException("At least one Output call is required to return updated rows.");

            var parameter = Expression.Parameter(typeof(T), "x");
            var outputFields = new Expression[outputs.Count];
            for (var i = 0; i < outputs.Count; i++)
            {
                var selectedField = ReplaceParameter(outputs[i], parameter);
                outputFields[i] = Expression.Convert(selectedField, typeof(object));
            }

            return Expression.Lambda<Func<T, object[]>>(
                Expression.NewArrayInit(typeof(object), outputFields),
                parameter);
        }

        private static Expression ReplaceParameter(LambdaExpression expression, ParameterExpression replacement)
        {
            if (expression.Parameters.Count != 1)
                throw new ArgumentException("A field selector must have exactly one parameter.", nameof(expression));
            return new ParameterReplacingVisitor(expression.Parameters[0], replacement).Visit(expression.Body);
        }

        /// <summary>
        ///     The type of a member Set can write to. A member with no setter is not a stored column —
        ///     a calculated property is the usual way to arrive here.
        /// </summary>
        private static Type GetSettableMemberType(Type entityType, MemberInfo member)
        {
            switch (member)
            {
                case PropertyInfo property when property.CanWrite:
                    return property.PropertyType;
                case FieldInfo field:
                    return field.FieldType;
                default:
                    throw new InvalidOperationException(
                        $"'{entityType.Name}.{member.Name}' cannot be assigned by Set. A calculated or read-only member is not a stored column.");
            }
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
                   ?? throw new ArgumentException(
                       $"Expected a member selector such as 'x => x.LastName', but got '{selector.Body}'.",
                       nameof(selector));
        }

        private sealed class ParameterReplacingVisitor : ExpressionVisitor
        {
            private readonly ParameterExpression source;
            private readonly ParameterExpression replacement;

            public ParameterReplacingVisitor(ParameterExpression source, ParameterExpression replacement)
            {
                this.source = source;
                this.replacement = replacement;
            }

            protected override Expression VisitParameter(ParameterExpression node)
                => node == this.source ? this.replacement : base.VisitParameter(node);
        }
    }
}
