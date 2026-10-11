using System;
using System.Collections.Generic;
using System.Linq;

namespace Atis.SqlExpressionEngine.SqlExpressions
{
    /// <summary>The aggregate functions that LINQ has a method for. The provider decides how each one is spelled.</summary>
    public enum SqlAggregateFunction
    {
        /// <summary><c>COUNT</c>.</summary>
        Count,
        /// <summary>A count that does not overflow an int (<c>COUNT_BIG</c> on SQL Server).</summary>
        CountBig,
        /// <summary><c>SUM</c>.</summary>
        Sum,
        /// <summary><c>MIN</c>.</summary>
        Min,
        /// <summary><c>MAX</c>.</summary>
        Max,
        /// <summary>The average (<c>AVG</c> on SQL Server).</summary>
        Avg,
    }

    /// <summary>
    ///     An aggregate function call produced from a LINQ aggregate method (<c>Count</c>, <c>Sum</c> and so on).
    ///     Unlike a plain <see cref="SqlFunctionExpression"/>, it holds which function it is as an enum, so the
    ///     translator picks the spelling and does not have to guess from a name.
    /// </summary>
    public sealed class SqlAggregateFunctionExpression : SqlFunctionExpression
    {
        /// <summary>Creates the node.</summary>
        public SqlAggregateFunctionExpression(SqlAggregateFunction function, IEnumerable<SqlExpression> arguments)
            : base(function.ToString(), arguments)
        {
            this.Function = function;
        }

        /// <summary>Which aggregate function.</summary>
        public SqlAggregateFunction Function { get; }

        /// <inheritdoc />
        public override SqlFunctionExpression Update(IEnumerable<SqlExpression> arguments)
        {
            if (arguments != null && !(arguments is IReadOnlyList<SqlExpression>))
                arguments = arguments.ToArray();
            if (arguments == this.Arguments
                || (arguments != null && this.Arguments.SequenceEqual(arguments)))
                return this;

            return new SqlAggregateFunctionExpression(this.Function, arguments);
        }
    }
}
