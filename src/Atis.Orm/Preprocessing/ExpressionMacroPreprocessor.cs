using Atis.Expressions;
using Atis.Orm.Annotations;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Atis.Orm.Preprocessing
{
    /// <summary>
    ///     <para>
    ///         Inlines calls to members marked with <see cref="ExpressionMacroAttribute"/>: the call is replaced
    ///         by the body of the named lambda, with the call's arguments substituted for its parameters.
    ///     </para>
    ///     <para>
    ///         This must stay a preprocessor, not a converter. The inlined body is ordinary user code and may
    ///         contain navigations, calculated properties or further macro calls, which only get
    ///         expanded if the body is back in the tree before the other preprocessors run again.
    ///     </para>
    ///     <para>
    ///         Shape-determinism: the lambda is a fixed static, so the same call always inlines the same body.
    ///     </para>
    /// </summary>
    public class ExpressionMacroPreprocessor : ExpressionVisitor, IExpressionPreprocessor
    {
        private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        /// <inheritdoc />
        public void Initialize()
        {
            // do nothing
        }

        /// <inheritdoc />
        public Expression Preprocess(Expression expression)
        {
            return this.Visit(expression);
        }

        /// <inheritdoc />
        protected override Expression VisitInvocation(InvocationExpression node)
        {
            // a Func<> field: macro(args) is compiled as Invoke(field, args)
            if (node.Expression is MemberExpression memberExpression)
            {
                var inlined = TryInline(memberExpression.Member, node.Arguments);
                if (inlined != null)
                    return inlined;
            }
            return base.VisitInvocation(node);
        }

        /// <inheritdoc />
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var inlined = TryInline(node.Method, node.Arguments);
            if (inlined != null)
                return inlined;
            return base.VisitMethodCall(node);
        }

        private static Expression TryInline(MemberInfo marked, System.Collections.Generic.IReadOnlyList<Expression> arguments)
        {
            var attribute = marked.GetCustomAttribute<ExpressionMacroAttribute>();
            if (attribute == null)
                return null;

            var lambda = GetLambda(marked, attribute.ExpressionMember);
            if (lambda.Parameters.Count != arguments.Count)
                throw new InvalidOperationException($"The number of arguments does not match the number of parameters in the lambda expression for {attribute.ExpressionMember}.");

            return ExpressionReplacementVisitor.Replace(lambda.Parameters, arguments, lambda.Body);
        }

        private static LambdaExpression GetLambda(MemberInfo marked, string name)
        {
            var type = marked.DeclaringType;
            object value;
            var field = type?.GetField(name, StaticMembers);
            if (field != null)
                value = field.GetValue(null);
            else
            {
                var property = type?.GetProperty(name, StaticMembers);
                if (property == null)
                    throw new InvalidOperationException($"{type?.FullName}.{marked.Name} is marked [ExpressionMacro(\"{name}\")] but no static field or property named '{name}' exists on that class.");
                value = property.GetValue(null);
            }

            if (value is LambdaExpression lambda)
                return lambda;
            throw new InvalidOperationException($"{type.FullName}.{name} must hold a LambdaExpression (Expression<Func<..>>) for [ExpressionMacro] on {marked.Name}.");
        }
    }
}
