using System.Linq.Expressions;

using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.ExpressionExtensions;
using Atis.SqlExpressionEngine.SqlExpressions;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    /// <summary>
    ///     <para>
    ///         Factory class for creating converters that handle <see cref="NamedParameterExpression"/>.
    ///     </para>
    /// </summary>
    public class NamedParameterExpressionConverterFactory : LinqToSqlExpressionConverterFactoryBase<NamedParameterExpression>
    {
        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="NamedParameterExpressionConverterFactory"/> class.
        ///     </para>
        /// </summary>
        public NamedParameterExpressionConverterFactory() : base() { }

        /// <inheritdoc />
        public override bool TryCreate(IConverterDependencies converterDependencies, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is NamedParameterExpression namedParameter)
            {
                var d = this.GetConverterDependencies(converterDependencies);
                converter = new NamedParameterExpressionConverter(d, namedParameter, converterStack);
                return true;
            }
            converter = null;
            return false;
        }
    }

    /// <summary>
    ///     <para>
    ///         Converter class for handling <see cref="NamedParameterExpression"/>.
    ///     </para>
    ///     <para>
    ///         Produces exactly what
    ///         <see cref="VariableMemberExpressionConverter"/> produces for a captured local — a
    ///         <c>SqlParameterExpression</c> carrying the value and a stable identity — so a named parameter
    ///         and a captured local are indistinguishable from here on, including collection expansion.
    ///         The identity comes off the node itself rather than from
    ///         <see cref="IVariableIdentityProvider"/>, because a node built at runtime has no member path to
    ///         derive one from; the caller's name <em>is</em> the identity.
    ///     </para>
    /// </summary>
    public class NamedParameterExpressionConverter : LinqToNonSqlQueryConverterBase<NamedParameterExpression>
    {
        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="NamedParameterExpressionConverter"/> class.
        ///     </para>
        /// </summary>
        /// <param name="dependencies">The conversion dependencies.</param>
        /// <param name="expression">The named parameter expression to be converted.</param>
        /// <param name="converterStack">The stack of converters representing the parent chain for context-aware conversion.</param>
        public NamedParameterExpressionConverter(LinqToSqlExpressionConverterDependencies dependencies, NamedParameterExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(dependencies, expression, converterStack)
        {
        }

        /// <inheritdoc />
        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            var value = this.Expression.Value;
            return this.SqlFactory.CreateParameter(
                                        value,
                                        multipleValues: this.ReflectionService.IsEnumerable(value),
                                        identity: this.Expression.Identity,
                                        valueType: this.Expression.Type);
        }
    }
}
