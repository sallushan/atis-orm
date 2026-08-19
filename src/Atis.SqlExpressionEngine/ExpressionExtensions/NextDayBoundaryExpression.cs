using System;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.ExpressionExtensions
{
    /// <summary>
    ///     <para>
    ///         Midnight at the start of the day <em>after</em> the given date -
    ///         <c>DATEADD(day, 1, CAST(value AS date))</c>. Used as the exclusive upper bound of an inclusive
    ///         date range, so <c>column &lt; NextDayBoundary(@to)</c> matches every row on <c>@to</c> itself,
    ///         whatever its time of day.
    ///     </para>
    ///     <para>
    ///         <strong>It exists so the arithmetic happens in SQL rather than in C#.</strong> Doing it on the
    ///         client would replace the caller's variable with a computed one, and a computed node has a
    ///         different variable identity from the node the original expression carries - so on a cache hit
    ///         there would be nothing to rebind against and the bound would silently freeze to the first
    ///         execution's date. Keeping <see cref="Value"/> as the caller's own node preserves that identity.
    ///     </para>
    /// </summary>
    public class NextDayBoundaryExpression : Expression
    {
        /// <summary>Creates a next-midnight boundary over <paramref name="value"/>.</summary>
        public NextDayBoundaryExpression(Expression value)
        {
            this.Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <inheritdoc />
        public override ExpressionType NodeType => ExpressionType.Extension;

        /// <inheritdoc />
        public sealed override Type Type => typeof(DateTime?);

        /// <summary>The date to take the following midnight of. Kept as the caller's own node.</summary>
        public Expression Value { get; }

        /// <inheritdoc />
        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var updatedValue = visitor.Visit(this.Value);
            return updatedValue == this.Value ? this : new NextDayBoundaryExpression(updatedValue);
        }

        /// <inheritdoc />
        public override string ToString() => $"{this.GetType().Name}({this.Value})";
    }
}
