using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    /// <summary>
    ///     <para>
    ///         Factory for creating converters that transform conditional expressions from LINQ to SQL.
    ///     </para>
    /// </summary>
    public class ConditionalExpressionConverterFactory : LinqToSqlExpressionConverterFactoryBase<ConditionalExpression>
    {
        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="ConditionalExpressionConverterFactory"/> class.
        ///     </para>
        /// </summary>
        public ConditionalExpressionConverterFactory() : base()
        {
        }

        /// <inheritdoc/>
        public override bool TryCreate(IConverterDependencies converterDependencies, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is ConditionalExpression conditionExpression)
            {
                var d = this.GetConverterDependencies(converterDependencies);
                converter = new ConditionalExpressionConverter(d, conditionExpression, converterStack);
                return true;
            }
            converter = null;
            return false;
        }
    }

    /// <summary>
    ///     <para>
    ///         Converter for transforming conditional expressions from LINQ to SQL.
    ///     </para>
    /// </summary>
    public class ConditionalExpressionConverter : LinqToNonSqlQueryConverterBase<ConditionalExpression>
    {
        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="ConditionalExpressionConverter"/> class.
        ///     </para>
        /// </summary>
        /// <param name="converterDependencies">The conversion dependencies.</param>
        /// <param name="expression">The conditional expression to be converted.</param>
        /// <param name="converterStack">The stack of converters representing the parent chain for context-aware conversion.</param>
        public ConditionalExpressionConverter(LinqToSqlExpressionConverterDependencies converterDependencies, ConditionalExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(converterDependencies, expression, converterStack)
        {
        }

        /// <inheritdoc />
        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            var test = convertedChildren[0];
            var ifTrue = convertedChildren[1];
            var ifFalse = convertedChildren[2];
            var result = this.SqlFactory.CreateCondition(test, ifTrue, ifFalse);
            return result;
        }
    }
}
