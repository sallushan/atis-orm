using System;
using System.Collections.Generic;
using System.Text;

namespace Atis.SqlExpressionEngine.SqlExpressions
{
    /// <summary>
    ///     A sub-query selected in a projection with <c>ToList()</c> / <c>ToArray()</c>. It is a
    ///     <see cref="SqlQueryableExpression"/> that remembers the call was written, so that
    ///     <see cref="SqlSelectExpression"/> can refuse to wrap a query that still carries one.
    /// </summary>
    public class SqlProjectedSubQueryExpression : SqlQueryableExpression
    {
        /// <summary>Creates the marker around the sub-query.</summary>
        public SqlProjectedSubQueryExpression(SqlDerivedTableExpression query) : base(query)
        {
        }

        /// <inheritdoc />
        public override SqlExpressionType NodeType => SqlExpressionType.ProjectedSubQuery;

        /// <summary>Keeps the marker when a visitor rebuilds the node.</summary>
        public override SqlQueryableExpression Update(SqlDerivedTableExpression query)
        {
            if (query == Query)
                return this;
            return new SqlProjectedSubQueryExpression(query);
        }
    }
}
