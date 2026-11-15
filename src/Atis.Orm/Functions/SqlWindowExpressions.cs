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
        /// <summary>SUM(value) OVER (...)</summary>
        Sum,
        /// <summary>AVG(value) OVER (...)</summary>
        Avg,
        /// <summary>MIN(value) OVER (...)</summary>
        Min,
        /// <summary>MAX(value) OVER (...)</summary>
        Max,
        /// <summary>COUNT(*) or COUNT(value) OVER (...)</summary>
        Count,
    }

    /// <summary>Whether a window frame counts physical rows or values of the ORDER BY column.</summary>
    public enum SqlWindowFrameMode
    {
        /// <summary>ROWS: the frame is counted in rows.</summary>
        Rows,
        /// <summary>RANGE: rows with the same ORDER BY value belong together.</summary>
        Range,
    }

    /// <summary>
    ///     The frame of an OVER clause: <c>ROWS UNBOUNDED PRECEDING</c>,
    ///     <c>ROWS BETWEEN 2 PRECEDING AND CURRENT ROW</c> and so on. It has no SQL Server specifics, so every
    ///     provider can write it; a provider that cannot run a combination refuses it in its translator.
    /// </summary>
    public sealed class SqlWindowFrame
    {
        /// <summary>A frame with only a start (<c>ROWS UNBOUNDED PRECEDING</c>); the end is the current row.</summary>
        public SqlWindowFrame(SqlWindowFrameMode mode, WindowFrameBound start, int? startOffset)
            : this(mode, start, startOffset, null, null)
        {
        }

        /// <summary>A frame with a start and an end (<c>ROWS BETWEEN .. AND ..</c>).</summary>
        public SqlWindowFrame(SqlWindowFrameMode mode, WindowFrameBound start, int? startOffset, WindowFrameBound? end, int? endOffset)
        {
            CheckBound(start, startOffset, "start");
            if (end.HasValue)
            {
                CheckBound(end.Value, endOffset, "end");
                if (start == WindowFrameBound.UnboundedFollowing)
                    throw new InvalidOperationException("A window frame cannot start at UNBOUNDED FOLLOWING.");
                if (end.Value == WindowFrameBound.UnboundedPreceding)
                    throw new InvalidOperationException("A window frame cannot end at UNBOUNDED PRECEDING.");
                if (Order(start) > Order(end.Value))
                    throw new InvalidOperationException($"A window frame cannot start at {start} and end at {end.Value}: the start must not come after the end.");
            }
            else
            {
                if (endOffset.HasValue)
                    throw new InvalidOperationException("An end offset was given but the frame has no end bound.");
                if (start == WindowFrameBound.Following || start == WindowFrameBound.UnboundedFollowing)
                    throw new InvalidOperationException(
                        $"A window frame with only a start cannot start at {start}; use BETWEEN with an end bound.");
            }
            this.Mode = mode;
            this.Start = start;
            this.StartOffset = startOffset;
            this.End = end;
            this.EndOffset = endOffset;
        }

        /// <summary>ROWS or RANGE.</summary>
        public SqlWindowFrameMode Mode { get; }

        /// <summary>The start bound.</summary>
        public WindowFrameBound Start { get; }

        /// <summary>The row count of the start bound; set only for <see cref="WindowFrameBound.Preceding"/> and <see cref="WindowFrameBound.Following"/>.</summary>
        public int? StartOffset { get; }

        /// <summary>The end bound, or <c>null</c> when the frame has only a start.</summary>
        public WindowFrameBound? End { get; }

        /// <summary>The row count of the end bound; set only for <see cref="WindowFrameBound.Preceding"/> and <see cref="WindowFrameBound.Following"/>.</summary>
        public int? EndOffset { get; }

        private static int Order(WindowFrameBound bound)
        {
            switch (bound)
            {
                case WindowFrameBound.UnboundedPreceding: return 0;
                case WindowFrameBound.Preceding: return 1;
                case WindowFrameBound.CurrentRow: return 2;
                case WindowFrameBound.Following: return 3;
                default: return 4;
            }
        }

        private static void CheckBound(WindowFrameBound bound, int? offset, string which)
        {
            var needsOffset = bound == WindowFrameBound.Preceding || bound == WindowFrameBound.Following;
            if (needsOffset && !offset.HasValue)
                throw new InvalidOperationException($"The {which} bound {bound} needs a row count, for example 3 {bound.ToString().ToUpperInvariant()}.");
            if (!needsOffset && offset.HasValue)
                throw new InvalidOperationException($"The {which} bound {bound} does not take a row count.");
            if (offset.HasValue && offset.Value < 0)
                throw new InvalidOperationException($"The {which} bound row count cannot be negative.");
        }

        /// <inheritdoc />
        public override string ToString()
        {
            string Bound(WindowFrameBound b, int? n) => n.HasValue ? $"{n} {b}" : b.ToString();
            return this.End.HasValue
                ? $"{this.Mode} between {Bound(this.Start, this.StartOffset)} and {Bound(this.End.Value, this.EndOffset)}"
                : $"{this.Mode} {Bound(this.Start, this.StartOffset)}";
        }
    }

    /// <summary>
    ///     The <c>OVER (PARTITION BY ... ORDER BY ... ROWS ...)</c> part of a window function.
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
            : this(partitionBy, orderBy, null)
        {
        }

        /// <summary>Creates the node with a frame.</summary>
        public SqlOverClauseExpression(IReadOnlyList<SqlExpression> partitionBy, IReadOnlyList<OrderByColumn> orderBy, SqlWindowFrame frame)
        {
            this.PartitionBy = partitionBy ?? throw new ArgumentNullException(nameof(partitionBy));
            this.OrderBy = orderBy ?? throw new ArgumentNullException(nameof(orderBy));
            this.Frame = frame;
        }

        /// <summary>The PARTITION BY expressions.</summary>
        public IReadOnlyList<SqlExpression> PartitionBy { get; }

        /// <summary>The ORDER BY columns.</summary>
        public IReadOnlyList<OrderByColumn> OrderBy { get; }

        /// <summary>The ROWS / RANGE frame, or <c>null</c> when the clause has none (the database then uses its default).</summary>
        public SqlWindowFrame Frame { get; }

        /// <summary>A copy with one more PARTITION BY expression.</summary>
        public SqlOverClauseExpression AddPartitionBy(SqlExpression expression)
            => new SqlOverClauseExpression(this.PartitionBy.Concat(new[] { expression }).ToList(), this.OrderBy, this.Frame);

        /// <summary>A copy with one more ORDER BY column.</summary>
        public SqlOverClauseExpression AddOrderBy(SqlExpression expression, SortDirection direction)
            => new SqlOverClauseExpression(this.PartitionBy, this.OrderBy.Concat(new[] { new OrderByColumn(expression, direction) }).ToList(), this.Frame);

        /// <summary>A copy with the frame set. A clause can have only one.</summary>
        public SqlOverClauseExpression WithFrame(SqlWindowFrame frame)
        {
            if (frame == null)
                throw new ArgumentNullException(nameof(frame));
            if (this.Frame != null)
                throw new InvalidOperationException("An OVER clause can have only one frame (Rows or Range), but a second one was given.");
            return new SqlOverClauseExpression(this.PartitionBy, this.OrderBy, frame);
        }

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
            return changed ? new SqlOverClauseExpression(partitions, orders, this.Frame) : this;
        }

        /// <inheritdoc />
        public override string ToString()
            => $"over (partition by [{string.Join(", ", this.PartitionBy)}] order by [{string.Join(", ", this.OrderBy)}]{(this.Frame == null ? "" : " " + this.Frame)})";
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
        public override bool IsWindowFunction => true;

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
            // Two frames give different SQL, so the frame must separate the hashes too.
            var frame = this.Over.Frame;
            yield return frame?.Mode;
            yield return frame?.Start;
            yield return frame?.StartOffset;
            yield return frame?.End;
            yield return frame?.EndOffset;
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
