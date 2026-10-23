using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Atis.Orm.Services
{
    /// <summary>
    ///     <para>
    ///         Rejects a query that loads a child collection (<c>x.Shoes.ToList()</c> or <c>ToArray()</c> on a
    ///         sub-query inside a <c>Select</c>) anywhere but the last <c>Select</c> of the query.
    ///     </para>
    ///     <para>
    ///         A child collection is part of the shape of the result: each returned row carries its own list.
    ///         Written in an earlier <c>Select</c> and followed by <c>Where</c>, <c>OrderBy</c>, <c>Join</c> or
    ///         <c>Take</c>, it reads as if the lists were loaded before those steps ran. They are not, and the
    ///         reader cannot tell. So the position is fixed: the last <c>Select</c>, optionally followed by a
    ///         terminal such as <c>First()</c> or <c>ToList()</c>.
    ///     </para>
    ///     <para>
    ///         Only the <c>ToList()</c> / <c>ToArray()</c> marker is restricted. A plain sub-query in a projection
    ///         (a join group, or one used by <c>Any</c> / <c>Count</c>) stays inside the SQL and is not affected.
    ///     </para>
    ///     <para>
    ///         Run from <see cref="Querying.QueryCompiler"/>, once per cache miss.
    ///     </para>
    /// </summary>
    public static class ChildCollectionPlacementValidator
    {
        private static readonly HashSet<string> TerminalNames = new HashSet<string>
        {
            nameof(Queryable.First), nameof(Queryable.FirstOrDefault),
            nameof(Queryable.Single), nameof(Queryable.SingleOrDefault),
            nameof(Queryable.Last), nameof(Queryable.LastOrDefault),
            nameof(Enumerable.ToList), nameof(Enumerable.ToArray),
        };

        /// <summary>
        ///     Throws <see cref="NotSupportedException"/> if <paramref name="expression"/> loads a child
        ///     collection outside the last <c>Select</c>, or inside another child collection.
        /// </summary>
        public static void Validate(Expression expression)
        {
            if (expression is null)
                return;
            new Walker(FindFinalSelect(expression), MapFollowingOperators(expression)).Visit(expression);
        }

        /// <summary>
        ///     <c>ToList()</c> / <c>ToArray()</c> with no other argument.
        /// </summary>
        public static bool IsChildCollectionMarker(Expression expression)
            => expression is MethodCallExpression call
                && call.Method.DeclaringType == typeof(Enumerable)
                && (call.Method.Name == nameof(Enumerable.ToList) || call.Method.Name == nameof(Enumerable.ToArray))
                && call.Arguments.Count == 1;

        private static bool IsQueryMethod(MethodCallExpression call)
            => call.Method.DeclaringType == typeof(Queryable) || call.Method.DeclaringType == typeof(Enumerable);

        private static bool IsSelect(MethodCallExpression call)
            => call.Method.Name == nameof(Queryable.Select) && IsQueryMethod(call) && call.Arguments.Count == 2;

        private static MethodCallExpression FindFinalSelect(Expression expression)
        {
            var current = expression;
            while (current is MethodCallExpression call
                    && call.Arguments.Count == 1
                    && TerminalNames.Contains(call.Method.Name)
                    && IsQueryMethod(call))
                current = call.Arguments[0];
            return current is MethodCallExpression select && IsSelect(select) ? select : null;
        }

        /// <summary>
        ///     For each operator of the main chain, the name of the operator written right after it, so the
        ///     message can say what follows a misplaced <c>Select</c>.
        /// </summary>
        private static Dictionary<Expression, string> MapFollowingOperators(Expression root)
        {
            var following = new Dictionary<Expression, string>();
            string outer = null;
            var current = root;
            while (current is MethodCallExpression call && call.Arguments.Count > 0)
            {
                if (outer != null)
                    following[call] = outer;
                outer = call.Method.Name;
                current = call.Arguments[0];
            }
            return following;
        }

        /// <summary>
        ///     The outermost markers in an expression. A marker's own source is not searched.
        /// </summary>
        private sealed class MarkerCollector : ExpressionVisitor
        {
            private readonly List<MethodCallExpression> markers = new List<MethodCallExpression>();

            public static IReadOnlyList<MethodCallExpression> CollectFrom(Expression expression)
            {
                var collector = new MarkerCollector();
                collector.Visit(expression);
                return collector.markers;
            }

            public override Expression Visit(Expression node)
            {
                if (IsChildCollectionMarker(node))
                {
                    this.markers.Add((MethodCallExpression)node);
                    return node;
                }
                return base.Visit(node);
            }
        }

        private sealed class Walker : ExpressionVisitor
        {
            private readonly MethodCallExpression finalSelect;
            private readonly Dictionary<Expression, string> followedBy;

            public Walker(MethodCallExpression finalSelect, Dictionary<Expression, string> followedBy)
            {
                this.finalSelect = finalSelect;
                this.followedBy = followedBy;
            }

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                if (IsSelect(node))
                {
                    var markers = MarkerCollector.CollectFrom(node.Arguments[1]);
                    if (markers.Count > 0)
                    {
                        if (!ReferenceEquals(node, this.finalSelect))
                            throw new NotSupportedException(this.MisplacedMessage(node, markers[0]));
                        foreach (var marker in markers)
                        {
                            var inner = MarkerCollector.CollectFrom(marker.Arguments[0]);
                            if (inner.Count > 0)
                                throw new NotSupportedException(
                                    $"A child collection ({Describe(inner[0])}) is selected inside another child collection ({Describe(marker)}). " +
                                    "Only one level of child collections is supported.");
                        }
                    }
                }
                return base.VisitMethodCall(node);
            }

            private string MisplacedMessage(MethodCallExpression select, MethodCallExpression marker)
            {
                var follow = this.followedBy.TryGetValue(select, out var name)
                                ? $" It is followed by '{name}'."
                                : string.Empty;
                return $"A child collection ({Describe(marker)}) must be selected in the last Select of the query.{follow} " +
                       "Move that Select to the end, after the Where, OrderBy, Join and Take steps.";
            }

            private static string Describe(MethodCallExpression marker)
                => $"{marker.Arguments[0]}.{marker.Method.Name}()";
        }
    }
}
