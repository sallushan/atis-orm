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
        private List<Expression> parameterNodes = new List<Expression>();

        public ExpressionVariableValuesExtractor(IExpressionEvaluator expressionEvaluator)
        {
            this.expressionEvaluator = expressionEvaluator ?? throw new ArgumentNullException(nameof(expressionEvaluator));
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
