using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    /// <summary>
    ///     <para>
    ///         Factory class for creating converters that handle <see cref="QueryExtensions.SelectField{TKey, TRow, TField}"/>.
    ///     </para>
    /// </summary>
    public class GroupFieldConverterFactory : LinqToSqlExpressionConverterFactoryBase<MethodCallExpression>
    {
        /// <inheritdoc />
        public override bool TryCreate(IConverterDependencies converterDependencies, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is MethodCallExpression call
                && call.Method.DeclaringType == typeof(QueryExtensions)
                && call.Method.Name == nameof(QueryExtensions.SelectField))
            {
                converter = new GroupFieldConverter(this.GetConverterDependencies(converterDependencies), call, converterStack);
                return true;
            }
            converter = null;
            return false;
        }
    }

    /// <summary>
    ///     <para>
    ///         Turns <c>g.SelectField(y =&gt; expr)</c> into the converted <c>expr</c>, with <c>y</c> standing for a row of
    ///         the group. The enclosing SQL function then receives it as an ordinary argument.
    ///     </para>
    /// </summary>
    public class GroupFieldConverter : LinqToNonSqlQueryConverterBase<MethodCallExpression>
    {
        /// <summary>Initializes a new instance of the <see cref="GroupFieldConverter"/> class.</summary>
        /// <param name="dependencies">The conversion dependencies.</param>
        /// <param name="expression">The method call expression to be converted.</param>
        /// <param name="converterStack">The stack of converters representing the parent chain for context-aware conversion.</param>
        public GroupFieldConverter(LinqToSqlExpressionConverterDependencies dependencies, MethodCallExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(dependencies, expression, converterStack)
        {
        }

        /// <inheritdoc />
        public override void OnConversionCompletedByChild(ExpressionConverterBase<Expression, SqlExpression> childConverter, Expression childNode, SqlExpression convertedExpression)
        {
            if (this.Expression.Arguments[0] != childNode)
                return;
            if (!(convertedExpression is SqlSelectExpression groupQuery))
                throw new InvalidOperationException(
                    $"'{nameof(QueryExtensions.SelectField)}' can only be used on the group of a GroupBy query " +
                    $"(the 'g' in GroupBy(..).Select(g => ..)), but got {convertedExpression.GetType().Name}.");
            if (this.Expression.TryGetArgLambdaParameter(argIndex: 1, paramIndex: 0, out var rowParameter))
                this.MapParameter(rowParameter, () => groupQuery.GetQueryShapeForFieldMapping());
        }

        /// <inheritdoc />
        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            // [0] is the group (not part of the output), [1] is the converted selector body.
            return convertedChildren[1];
        }
    }
}
