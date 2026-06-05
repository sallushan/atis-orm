using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.Internal;
using Atis.SqlExpressionEngine.Services;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine
{
    public class LinqToSqlConverter : ILinqToSqlConverter
    {
        private readonly IExpressionTreeConverter<Expression, SqlExpression> treeConverter;
        private readonly ISqlExpressionPostprocessorProvider postProcessorProvider;

        public LinqToSqlConverter(IExpressionTreeConverter<Expression, SqlExpression> treeConverter, ISqlExpressionPostprocessorProvider postProcessorProvider)
        {
            this.treeConverter = treeConverter;
            this.postProcessorProvider = postProcessorProvider;
        }

        /// <inheritdoc />
        public virtual SqlExpression Convert(Expression expression)
        {
            var visitor = new ExpressionConverterVisitor<Expression, SqlExpression>(treeConverter);
            var linqToSqlConverterInternal = new LinqToSqlConverterInternal(visitor);
            var sqlExpression = linqToSqlConverterInternal.Convert(expression);
            if (this.postProcessorProvider != null)
                sqlExpression = this.postProcessorProvider.Postprocess(sqlExpression);
            return sqlExpression;
        }

        private class LinqToSqlConverterInternal : ExpressionVisitor
        {
            private readonly ExpressionConverterVisitor<Expression, SqlExpression> visitor;

            public LinqToSqlConverterInternal(ExpressionConverterVisitor<Expression, SqlExpression> visitor)
            {
                this.visitor = visitor ?? throw new ArgumentNullException(nameof(visitor));
            }

            public SqlExpression Convert(Expression expression)
            {
                //this.visitor.Initialize();

                this.Visit(expression);

                var sqlExpression = this.visitor.GetConvertedExpression();

                return sqlExpression;
            }

            /// <inheritdoc />
            public sealed override Expression Visit(Expression node)
            {
                if (node is null) return node;

                var expr = this.visitor.Visit(node, base.Visit);

                return expr;
            }
        }
    }
}
