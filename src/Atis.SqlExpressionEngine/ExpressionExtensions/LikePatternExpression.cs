using System;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.ExpressionExtensions
{
    /// <summary>
    ///     <para>
    ///         A <c>LIKE</c> whose pattern is used <em>verbatim</em>: the caller supplies the wildcards, so
    ///         nothing is prepended or appended.
    ///     </para>
    ///     <para>
    ///         The three wildcard-decorating forms already have BCL spellings that convert on their own -
    ///         <c>string.Contains</c>, <c>StartsWith</c> and <c>EndsWith</c> - but a raw pattern has none, which
    ///         is why it needs its own node. Produced by <see cref="WhereBuilder"/>'s preprocessor.
    ///     </para>
    /// </summary>
    public class LikePatternExpression : Expression
    {
        /// <summary>Creates a verbatim-pattern LIKE.</summary>
        public LikePatternExpression(Expression expression, Expression pattern)
        {
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
            this.Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
        }

        /// <inheritdoc />
        public override ExpressionType NodeType => ExpressionType.Extension;

        /// <inheritdoc />
        public sealed override Type Type => typeof(bool);

        /// <summary>The string expression being matched.</summary>
        public Expression Expression { get; }

        /// <summary>The pattern, including any wildcards, used exactly as given.</summary>
        public Expression Pattern { get; }

        /// <inheritdoc />
        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var updatedExpression = visitor.Visit(this.Expression);
            var updatedPattern = visitor.Visit(this.Pattern);

            if (updatedExpression == this.Expression && updatedPattern == this.Pattern)
                return this;

            return new LikePatternExpression(updatedExpression, updatedPattern);
        }

        /// <inheritdoc />
        public override string ToString() => $"{this.GetType().Name}({this.Expression}, {this.Pattern})";
    }
}
