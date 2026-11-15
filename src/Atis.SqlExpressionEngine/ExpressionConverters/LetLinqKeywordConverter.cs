using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    public class LetLinqKeywordConverterFactory : LinqToSqlExpressionConverterFactoryBase<MethodCallExpression>
    {
        public LetLinqKeywordConverterFactory() : base()
        {
        }

        /// <inheritdoc />
        public override bool TryCreate(IConverterDependencies converterDependencies, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (converterStack.Length > 0 &&
                expression is MethodCallExpression methodCall &&                // a method call
                methodCall.Method.Name == nameof(Queryable.Select) &&           // .Select
                (methodCall.Method.DeclaringType == typeof(Queryable) ||        // Select method should be from Queryable 
                methodCall.Method.DeclaringType == typeof(Enumerable)) &&       //  or Enumerable
                methodCall.Arguments.Count == 2 &&                              // .Select(arg0, arg1)
                methodCall.Arguments[1] is UnaryExpression ue &&                // arg0 must be Quote
                ue.Operand is LambdaExpression lambda &&                        // and Quote must be wrapping a Lambda
                lambda.Parameters.Count == 1 &&                                 // p1 => 
                lambda.Body is NewExpression newExpression &&                   // p1 => new { 
                newExpression.Arguments[0] == lambda.Parameters[0])             // p1 => new { p1, ....
            {
                var dependencies = this.GetConverterDependencies(converterDependencies);
                converter = new LetLinqKeywordConverter(dependencies, methodCall, converterStack);
                return true;
            }
            converter = null;
            return false;
        }
    }

    public class LetLinqKeywordConverter : QueryMethodExpressionConverterBase
    {
        public LetLinqKeywordConverter(LinqToSqlExpressionConverterDependencies dependencies, MethodCallExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converters) : base(dependencies, expression, converters)
        {
        }
        
        /// <inheritdoc />
        protected override SqlExpression Convert(SqlSelectExpression sqlQuery, SqlExpression[] arguments)
        {
            var compositeBinding = arguments[0].CastTo<SqlQueryShapeExpression>();
            // A let is only a name for an expression, and a later Where / Join / GroupBy simply writes that expression
            // again. A window function cannot be written there (it exists only in a SELECT list), so a let that computes
            // one is made a real Select instead, and the later clause then reads the derived column.
            if (ContainsWindowFunction(compositeBinding))
            {
                sqlQuery.ApplyProjection(compositeBinding);
                return sqlQuery;
            }
            sqlQuery.UpdateModelBinding(compositeBinding);
            // TODO: check if we need to mark the newly added binding as "non-projectable"
            return sqlQuery;
        }

        private static bool ContainsWindowFunction(SqlQueryShapeExpression shape)
        {
            var finder = new WindowFunctionFinder();
            finder.Visit(shape);
            return finder.Found;
        }

        private sealed class WindowFunctionFinder : Atis.SqlExpressionEngine.Visitors.SqlExpressionVisitor
        {
            public bool Found { get; private set; }

            protected internal override SqlExpression VisitSqlFunction(SqlFunctionExpression node)
            {
                if (node.IsWindowFunction)
                    this.Found = true;
                return base.VisitSqlFunction(node);
            }
        }
    }
}
