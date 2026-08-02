using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Atis.SqlExpressionEngine.ExpressionExtensions
{
    public class AggregatedListExpression : Expression
    {
        public IReadOnlyList<Expression> Expressions { get; }

        public AggregatedListExpression(IReadOnlyList<Expression> expressions)
        {
            this.Expressions = expressions;
        }

        public override Type Type => typeof(object);

        public override ExpressionType NodeType =>  ExpressionType.Extension;

        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var updatedExpressions = new List<Expression>();
            foreach (var expression in this.Expressions)
            {
                var updatedExpression = visitor.Visit(expression);
                updatedExpressions.Add(updatedExpression);
            }
            if (!Enumerable.SequenceEqual(updatedExpressions, this.Expressions))
            {
                return new AggregatedListExpression(updatedExpressions);
            }
            return this;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            string expressionsToString = "";
            if (this.Expressions != null)
            {
                expressionsToString = string.Join(", ", this.Expressions);
            }
            return $"[{expressionsToString}]";
        }
    }
}
