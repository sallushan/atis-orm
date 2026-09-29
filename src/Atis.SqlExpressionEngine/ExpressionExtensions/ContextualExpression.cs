using System;
using System.Linq.Expressions;

using Atis.SqlExpressionEngine.Abstractions;

namespace Atis.SqlExpressionEngine.ExpressionExtensions
{
    /// <summary>
    ///     <para>
    ///         A value that comes from the ambient context of the execution — the logged-in user, the current
    ///         tenant — rather than from the query text or from a captured local. The translation pipeline turns
    ///         it into a SQL <em>parameter</em> whose value is asked for, by <see cref="Key"/>, every time the
    ///         query runs. Create one through <see cref="SqlContext.Get{T}(string)"/>.
    ///     </para>
    ///     <para>
    ///         The node holds no value at all, so there is nothing to freeze and nothing to re-extract from the
    ///         tree. That is what makes it safe for a preprocessor to inject one: a node that appears only
    ///         after preprocessing is invisible to cache-hit value extraction, yet it still gets a fresh value
    ///         on every execution.
    ///     </para>
    ///     <para>
    ///         Only <see cref="Key"/> and <see cref="Type"/> are part of the cache key. Two users running the
    ///         same query share one compiled query and each gets their own value.
    ///     </para>
    /// </summary>
    public class ContextualExpression : Expression, IStructuralKeyExpression
    {
        private readonly Type type;

        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="ContextualExpression"/> class.
        ///     </para>
        /// </summary>
        /// <param name="key">Name of the context value, for example <c>"CurrentUserId"</c>.</param>
        /// <param name="type">
        ///     The declared type of the value. Required rather than inferred because the value is not known
        ///     until execution, and because the type is part of the cache key.
        /// </param>
        public ContextualExpression(string key, Type type)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("A contextual value needs a non-blank key; the key is how its value is found at execution time.", nameof(key));
            this.Key = key;
            this.type = type ?? throw new ArgumentNullException(nameof(type));
        }

        /// <summary>Gets the name of the context value.</summary>
        public string Key { get; }

        /// <summary>
        ///     Gets the identity of the parameter this node becomes. The <c>context:</c> prefix keeps it apart
        ///     from captured-local identities (dotted member paths, never a colon) and from
        ///     <see cref="NamedParameterExpression"/> identities (<c>named:</c>).
        /// </summary>
        public string Identity => "context:" + this.Key;

        /// <inheritdoc />
        public object StructuralKey => this.Identity;

        /// <inheritdoc />
        public override ExpressionType NodeType => ExpressionType.Extension;

        /// <inheritdoc />
        public override Type Type => this.type;

        /// <inheritdoc />
        public override bool CanReduce => false;

        /// <inheritdoc />
        protected override Expression VisitChildren(ExpressionVisitor visitor) => this;

        /// <inheritdoc />
        public override string ToString() => "context(" + this.Key + ")";

        /// <summary>Equality on shape — key and declared type — matching <see cref="StructuralKey"/>.</summary>
        public override bool Equals(object obj)
        {
            var other = obj as ContextualExpression;
            return other != null && other.Key == this.Key && other.Type == this.Type;
        }

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(this.Key, this.Type);
    }
}
