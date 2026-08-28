using System;
using System.Linq.Expressions;

using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.ExpressionExtensions;
using Atis.SqlExpressionEngine.SqlExpressions;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    /// <summary>
    ///     <para>Creates the converter for <see cref="DelimitedValuesExpression"/>.</para>
    /// </summary>
    public class DelimitedValuesExpressionConverterFactory : LinqToSqlExpressionConverterFactoryBase<DelimitedValuesExpression>
    {
        /// <summary>Creates the factory.</summary>
        public DelimitedValuesExpressionConverterFactory() : base() { }

        /// <inheritdoc />
        public override bool TryCreate(IConverterDependencies converterDependencies, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is DelimitedValuesExpression delimitedValues)
            {
                var dependencies = this.GetConverterDependencies(converterDependencies);
                converter = new DelimitedValuesExpressionConverter(dependencies, delimitedValues, converterStack);
                return true;
            }

            converter = null;
            return false;
        }
    }

    /// <summary>
    ///     <para>
    ///         Converts <see cref="DelimitedValuesExpression"/> to <see cref="SqlDelimitedValuesExpression"/>.
    ///     </para>
    ///     <para>
    ///         The string converts like any other child, so a captured variable becomes a
    ///         <see cref="SqlParameterExpression"/> carrying its identity - and it is kept whole. Splitting it
    ///         here would need its value, which is not knowable at conversion: the same compiled query is
    ///         re-executed with strings holding different numbers of values.
    ///     </para>
    /// </summary>
    public class DelimitedValuesExpressionConverter : LinqToNonSqlQueryConverterBase<DelimitedValuesExpression>
    {
        /// <summary>Creates the converter.</summary>
        public DelimitedValuesExpressionConverter(LinqToSqlExpressionConverterDependencies context, DelimitedValuesExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(context, expression, converterStack)
        {
        }

        /// <inheritdoc/>
        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            var values = convertedChildren[0];

            // A column cannot be read as a value list: the entries decide how many placeholders the statement
            // has, which is settled once per execution, before any row is read.
            if (!(values is SqlParameterExpression) && !(values is SqlLiteralExpression))
                throw new InvalidOperationException(
                    $"{nameof(WhereBuilder)}.{nameof(WhereBuilder.Delimited)} needs a value - a captured " +
                    $"variable or a constant string - but its argument translated to " +
                    $"'{values.GetType().Name}'. A column cannot supply the list, because the number of values " +
                    $"decides the shape of the statement itself.");

            return this.SqlFactory.CreateDelimitedValues(values, this.Expression.Delimiter);
        }
    }
}
