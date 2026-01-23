using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Atis.SqlExpressionEngine.ExpressionExtensions
{
    public sealed class QueryRootExpression : Expression
    {
        public QueryRootExpression(Type entityType)
        {
            EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
            Type = typeof(IQueryable<>).MakeGenericType(EntityType);
        }

        public Type EntityType { get; }

        /// <inheritdoc />
        public override ExpressionType NodeType => ExpressionType.Extension;

        /// <inheritdoc />
        public override Type Type { get; }

        /// <inheritdoc />
        public override bool CanReduce => false;

        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            return this;
        }
    }
}
