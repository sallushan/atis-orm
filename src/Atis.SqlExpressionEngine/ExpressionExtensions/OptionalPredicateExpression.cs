using System;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.ExpressionExtensions
{
    /// <summary>
    ///     <para>
    ///         A predicate that is only part of the query when its <see cref="Guard"/> has a value. Produced by
    ///         the <see cref="WhereBuilder"/> preprocessor; carried through translation and finally rendered as
    ///         a span of SQL the renderer can skip.
    ///     </para>
    ///     <para>
    ///         <strong>Nothing here reads the guard's value.</strong> That is the whole point: preprocessing and
    ///         translation are value-blind, so the same compiled query serves an execution that supplies the
    ///         value and one that omits it. Deciding at preprocess time would bake the first caller's choice
    ///         into the cache entry, because the compiled-query cache is keyed on the <em>original</em>
    ///         expression and cannot see a preprocessor's branch.
    ///     </para>
    /// </summary>
    public class OptionalPredicateExpression : Expression
    {
        /// <summary>Creates an optional predicate.</summary>
        /// <param name="guard">
        ///     The value whose nullness decides whether <paramref name="predicate"/> appears. Normally the same
        ///     node that also supplies the value inside the predicate, so it converts to a parameter carrying
        ///     the variable's identity.
        /// </param>
        /// <param name="predicate">The predicate to emit when the guard has a value.</param>
        public OptionalPredicateExpression(Expression guard, Expression predicate)
        {
            this.Guard = guard ?? throw new ArgumentNullException(nameof(guard));
            this.Predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        }

        /// <inheritdoc />
        public override ExpressionType NodeType => ExpressionType.Extension;

        /// <inheritdoc />
        public sealed override Type Type => typeof(bool);

        /// <summary>The value whose nullness activates or drops <see cref="Predicate"/>.</summary>
        public Expression Guard { get; }

        /// <summary>The predicate emitted when the guard has a value.</summary>
        public Expression Predicate { get; }

        /// <inheritdoc />
        /// <remarks>
        ///     <para>
        ///         Both children must be visited. The guard is normally the same node instance that also occurs
        ///         inside the predicate, so it is visited twice - which is harmless, because values are
        ///         extracted and rebound by identity rather than by traversal position. Skipping the guard,
        ///         however, would hide it from a rewriting visitor and leave it pointing at a stale node.
        ///     </para>
        /// </remarks>
        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var updatedGuard = visitor.Visit(this.Guard);
            var updatedPredicate = visitor.Visit(this.Predicate);

            if (updatedGuard == this.Guard && updatedPredicate == this.Predicate)
                return this;

            return new OptionalPredicateExpression(updatedGuard, updatedPredicate);
        }

        /// <inheritdoc />
        public override string ToString() => $"{this.GetType().Name}({this.Guard}, {this.Predicate})";
    }
}
