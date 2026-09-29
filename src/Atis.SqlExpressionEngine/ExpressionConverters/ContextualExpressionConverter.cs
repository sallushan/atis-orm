using System.Linq.Expressions;

using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.ExpressionExtensions;
using Atis.SqlExpressionEngine.SqlExpressions;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    /// <summary>
    ///     <para>
    ///         Factory class for creating converters that handle <see cref="ContextualExpression"/>.
    ///     </para>
    /// </summary>
    public class ContextualExpressionConverterFactory : LinqToSqlExpressionConverterFactoryBase<ContextualExpression>
    {
        /// <inheritdoc />
        public override bool TryCreate(IConverterDependencies converterDependencies, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is ContextualExpression contextual)
            {
                var d = this.GetConverterDependencies(converterDependencies);
                converter = new ContextualExpressionConverter(d, contextual, converterStack);
                return true;
            }
            converter = null;
            return false;
        }
    }

    /// <summary>
    ///     <para>
    ///         Converter class for handling <see cref="ContextualExpression"/>.
    ///     </para>
    ///     <para>
    ///         Produces a <c>SqlParameterExpression</c> that carries the node's key as its
    ///         <see cref="SqlParameterExpression.ContextKey"/> and no value: the value is asked for at
    ///         execution time, on the first run and on every cache hit alike.
    ///     </para>
    /// </summary>
    public class ContextualExpressionConverter : LinqToNonSqlQueryConverterBase<ContextualExpression>
    {
        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="ContextualExpressionConverter"/> class.
        ///     </para>
        /// </summary>
        public ContextualExpressionConverter(LinqToSqlExpressionConverterDependencies dependencies, ContextualExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(dependencies, expression, converterStack)
        {
        }

        /// <inheritdoc />
        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            return this.SqlFactory.CreateParameter(
                                        value: null,
                                        identity: this.Expression.Identity,
                                        valueType: this.Expression.Type,
                                        contextKey: this.Expression.Key);
        }
    }
}
