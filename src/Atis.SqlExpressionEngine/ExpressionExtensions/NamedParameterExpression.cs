using System;
using System.Linq.Expressions;

using Atis.SqlExpressionEngine.Abstractions;

namespace Atis.SqlExpressionEngine.ExpressionExtensions
{
    /// <summary>
    ///     <para>
    ///         A value placed directly into a hand-built expression tree, which the translation pipeline turns
    ///         into a SQL <em>parameter</em> rather than a literal. Create one through
    ///         <see cref="SqlParam.Create{T}(string, T)"/>.
    ///     </para>
    ///     <para>
    ///         Written with <c>Expression.Constant</c> instead, a value becomes a literal: it is folded into
    ///         the compiled-query cache key and frozen at translation time, so every distinct value compiles
    ///         and caches its own query and none of them can ever be rebound. Code that builds a tree by hand
    ///         — a dynamic filter, a reporting screen, a rules engine — has no captured local for the pipeline
    ///         to recognise instead, which is what this node supplies.
    ///     </para>
    ///     <para>
    ///         <strong>The value is deliberately absent from <see cref="StructuralKey"/>.</strong> That is the
    ///         whole feature: two trees identical apart from what their named parameters hold produce the same
    ///         cache key, share one compiled query, and each execution binds the value carried by the node it
    ///         was actually given.
    ///     </para>
    ///     <para>
    ///         <see cref="Name"/> is required because rebinding is by identity, never by position. The
    ///         translator walks the reshaped SqlExpression tree while values are re-extracted from the
    ///         original LINQ tree, and reshaping (CTE hoisting, subtree copying) can reorder or duplicate
    ///         parameters relative to LINQ order. A captured local derives its identity from its member path;
    ///         a node built at runtime has no member path, so the caller names it.
    ///     </para>
    /// </summary>
    public sealed class NamedParameterExpression : Expression, IStructuralKeyExpression
    {
        private readonly Type type;

        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="NamedParameterExpression"/> class.
        ///     </para>
        /// </summary>
        /// <param name="name">
        ///     Name identifying this parameter within the query. Two nodes sharing a name must hold equal
        ///     values, otherwise value re-extraction rejects the tree.
        /// </param>
        /// <param name="value">The value to bind. May be <c>null</c>.</param>
        /// <param name="type">
        ///     The declared type of the value. Required rather than inferred, because the value may be
        ///     <c>null</c> and because the type is part of the cache key.
        /// </param>
        public NamedParameterExpression(string name, object value, Type type)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A named parameter needs a non-blank name; the name is its identity for cache-hit rebinding.", nameof(name));
            this.Name = name;
            this.Value = value;
            this.type = type ?? throw new ArgumentNullException(nameof(type));
        }

        /// <summary>Gets the name identifying this parameter within the query.</summary>
        public string Name { get; }

        /// <summary>
        ///     <para>
        ///         Gets the value to bind for this execution.
        ///     </para>
        ///     <para>
        ///         Not part of <see cref="StructuralKey"/>, and never rendered by <see cref="ToString"/> — a
        ///         value reaching either of those would make the node's shape vary per execution.
        ///     </para>
        /// </summary>
        public object Value { get; }

        /// <summary>
        ///     <para>
        ///         Gets the identity under which this parameter's value is rebound on a cache hit.
        ///     </para>
        ///     <para>
        ///         The <c>named:</c> prefix keeps these apart from the identities the
        ///         <see cref="IVariableIdentityProvider"/> derives for captured locals, which are dotted
        ///         member paths rooted at a type name and so can never contain a colon.
        ///     </para>
        /// </summary>
        public string Identity => "named:" + this.Name;

        /// <inheritdoc />
        public object StructuralKey => this.Identity;

        /// <inheritdoc />
        public override ExpressionType NodeType => ExpressionType.Extension;

        /// <inheritdoc />
        public override Type Type => this.type;

        /// <inheritdoc />
        public override bool CanReduce => false;

        /// <inheritdoc />
        /// <remarks>The value is a plain field, not a child expression, so there is nothing to visit.</remarks>
        protected override Expression VisitChildren(ExpressionVisitor visitor) => this;

        /// <summary>
        ///     Renders the parameter by name only. The value must never appear here: identity computation
        ///     falls back to <see cref="object.ToString"/> for node shapes it does not recognise, and a
        ///     value-bearing string there would yield a fresh identity on every execution — every lookup
        ///     missing, and every parameter silently keeping the first run's value.
        /// </summary>
        public override string ToString() => "@" + this.Name;

        /// <summary>
        ///     Equality on shape — name and declared type — never on the value, matching
        ///     <see cref="StructuralKey"/>. Extension nodes compare by reference unless they override this,
        ///     and a node rebuilt on every call would never match its own cache entry.
        /// </summary>
        public override bool Equals(object obj)
        {
            var other = obj as NamedParameterExpression;
            return other != null && other.Name == this.Name && other.Type == this.Type;
        }

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(this.Name, this.Type);
    }
}
