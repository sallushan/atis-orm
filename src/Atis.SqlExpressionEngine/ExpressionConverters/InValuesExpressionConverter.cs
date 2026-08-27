using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.ExpressionExtensions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    public class InValuesExpressionConverterFactory : LinqToSqlExpressionConverterFactoryBase<InValuesExpression>
    {
        public InValuesExpressionConverterFactory() : base() { }

        public override bool TryCreate(IConverterDependencies converterDependencies, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is InValuesExpression inExpr)
            {
                var dependencies = this.GetConverterDependencies(converterDependencies);
                converter = new InValuesExpressionConverter(dependencies, inExpr, converterStack);
                return true;
            }

            converter = null;
            return false;
        }
    }

    /// <summary>
    ///     <para>
    ///         Converter for `InValuesExpression` that converts to SQL `IN (...)` clause.
    ///     </para>
    /// </summary>
    public class InValuesExpressionConverter : LinqToNonSqlQueryConverterBase<InValuesExpression>
    {
        public InValuesExpressionConverter(LinqToSqlExpressionConverterDependencies context, InValuesExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(context, expression, converterStack)
        {
        }

        /// <inheritdoc/>
        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            // child[0] = converted Expression (e.g., x.Department)
            // child[1] = converted Values

            var values = convertedChildren[1];

            // An inline array with no elements would be `IN ()`, which no dialect accepts, and it cannot be
            // carried through the tree either (SqlCollectionExpression.Update rejects an empty collection, far
            // from here and with nothing pointing back to this query). Reject it where the cause is visible.
            // A collection *variable* has no such problem: it may legitimately be empty on some execution and
            // the renderer substitutes a value list that matches nothing.
            if (values is SqlCollectionExpression collection && !collection.SqlExpressions.Any())
                throw new InvalidOperationException(
                    $"An inline empty array cannot be used in '{this.Expression}': an IN list needs at least " +
                    $"one value. Use a collection variable instead - an empty one is handled at execution time.");

            // Otherwise the values node is passed through exactly as it was converted, never flattened or
            // folded. A captured collection arrives as one SqlParameterExpression and expands per execution;
            // an inline array arrives as a SqlCollectionExpression whose elements each keep their own nature,
            // so a variable used inside the array (new[] { "HR", dept }) stays reboundable instead of
            // freezing to the value it happened to hold when this query was first compiled.
            return this.SqlFactory.CreateInValuesExpression(convertedChildren[0], values);
        }
    }
}
