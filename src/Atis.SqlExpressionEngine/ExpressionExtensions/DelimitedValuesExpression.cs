using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.ExpressionExtensions
{
    /// <summary>
    ///     <para>
    ///         A collection of values written as one delimited string ("HR,IT,Finance"). Produced by
    ///         <see cref="WhereBuilder"/>'s preprocessor for <see cref="WhereBuilder.Delimited"/>, and only ever
    ///         placed where a collection goes - the values of <c>In</c>, <c>NotIn</c> or one of the
    ///         <c>...Any</c> methods.
    ///     </para>
    ///     <para>
    ///         It is a node rather than a rewrite because the string cannot be split here: how many values it
    ///         holds is a property of the value, and this preprocessor never reads a value. The node carries the
    ///         delimiter through translation so the renderer can split the string once per execution - which is
    ///         what keeps one compiled query serving lists of every length, including none.
    ///     </para>
    /// </summary>
    public class DelimitedValuesExpression : Expression
    {
        /// <summary>Creates a delimited value list.</summary>
        /// <param name="values">The string expression holding the whole list.</param>
        /// <param name="delimiter">The text separating one value from the next.</param>
        /// <exception cref="ArgumentNullException">Either argument is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="delimiter"/> is empty.</exception>
        public DelimitedValuesExpression(Expression values, string delimiter)
        {
            this.Values = values ?? throw new ArgumentNullException(nameof(values));
            this.Delimiter = delimiter ?? throw new ArgumentNullException(nameof(delimiter));
            if (delimiter.Length == 0)
                throw new ArgumentException("The delimiter must not be empty.", nameof(delimiter));
        }

        /// <inheritdoc />
        public override ExpressionType NodeType => ExpressionType.Extension;

        /// <inheritdoc />
        public sealed override Type Type => typeof(IEnumerable<string>);

        /// <summary>The string expression holding the whole list.</summary>
        public Expression Values { get; }

        /// <summary>
        ///     The text separating one value from the next. A string rather than a character, so a line break
        ///     or any other multi-character separator can be used.
        /// </summary>
        public string Delimiter { get; }

        /// <inheritdoc />
        /// <remarks>
        ///     <para>
        ///         <see cref="Values"/> must be visited, and must stay the caller's own node: it carries the
        ///         variable identity the string is rebound by on a cache hit, and it is also what the
        ///         surrounding optional term tests as its guard.
        ///     </para>
        /// </remarks>
        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var updatedValues = visitor.Visit(this.Values);

            if (updatedValues == this.Values)
                return this;

            return new DelimitedValuesExpression(updatedValues, this.Delimiter);
        }

        /// <inheritdoc />
        public override string ToString() => $"{this.GetType().Name}('{this.Delimiter}', {this.Values})";
    }
}
