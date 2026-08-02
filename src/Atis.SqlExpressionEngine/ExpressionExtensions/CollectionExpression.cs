using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Atis.SqlExpressionEngine.ExpressionExtensions
{
    /// <summary>
    ///     <para>
    ///         A list of expressions carried as one node, so that a custom node can hold a variable
    ///         number of children and still be visited and converted like any other. Converts to
    ///         <see cref="SqlExpressions.SqlCollectionExpression"/>.
    ///     </para>
    /// </summary>
    public class CollectionExpression : Expression
    {
        /// <summary>The expressions in this collection.</summary>
        public IReadOnlyList<Expression> Expressions { get; }

        /// <inheritdoc />
        public CollectionExpression(IReadOnlyList<Expression> expressions)
        {
            this.Expressions = expressions ?? throw new ArgumentNullException(nameof(expressions));
        }

        /// <summary>
        ///     The node is a container and never evaluates to a value of its own, so there is no
        ///     meaningful type to report. What distinguishes one collection from another — for cache
        ///     keys, for instance — is its children.
        /// </summary>
        public override Type Type => typeof(object);

        /// <inheritdoc />
        public override ExpressionType NodeType => ExpressionType.Extension;

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
                return new CollectionExpression(updatedExpressions);
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
