using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Atis.Orm
{
    /// <summary>
    ///     <para>
    ///         The words one fluent entity API calls itself by. The lambda-building work below is the
    ///         same for every such API, but its diagnostics must name the method the caller actually
    ///         typed — telling someone who wrote <c>Value</c> that <c>Set</c> rejected their member
    ///         sends them looking at the wrong line.
    ///     </para>
    /// </summary>
    internal sealed class EntityFluentApiNames
    {
        public static readonly EntityFluentApiNames Update =
            new EntityFluentApiNames(nameof(DataContext.UpdateEntity), "Set", "update", "updated");

        public static readonly EntityFluentApiNames Insert =
            new EntityFluentApiNames(nameof(DataContext.InsertEntity), "Value", "insert", "inserted");

        // Delete assigns no column, so it never reaches the setter diagnostics; Key is the only
        // method it can be told it is missing.
        public static readonly EntityFluentApiNames Delete =
            new EntityFluentApiNames(nameof(DataContext.DeleteEntity), "Key", "delete", "deleted");

        private EntityFluentApiNames(string apiMethod, string setterMethod, string operationVerb, string operationPastTense)
        {
            this.ApiMethod = apiMethod;
            this.SetterMethod = setterMethod;
            this.OperationVerb = operationVerb;
            this.OperationPastTense = operationPastTense;
        }

        /// <summary>The entry point, for example <c>UpdateEntity</c>.</summary>
        public string ApiMethod { get; }

        /// <summary>The method that assigns a column, for example <c>Set</c>.</summary>
        public string SetterMethod { get; }

        /// <summary>The operation as a verb, for example <c>update</c>.</summary>
        public string OperationVerb { get; }

        /// <summary>The operation as a past tense, for example <c>updated</c>.</summary>
        public string OperationPastTense { get; }
    }

    /// <summary>
    ///     <para>
    ///         Builds the LINQ lambdas that the fluent entity APIs hand to
    ///         <see cref="SqlExpressionEngine.QueryExtensions"/>. Insert and update collect different
    ///         state and emit different method calls, but the translation from
    ///         <see cref="FieldValuePair"/>s and output selectors into lambdas is identical — only the
    ///         names in the diagnostics differ, which is what <see cref="EntityFluentApiNames"/> carries.
    ///     </para>
    /// </summary>
    internal static class EntityLambdaFactory
    {
        /// <summary>
        ///     <para>
        ///         The <c>new T { Member = value }</c> body shared by both APIs. Both accept the value as
        ///         <c>Expression&lt;Func&lt;FT&gt;&gt;</c>, so the value body has no entity parameter —
        ///         the parameter on the field selector only identifies the member being initialized.
        ///     </para>
        /// </summary>
        public static MemberInitExpression CreateMemberInit<T>(
            IReadOnlyList<FieldValuePair> fieldValues,
            EntityFluentApiNames names)
        {
            if (fieldValues is null)
                throw new ArgumentNullException(nameof(fieldValues));
            if (fieldValues.Count == 0)
                throw new InvalidOperationException($"At least one {names.SetterMethod} call is required to {names.OperationVerb} an entity.");

            var entityType = typeof(T);
            if (!entityType.IsValueType && entityType.GetConstructor(Type.EmptyTypes) is null)
                throw new InvalidOperationException($"Entity '{entityType.Name}' needs a parameterless constructor to be {names.OperationPastTense} through {names.ApiMethod}.");

            var bindings = new List<MemberBinding>(fieldValues.Count);
            foreach (var fieldValue in fieldValues)
            {
                var member = GetSelectedMember(fieldValue.FieldSelector);
                var memberType = GetSettableMemberType(entityType, member, names);

                var value = fieldValue.ValueSelector.Body;
                if (!memberType.IsAssignableFrom(value.Type))
                {
                    // Reachable because GetSelectedMember strips Convert nodes off the field
                    // selector, so FT can be wider than the member it names.
                    throw new InvalidOperationException(
                        $"{names.SetterMethod} gave '{entityType.Name}.{member.Name}' a value of type '{value.Type}', which cannot be assigned to a member of type '{memberType}'.");
                }

                bindings.Add(Expression.Bind(member, value));
            }

            return Expression.MemberInit(Expression.New(entityType), bindings);
        }

        /// <summary>Rewrites the collected output selectors into the single array lambda the OUTPUT clause is built from.</summary>
        public static Expression<Func<T, object[]>> CreateOutputLambda<T>(
            IReadOnlyList<LambdaExpression> outputs,
            EntityFluentApiNames names)
        {
            if (outputs is null)
                throw new ArgumentNullException(nameof(outputs));
            if (outputs.Count == 0)
                throw new InvalidOperationException($"At least one Output call is required to return {names.OperationPastTense} rows.");

            // Each selector was written against its own parameter; the array lambda has one.
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

        /// <summary>
        ///     The <c>x =&gt; x.Key1 == v1 &amp;&amp; x.Key2 == v2</c> predicate that restricts an operation to
        ///     the keyed rows. Each key was written against its own parameter, so the field selectors
        ///     are rebound onto the one parameter this lambda has.
        /// </summary>
        public static Expression<Func<T, bool>> CreateKeyPredicate<T>(
            IReadOnlyList<FieldValuePair> keys,
            EntityFluentApiNames names)
        {
            if (keys is null)
                throw new ArgumentNullException(nameof(keys));
            if (keys.Count == 0)
                throw new InvalidOperationException($"At least one Key call is required to {names.OperationVerb} an entity.");

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

        /// <summary>Returns <paramref name="expression"/>'s body with its single parameter swapped for <paramref name="replacement"/>.</summary>
        public static Expression ReplaceParameter(LambdaExpression expression, ParameterExpression replacement)
        {
            if (expression.Parameters.Count != 1)
                throw new ArgumentException("A field selector must have exactly one parameter.", nameof(expression));
            return new ParameterReplacingVisitor(expression.Parameters[0], replacement).Visit(expression.Body);
        }

        /// <summary>The member a <c>x =&gt; x.LastName</c> style selector names, with any widening Convert removed.</summary>
        public static MemberInfo GetSelectedMember(LambdaExpression selector)
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

        /// <summary>
        ///     The type of a member the setter can write to. A member with no setter is not a stored
        ///     column — a calculated property is the usual way to arrive here.
        /// </summary>
        private static Type GetSettableMemberType(Type entityType, MemberInfo member, EntityFluentApiNames names)
        {
            switch (member)
            {
                case PropertyInfo property when property.CanWrite:
                    return property.PropertyType;
                case FieldInfo field:
                    return field.FieldType;
                default:
                    throw new InvalidOperationException(
                        $"'{entityType.Name}.{member.Name}' cannot be assigned by {names.SetterMethod}. A calculated or read-only member is not a stored column.");
            }
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
