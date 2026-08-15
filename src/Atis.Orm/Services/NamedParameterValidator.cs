using System;
using System.Collections.Generic;
using System.Linq.Expressions;

using Atis.SqlExpressionEngine.ExpressionExtensions;

namespace Atis.Orm.Services
{
    /// <summary>
    ///     <para>
    ///         Rejects a query in which one <see cref="NamedParameterExpression"/> name carries two different
    ///         values.
    ///     </para>
    ///     <para>
    ///         A parameter's name is its identity, and identity is what the value is rebound under on a cache
    ///         hit. Two values under one name cannot both be rebound, so the query is broken — but not
    ///         visibly, because on the first run each node still keeps its own translation-time value and the
    ///         query returns exactly the right rows. The clash only surfaces on the second run, once values
    ///         are re-extracted into a dictionary. Left unchecked that is a bug which passes every test a
    ///         developer runs and fails in production after the query warms up.
    ///     </para>
    ///     <para>
    ///         Run from <see cref="Querying.QueryCompiler"/>, so the cost falls once per cache miss rather
    ///         than on every execution, and the error lands on the first run instead of the second.
    ///     </para>
    ///     <para>
    ///         Deliberately limited to named parameters. The equivalent clash between two <em>captured
    ///         variables</em> is also only fatal on a cache hit, but promoting it to compile time would make a
    ///         query that today runs once and works start throwing — and
    ///         <see cref="SqlExpressionEngine.Services.VariableIdentityProvider"/> derives type-qualified
    ///         member paths, which makes such a clash close to unreachable anyway.
    ///     </para>
    /// </summary>
    public static class NamedParameterValidator
    {
        /// <summary>
        ///     Throws <see cref="InvalidOperationException"/> if any named parameter in
        ///     <paramref name="expression"/> appears more than once with different values. A name repeated
        ///     with an equal value is fine — it is a single binding referenced twice.
        /// </summary>
        public static void Validate(Expression expression)
        {
            if (expression is null)
                return;
            new Walker().Visit(expression);
        }

        /// <summary>
        ///     The one wording for this failure, shared with
        ///     <see cref="ExpressionVariableValuesExtractor"/> so the same mistake reads the same whichever
        ///     guard catches it.
        /// </summary>
        public static string DuplicateNameMessage(string name, object firstValue, object secondValue)
            => $"The named parameter '{name}' is used more than once in this query with different values " +
               $"('{firstValue}' and '{secondValue}'). A parameter's name is its identity for cache-hit rebinding, " +
               $"so each distinct value needs its own name.";

        private sealed class Walker : ExpressionVisitor
        {
            private readonly Dictionary<string, object> valueByName = new Dictionary<string, object>();

            protected override Expression VisitExtension(Expression node)
            {
                if (node is NamedParameterExpression named)
                {
                    if (this.valueByName.TryGetValue(named.Name, out var existing))
                    {
                        if (!Equals(existing, named.Value))
                            throw new InvalidOperationException(DuplicateNameMessage(named.Name, existing, named.Value));
                    }
                    else
                    {
                        this.valueByName.Add(named.Name, named.Value);
                    }
                    // The value is a plain field, not a child expression; nothing below to visit.
                    return node;
                }
                return base.VisitExtension(node);
            }
        }
    }
}
