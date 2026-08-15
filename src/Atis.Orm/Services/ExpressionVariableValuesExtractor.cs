using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.ExpressionExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using Atis.Orm.Abstractions;
namespace Atis.Orm.Services
{
    /// <summary>
    ///     <para>
    ///         Extracts the runtime values of the <em>variable</em> (parameter) nodes from a LINQ expression,
    ///         in the order they are visited.
    ///     </para>
    ///     <para>
    ///         Only nodes that the translation pipeline turns into a <c>SqlParameterExpression</c> are collected,
    ///         i.e. variable member accesses (captured locals / static members) as classified by
    ///         <see cref="IExpressionEvaluator.IsVariable(Expression)"/>. Inline / injected constants become
    ///         literals (<c>SqlLiteralExpression</c>, <see cref="IQueryParameter.IsLiteral"/>) whose value is fixed
    ///         at translation time and must never be re-extracted, so they are deliberately skipped here. Query-typed
    ///         members (e.g. a <c>context.Employees</c> root) are sources, not parameters, and are only removed by
    ///         preprocessing, so they are excluded here too for the skip path that runs over the original expression.
    ///     </para>
    ///     <para>
    ///         A <see cref="NamedParameterExpression"/> is collected as well: it is the hand-built-tree
    ///         equivalent of a captured local, and becomes a <c>SqlParameterExpression</c> the same way. It
    ///         supplies its own identity and value, so neither <see cref="IVariableIdentityProvider"/> nor
    ///         <see cref="IExpressionEvaluator"/> is consulted for it.
    ///     </para>
    /// </summary>
    public class ExpressionVariableValuesExtractor : ExpressionVisitor, IExpressionVariableValuesExtractor
    {
        private readonly IExpressionEvaluator expressionEvaluator;
        private readonly IVariableIdentityProvider variableIdentityProvider;
        private List<Expression> parameterNodes = new List<Expression>();

        public ExpressionVariableValuesExtractor(IExpressionEvaluator expressionEvaluator, IVariableIdentityProvider variableIdentityProvider)
        {
            this.expressionEvaluator = expressionEvaluator ?? throw new ArgumentNullException(nameof(expressionEvaluator));
            this.variableIdentityProvider = variableIdentityProvider ?? throw new ArgumentNullException(nameof(variableIdentityProvider));
        }

        /// <inheritdoc />
        public IReadOnlyList<Expression> ExtractParameterNodes(Expression sqlExpression)
        {
            this.parameterNodes = new List<Expression>();
            this.Visit(sqlExpression);
            return this.parameterNodes;
        }

        /// <inheritdoc />
        public IReadOnlyList<object> ExtractVariableValues(Expression sqlExpression)
        {
            var nodes = this.ExtractParameterNodes(sqlExpression);
            var values = new object[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
                values[i] = this.ValueOf(nodes[i]);
            return values;
        }

        /// <inheritdoc />
        public IReadOnlyDictionary<string, object> ExtractVariableValuesByIdentity(Expression sqlExpression)
        {
            var nodes = this.ExtractParameterNodes(sqlExpression);
            var byIdentity = new Dictionary<string, object>();
            foreach (var node in nodes)
            {
                var identity = this.IdentityOf(node);
                var value = this.ValueOf(node);
                if (byIdentity.TryGetValue(identity, out var existing))
                {
                    // Same variable referenced more than once -> identical value, keep the single entry.
                    // Different values under one identity would mean the identity failed to distinguish two
                    // captures; fail loudly rather than silently mis-bind.
                    if (!Equals(existing, value))
                        throw new InvalidOperationException(
                            node is NamedParameterExpression named
                                // Normally unreachable: NamedParameterValidator rejects this at compile time,
                                // on the first run. Kept as a backstop for a tree that reaches execution
                                // without having been compiled through QueryCompiler.
                                ? NamedParameterValidator.DuplicateNameMessage(named.Name, existing, value)
                                : $"Two distinct variables resolved to the same parameter identity '{identity}' with different values. " +
                                  $"This would corrupt cache-hit rebinding.");
                    continue;
                }
                byIdentity.Add(identity, value);
            }
            return byIdentity;
        }

        /// <summary>
        ///     The identity a collected node's value is rebound under. A named parameter carries its own; a
        ///     variable member access has one derived from its member path.
        /// </summary>
        private string IdentityOf(Expression node)
            => node is NamedParameterExpression named
                    ? named.Identity
                    : this.variableIdentityProvider.GetIdentity(node);

        /// <summary>
        ///     The current value of a collected node. A named parameter holds its own; a variable member
        ///     access is evaluated through its container.
        /// </summary>
        private object ValueOf(Expression node)
            => node is NamedParameterExpression named
                    ? named.Value
                    : this.expressionEvaluator.Evaluate(node);

        protected override Expression VisitMember(MemberExpression node)
        {
            if (this.expressionEvaluator.IsVariable(node))
            {
                // The whole member access evaluates to a value, so stop traversing into the access chain
                // (its children are the closure / root container, never parameters) — this mirrors
                // VariableMemberExpressionConverter.TryOverrideChildConversion. A query-typed member is a
                // source root, not a parameter, so it is skipped (but traversal still stops here).
                if (!IsQuerySourceType(node.Type))
                    this.parameterNodes.Add(node);
                return node;
            }
            return base.VisitMember(node);
        }

        /// <inheritdoc />
        protected override Expression VisitExtension(Expression node)
        {
            if (node is NamedParameterExpression)
            {
                // The value is a plain field, not a child expression, so there is nothing below to visit.
                this.parameterNodes.Add(node);
                return node;
            }
            return base.VisitExtension(node);
        }

        private static bool IsQuerySourceType(Type type)
        {
            return typeof(IQueryable).IsAssignableFrom(type) || typeof(IQueryProvider).IsAssignableFrom(type);
        }
    }
}
