using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    /// <summary>
    ///     <para>
    ///         Factory for creating <see cref="QuoteExpressionConverter"/> instances.
    ///     </para>
    /// </summary>
    public class QuoteExpressionConverterFactory : LinqToSqlExpressionConverterFactoryBase<UnaryExpression>
    {
        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="QuoteExpressionConverterFactory"/> class.
        ///     </para>
        /// </summary>
        public QuoteExpressionConverterFactory() : base()
        {
        }

        /// <inheritdoc />
        public override bool TryCreate(IConverterDependencies converterDependencies, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is UnaryExpression unaryExpression && unaryExpression.NodeType == ExpressionType.Quote)
            {
                var d = this.GetConverterDependencies(converterDependencies);
                converter = new QuoteExpressionConverter(d, unaryExpression, converterStack);
                return true;
            }
            converter = null;
            return false;
        }
    }

    /// <summary>
    ///     <para>
    ///         Converter for handling <see cref="UnaryExpression"/> nodes with <see cref="ExpressionType.Quote"/>.
    ///     </para>
    /// </summary>
    public class QuoteExpressionConverter : LinqToNonSqlQueryConverterBase<UnaryExpression>
    {
        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="QuoteExpressionConverter"/> class.
        ///     </para>
        /// </summary>
        /// <param name="context">The conversion context.</param>
        /// <param name="expression">The source expression to be converted.</param>
        /// <param name="converterStack">The stack of converters representing the parent chain for context-aware conversion.</param>
        public QuoteExpressionConverter(LinqToSqlExpressionConverterDependencies context, UnaryExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(context, expression, converterStack)
        {
        }

        /// <inheritdoc />
        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            return convertedChildren[0];
        }
    }
}
