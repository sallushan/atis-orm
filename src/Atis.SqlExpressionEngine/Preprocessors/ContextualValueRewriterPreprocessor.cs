using System;
using System.Linq.Expressions;

using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.ExpressionExtensions;

namespace Atis.SqlExpressionEngine.Preprocessors
{
    /// <summary>
    ///     <para>
    ///         Rewrites the two ways a query names a context value — a member the <see cref="IContextualMemberProvider"/> reports
    ///         as contextual (<see cref="IContextualMemberProvider.TryGetContextualKey"/>), and a
    ///         <see cref="SqlContext.Get{T}(string)"/> call — into <see cref="ContextualExpression"/>.
    ///     </para>
    ///     <para>
    ///         Shape-deterministic by construction: the result depends on the member or the key literal, never
    ///         on any value. That is also why the key must be a literal string. Model-agnostic, like the
    ///         navigation preprocessors: how a member gets marked is the model's business.
    ///     </para>
    /// </summary>
    public class ContextualValueRewriterPreprocessor : ExpressionVisitor, IExpressionPreprocessor
    {
        private readonly IContextualMemberProvider model;

        /// <summary>Initializes a new instance of the <see cref="ContextualValueRewriterPreprocessor"/> class.</summary>
        public ContextualValueRewriterPreprocessor(IContextualMemberProvider model)
        {
            this.model = model ?? throw new ArgumentNullException(nameof(model));
        }

        /// <inheritdoc />
        public Expression Preprocess(Expression node) => this.Visit(node);

        /// <inheritdoc />
        public void Initialize()
        {
        }

        /// <inheritdoc />
        protected override Expression VisitMember(MemberExpression node)
        {
            if (this.model.TryGetContextualKey(node.Member, out var key))
                return new ContextualExpression(key, node.Type);
            return base.VisitMember(node);
        }

        /// <inheritdoc />
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(SqlContext) && node.Method.Name == nameof(SqlContext.Get))
            {
                if (!(node.Arguments[0] is ConstantExpression constant) || !(constant.Value is string key))
                    throw new InvalidOperationException(
                        $"The key passed to {nameof(SqlContext)}.{nameof(SqlContext.Get)} must be written as a literal string, " +
                        $"but was '{node.Arguments[0]}'. The key is part of the query's shape; a key read from a variable " +
                        $"would be frozen to the first execution's value.");
                return new ContextualExpression(key, node.Type);
            }
            return base.VisitMethodCall(node);
        }
    }
}
