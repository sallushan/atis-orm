using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    /// <summary>
    ///     Creates converters for <c>new DateTime(...)</c> built from integer parts: (year, month, day),
    ///     (year, month, day, hour, minute, second) and (year, month, day, hour, minute, second, millisecond).
    ///     The arguments may be constants, variables or columns.
    /// </summary>
    public sealed class NewDateTimeExpressionConverterFactory : LinqToSqlExpressionConverterFactoryBase<NewExpression>
    {
        /// <inheritdoc />
        public override bool TryCreate(
            IConverterDependencies converterDependencies,
            Expression expression,
            ExpressionConverterBase<Expression, SqlExpression>[] converterStack,
            out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is NewExpression newExpression && IsSupported(newExpression))
            {
                converter = new NewDateTimeExpressionConverter(
                    this.GetConverterDependencies(converterDependencies), newExpression, converterStack);
                return true;
            }

            converter = null;
            return false;
        }

        private static bool IsSupported(NewExpression expression)
        {
            if (expression.Type != typeof(DateTime) || expression.Constructor == null)
                return false;
            var count = expression.Arguments.Count;
            return (count == 3 || count == 6 || count == 7)
                && expression.Constructor.GetParameters().All(p => p.ParameterType == typeof(int));
        }
    }

    /// <summary>Converts <c>new DateTime(...)</c> to a dialect-neutral <see cref="SqlNewDateTimeExpression"/>.</summary>
    public sealed class NewDateTimeExpressionConverter : LinqToNonSqlQueryConverterBase<NewExpression>
    {
        internal NewDateTimeExpressionConverter(
            LinqToSqlExpressionConverterDependencies dependencies,
            NewExpression expression,
            ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(dependencies, expression, converterStack)
        {
        }

        /// <inheritdoc />
        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            var a = convertedChildren;
            return a.Length == 3
                ? this.SqlFactory.CreateNewDateTime(a[0], a[1], a[2])
                : this.SqlFactory.CreateNewDateTime(a[0], a[1], a[2], a[3], a[4], a[5], a.Length == 7 ? a[6] : null);
        }
    }
}
