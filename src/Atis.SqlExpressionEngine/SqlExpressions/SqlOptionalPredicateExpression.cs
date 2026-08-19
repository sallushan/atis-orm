using System;

using Atis.SqlExpressionEngine.Visitors;

namespace Atis.SqlExpressionEngine.SqlExpressions
{
    /// <summary>
    ///     <para>
    ///         SQL-side counterpart of
    ///         <see cref="ExpressionExtensions.OptionalPredicateExpression"/>: a predicate that the renderer
    ///         emits only when <see cref="Guard"/>'s value is not null at execution time.
    ///     </para>
    ///     <para>
    ///         The translator emits this as a self-anchored group - <c>(1 = 1</c>, then a skippable span
    ///         holding <c>AND &lt;predicate&gt;</c>, then <c>)</c>. Anchoring each term on its own means an
    ///         optional term is valid boolean SQL whether or not it is dropped, so it composes with the
    ///         surrounding predicate through the ordinary binary translation and needs no separator
    ///         bookkeeping.
    ///     </para>
    /// </summary>
    public class SqlOptionalPredicateExpression : SqlExpression
    {
        /// <summary>Creates an optional predicate node.</summary>
        public SqlOptionalPredicateExpression(SqlExpression guard, SqlExpression predicate)
        {
            this.Guard = guard ?? throw new ArgumentNullException(nameof(guard));
            this.Predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        }

        /// <summary>The value whose nullness activates or drops <see cref="Predicate"/>.</summary>
        public SqlExpression Guard { get; }

        /// <summary>The predicate emitted when the guard has a value.</summary>
        public SqlExpression Predicate { get; }

        /// <inheritdoc />
        public override SqlExpressionType NodeType => SqlExpressionType.OptionalPredicate;

        /// <inheritdoc />
        protected internal override SqlExpression Accept(SqlExpressionVisitor sqlExpressionVisitor)
        {
            return sqlExpressionVisitor.VisitOptionalPredicate(this);
        }

        /// <summary>Returns this node, or a new one when a child changed.</summary>
        public SqlExpression Update(SqlExpression guard, SqlExpression predicate)
        {
            if (guard == this.Guard && predicate == this.Predicate)
                return this;
            return new SqlOptionalPredicateExpression(guard, predicate);
        }

        /// <inheritdoc />
        public override string ToString() => $"optional({this.Guard}) {this.Predicate}";
    }
}
