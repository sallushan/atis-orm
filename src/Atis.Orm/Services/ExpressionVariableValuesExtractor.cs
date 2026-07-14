using Atis.SqlExpressionEngine.Abstractions;
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
                values[i] = this.expressionEvaluator.Evaluate(nodes[i]);
            return values;
        }

        /// <inheritdoc />
        public IReadOnlyDictionary<string, object> ExtractVariableValuesByIdentity(Expression sqlExpression)
        {
            var nodes = this.ExtractParameterNodes(sqlExpression);
            var byIdentity = new Dictionary<string, object>();
            foreach (var node in nodes)
            {
                var identity = this.variableIdentityProvider.GetIdentity(node);
                var value = this.expressionEvaluator.Evaluate(node);
                if (byIdentity.TryGetValue(identity, out var existing))
                {
                    // Same variable referenced more than once -> identical value, keep the single entry.
                    // Different values under one identity would mean the identity failed to distinguish two
                    // captures; fail loudly rather than silently mis-bind.
                    if (!Equals(existing, value))
                        throw new InvalidOperationException(
                            $"Two distinct variables resolved to the same parameter identity '{identity}' with different values. " +
                            $"This would corrupt cache-hit rebinding.");
                    continue;
                }
                byIdentity.Add(identity, value);
            }
            return byIdentity;
        }

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

        private static bool IsQuerySourceType(Type type)
        {
            return typeof(IQueryable).IsAssignableFrom(type) || typeof(IQueryProvider).IsAssignableFrom(type);
        }
    }
}
