using System;
using System.Collections.Generic;
using System.Linq;

using Atis.SqlExpressionEngine.SqlExpressions;
using Atis.SqlExpressionEngine.Visitors;

namespace Atis.Orm.Functions
{
    /// <summary>The window functions the neutral <see cref="Sql"/> class offers.</summary>
    public enum SqlWindowFunction
    {
        /// <summary>ROW_NUMBER()</summary>
        RowNumber,
        /// <summary>RANK()</summary>
        Rank,
        /// <summary>DENSE_RANK()</summary>
        DenseRank,
        /// <summary>NTILE(n)</summary>
        Ntile,
        /// <summary>PERCENT_RANK()</summary>
        PercentRank,
        /// <summary>CUME_DIST()</summary>
        CumeDist,
        /// <summary>LAG(value [, offset [, default]])</summary>
        Lag,
        /// <summary>LEAD(value [, offset [, default]])</summary>
        Lead,
        /// <summary>FIRST_VALUE(value)</summary>
        FirstValue,
        /// <summary>LAST_VALUE(value)</summary>
        LastValue,
    }

    /// <summary>
    ///     The <c>OVER (PARTITION BY ... ORDER BY ...)</c> part of a window function.
    /// </summary>
    public sealed class SqlOverClauseExpression : SqlExpression
    {
        /// <summary>An OVER clause with nothing in it.</summary>
        public SqlOverClauseExpression()
            : this(Array.Empty<SqlExpression>(), Array.Empty<OrderByColumn>())
        {
        }

        /// <summary>Creates the node.</summary>
        public SqlOverClauseExpression(IReadOnlyList<SqlExpression> partitionBy, IReadOnlyList<OrderByColumn> orderBy)
        {
            this.PartitionBy = partitionBy ?? throw new ArgumentNullException(nameof(partitionBy));
            this.OrderBy = orderBy ?? throw new ArgumentNullException(nameof(orderBy));
        }

        /// <summary>The PARTITION BY expressions.</summary>
        public IReadOnlyList<SqlExpression> PartitionBy { get; }

        /// <summary>The ORDER BY columns.</summary>
        public IReadOnlyList<OrderByColumn> OrderBy { get; }

        /// <summary>A copy with one more PARTITION BY expression.</summary>
        public SqlOverClauseExpression AddPartitionBy(SqlExpression expression)
            => new SqlOverClauseExpression(this.PartitionBy.Concat(new[] { expression }).ToList(), this.OrderBy);

        /// <summary>A copy with one more ORDER BY column.</summary>
        public SqlOverClauseExpression AddOrderBy(SqlExpression expression, SortDirection direction)
            => new SqlOverClauseExpression(this.PartitionBy, this.OrderBy.Concat(new[] { new OrderByColumn(expression, direction) }).ToList());

        /// <inheritdoc />
        protected override SqlExpression VisitChildren(SqlExpressionVisitor visitor)
        {
            var changed = false;
            var partitions = new List<SqlExpression>(this.PartitionBy.Count);
            foreach (var p in this.PartitionBy)
            {
                var visited = visitor.Visit(p);
                changed |= !ReferenceEquals(visited, p);
                partitions.Add(visited);
            }
            var orders = new List<OrderByColumn>(this.OrderBy.Count);
            foreach (var o in this.OrderBy)
            {
                var visited = visitor.Visit(o.ColumnExpression);
                if (ReferenceEquals(visited, o.ColumnExpression))
                    orders.Add(o);
                else
                {
                    changed = true;
                    orders.Add(new OrderByColumn(visited, o.Direction));
                }
            }
            return changed ? new SqlOverClauseExpression(partitions, orders) : this;
        }

        /// <inheritdoc />
        public override string ToString()
            => $"over (partition by [{string.Join(", ", this.PartitionBy)}] order by [{string.Join(", ", this.OrderBy)}])";
    }

    /// <summary>
    ///     A window function call such as <c>ROW_NUMBER() OVER (...)</c>. The base translator writes the
    ///     standard SQL spelling; a provider overrides <c>TranslateWindowFunction</c> where its database differs.
    /// </summary>
    public sealed class SqlWindowFunctionExpression : SqlFunctionExpression
    {
        /// <summary>Creates the node.</summary>
        public SqlWindowFunctionExpression(SqlWindowFunction function, IReadOnlyList<SqlExpression> arguments, SqlOverClauseExpression over)
            : base(function.ToString(), arguments)
        {
            this.Function = function;
            this.Over = over ?? throw new ArgumentNullException(nameof(over));
        }

        /// <summary>Which window function.</summary>
        public SqlWindowFunction Function { get; }

        /// <summary>The OVER clause.</summary>
        public SqlOverClauseExpression Over { get; }

        /// <inheritdoc />
        public override SqlFunctionExpression Update(IEnumerable<SqlExpression> arguments)
        {
            var list = arguments?.ToList() ?? new List<SqlExpression>();
            if (list.SequenceEqual(this.Arguments))
                return this;
            return new SqlWindowFunctionExpression(this.Function, list, this.Over);
        }

        /// <inheritdoc />
        public override IEnumerable<object> GetHashComponents()
        {
            yield return this.FunctionName;
            // The OVER clause is a separate node, so which list a child sits in and its direction are hashed here.
            yield return this.Arguments.Count;
            yield return this.Over.PartitionBy.Count;
            foreach (var order in this.Over.OrderBy)
                yield return order.Direction;
        }

        /// <inheritdoc />
        protected override SqlExpression VisitChildren(SqlExpressionVisitor visitor)
        {
            var changed = false;
            var arguments = new List<SqlExpression>(this.Arguments.Count);
            foreach (var a in this.Arguments)
            {
                var visited = visitor.Visit(a);
                changed |= !ReferenceEquals(visited, a);
                arguments.Add(visited);
            }
            var over = visitor.Visit(this.Over) as SqlOverClauseExpression
                ?? throw new InvalidOperationException("A visitor replaced the OVER clause of a window function with a different kind of node.");
            changed |= !ReferenceEquals(over, this.Over);
            return changed ? new SqlWindowFunctionExpression(this.Function, arguments, over) : this;
        }

        /// <inheritdoc />
        public override string ToString() => $"{this.Function}({string.Join(", ", this.Arguments)}) {this.Over}";
    }
}
