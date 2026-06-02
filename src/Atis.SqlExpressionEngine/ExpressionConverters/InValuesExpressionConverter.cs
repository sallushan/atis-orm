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
            // child[1] = converted Values (e.g., variable array)

            var leftSide = convertedChildren[0];
            SqlExpression[] values;
            if (convertedChildren[1] is SqlCollectionExpression collection)
                values = collection.SqlExpressions.ToArray();
            else
                values = new[] { convertedChildren[1] };

            return this.SqlFactory.CreateInValuesExpression(leftSide, values);
        }
    }
}
