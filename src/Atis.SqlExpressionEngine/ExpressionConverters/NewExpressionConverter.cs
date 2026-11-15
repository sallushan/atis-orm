using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    /// <summary>
    ///     <para>
    ///         Factory for creating <see cref="NewExpressionConverter"/> instances.
    ///     </para>
    /// </summary>
    public class NewExpressionConverterFactory : LinqToSqlExpressionConverterFactoryBase<NewExpression>
    {
        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="NewExpressionConverterFactory"/> class.
        ///     </para>
        /// </summary>
        public NewExpressionConverterFactory() : base()
        {
        }

        /// <inheritdoc />
        public override bool TryCreate(IConverterDependencies converterDependencies, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is NewExpression newExpression)
            {
                var d = this.GetConverterDependencies(converterDependencies);
                converter = new NewExpressionConverter(d, newExpression, converterStack);
                return true;
            }
            converter = null;
            return false;
        }
    }

    /// <summary>
    ///     <para>
    ///         Converter for <see cref="NewExpression"/> instances to SQL expressions.
    ///     </para>
    /// </summary>
    public class NewExpressionConverter : CompositeMemberAssignmentConverterBase<NewExpression>
    {
        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="NewExpressionConverter"/> class.
        ///     </para>
        /// </summary>
        /// <param name="context">The conversion context.</param>
        /// <param name="expression">The source expression to be converted.</param>
        /// <param name="converterStack">The stack of converters representing the parent chain for context-aware conversion.</param>
        public NewExpressionConverter(LinqToSqlExpressionConverterDependencies context, NewExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(context, expression, converterStack)
        {
        }

        /// <summary>
        ///     <para>
        ///         The member each constructor argument feeds. An anonymous type carries these in
        ///         <see cref="NewExpression.Members"/>. A constructor call on any other type, such as a
        ///         positional record, does not, so each parameter is matched to a public property or field of
        ///         the same name (ignoring case) — which is exactly how a positional record defines its
        ///         properties. If any parameter has no unique match, the call cannot be a projection.
        ///     </para>
        /// </summary>
        protected override string[] GetMemberNames()
        {
            if (this.Expression.Members != null)
                return this.Expression.Members.Select(x => x.Name).ToArray();

            var constructor = this.Expression.Constructor;
            var parameters = constructor?.GetParameters();
            if (parameters is null || parameters.Length == 0)
                throw new InvalidOperationException($"Members of the new expression '{this.Expression}' are not set.");

            var names = new string[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                names[i] = FindMemberName(constructor.DeclaringType, parameters[i].Name)
                           ?? throw new InvalidOperationException(
                                $"Members of the new expression '{this.Expression}' are not set, and constructor parameter '{parameters[i].Name}' " +
                                $"has no public property or field named '{parameters[i].Name}' (ignoring case) on '{constructor.DeclaringType.Name}' to map it to.");
            }
            return names;
        }

        private static string FindMemberName(Type type, string parameterName)
        {
            var matches = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                              .Where(x => x.GetIndexParameters().Length == 0)
                              .Select(x => x.Name)
                              .Concat(type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Select(x => x.Name))
                              .Where(x => string.Equals(x, parameterName, StringComparison.OrdinalIgnoreCase))
                              .Distinct()
                              .ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        protected override SqlExpression[] GetSqlExpressions(SqlExpression[] convertedChildren)
        {
            return convertedChildren.Take(this.Expression.Arguments.Count).ToArray();
        }

        protected override Type GetExpressionType(int i)
        {
            return this.Expression.Arguments[i].Type;
        }
    }
}
