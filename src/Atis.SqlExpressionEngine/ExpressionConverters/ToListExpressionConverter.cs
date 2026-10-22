using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.ExpressionExtensions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    /// <summary>
    ///     <para>
    ///         Factory class for creating <see cref="ToListExpressionConverter"/> instances.
    ///     </para>
    /// </summary>
    public class ToListExpressionConverterFactory : LinqToSqlExpressionConverterFactoryBase<MethodCallExpression>
    {
        /// <inheritdoc />
        public override bool TryCreate(IConverterDependencies converterDependencies, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is MethodCallExpression methodCallExpression &&
                methodCallExpression.Method.DeclaringType == typeof(Enumerable) &&
                (methodCallExpression.Method.Name == nameof(Enumerable.ToList) || methodCallExpression.Method.Name == nameof(Enumerable.ToArray)) &&
                methodCallExpression.Arguments.Count == 1)
            {
                var dependencies = this.GetConverterDependencies(converterDependencies);
                converter = new ToListExpressionConverter(dependencies, methodCallExpression, converterStack);
                return true;
            }
            converter = null;
            return false;
        }
    }

    /// <summary>
    ///     <para>
    ///         Converter class for <c>ToList()</c> / <c>ToArray()</c> applied to a sub-query that is selected
    ///         in a projection, e.g. <c>Select(x =&gt; new { x.Id, Marks = x.NavMarks.ToList() })</c>.
    ///     </para>
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The call changes nothing in the SQL of the sub-query. It only changes the CLR type of the
    ///         projected member (a list instead of a lazy query), and that type is what tells the
    ///         materializer to load the sub-query as a separate query and fill the member with its rows.
    ///     </para>
    /// </remarks>
    public class ToListExpressionConverter : LinqToNonSqlQueryConverterBase<MethodCallExpression>
    {
        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="ToListExpressionConverter"/> class.
        ///     </para>
        /// </summary>
        public ToListExpressionConverter(LinqToSqlExpressionConverterDependencies dependencies, MethodCallExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(dependencies, expression, converterStack)
        {
        }

        /// <inheritdoc />
        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            return convertedChildren[0].CastTo<SqlDerivedTableExpression>();
        }
    }
}
