using System;

using Atis.Orm.Annotations;
using Atis.SqlExpressionEngine.Exceptions;

namespace Atis.Orm
{
    /// <summary>
    ///     The type of <see cref="Sql.Over"/>. It only exists so the OVER clause has a type to chain on;
    ///     it has no members of its own and is never created at run time.
    /// </summary>
    public sealed class OverClause
    {
        private OverClause()
        {
        }
    }

    /// <summary>One end of a window frame, used with <c>Sql.Rows(...)</c> and <c>Sql.Range(...)</c>.</summary>
    public enum WindowFrameBound
    {
        /// <summary>UNBOUNDED PRECEDING: the first row of the partition.</summary>
        UnboundedPreceding,
        /// <summary>n PRECEDING: n rows (or, for RANGE, n units of the ORDER BY value) before the current row. Needs a count.</summary>
        Preceding,
        /// <summary>CURRENT ROW.</summary>
        CurrentRow,
        /// <summary>n FOLLOWING: n rows (or units) after the current row. Needs a count.</summary>
        Following,
        /// <summary>UNBOUNDED FOLLOWING: the last row of the partition.</summary>
        UnboundedFollowing,
    }

    /// <summary>
    ///     <para>
    ///         Database functions that work the same on every database. They are markers: a query names
    ///         them and the translator writes the SQL for the provider in use. Calling one directly throws.
    ///     </para>
    ///     <para>
    ///         Only functions with no built-in C# spelling live here (a string length is <c>x.Name.Length</c>,
    ///         not a function). Functions that belong to one database, such as SQL Server's <c>CHARINDEX</c>,
    ///         are in that provider's own class (for SQL Server: <c>SqlServerFunctions</c>).
    ///     </para>
    ///     <para>
    ///         The OVER clause is built inline as a chain:
    ///     </para>
    ///     <code>
    ///     .Select(x =&gt; new
    ///     {
    ///         x.Id,
    ///         Rank = Sql.RowNumber(Sql.Over().PartitionBy(x.DeptId).OrderByDescending(x.Salary))
    ///     })
    ///     </code>
    ///     <para>
    ///         A window function sits in the select list, so use it in a projection and filter on the result
    ///         in a following <c>Where</c>. <c>Lag</c>/<c>Lead</c> return <c>null</c> when there is no such row;
    ///         when the column is not nullable, cast it (<c>(int?)x.Salary</c>) so the result can hold that.
    ///     </para>
    /// </summary>
    [SqlFunctionClass]
    public static class Sql
    {
        #region OVER clause

        /// <summary>Starts an OVER clause with no partition and no order.</summary>
        public static OverClause Over() => throw new DirectCallNotSupportedException(nameof(Over));

        /// <summary>Adds a <c>PARTITION BY</c> expression. Call it again to partition by more than one.</summary>
        public static OverClause PartitionBy<T>(this OverClause over, T expression) => throw new DirectCallNotSupportedException(nameof(PartitionBy));

        /// <summary>Adds an ascending <c>ORDER BY</c> expression. Call it again for more.</summary>
        public static OverClause OrderBy<T>(this OverClause over, T expression) => throw new DirectCallNotSupportedException(nameof(OrderBy));

        /// <summary>Adds a descending <c>ORDER BY</c> expression. Call it again for more.</summary>
        public static OverClause OrderByDescending<T>(this OverClause over, T expression) => throw new DirectCallNotSupportedException(nameof(OrderByDescending));

        // Frame (ROWS / RANGE). Every argument must be a constant written in the query, because it becomes part of the SQL text.
        // A bound that needs a count (Preceding / Following) is followed by that count:
        //   .Rows(WindowFrameBound.UnboundedPreceding)                                         ROWS UNBOUNDED PRECEDING
        //   .Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow)            ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
        //   .Rows(WindowFrameBound.Preceding, 2, WindowFrameBound.CurrentRow)                  ROWS BETWEEN 2 PRECEDING AND CURRENT ROW
        //   .Rows(WindowFrameBound.CurrentRow, WindowFrameBound.Following, 1)                  ROWS BETWEEN CURRENT ROW AND 1 FOLLOWING
        //   .Rows(WindowFrameBound.Preceding, 2, WindowFrameBound.Following, 2)                ROWS BETWEEN 2 PRECEDING AND 2 FOLLOWING

        /// <summary>
        ///     Adds a <c>ROWS</c> frame that starts at <paramref name="start"/> and ends at the current row.
        ///     Without a frame, a database counts <c>RANGE UNBOUNDED PRECEDING</c> when there is an ORDER BY, which gives
        ///     tied rows the same running total; <c>ROWS</c> gives every row its own.
        /// </summary>
        public static OverClause Rows(this OverClause over, WindowFrameBound start) => throw new DirectCallNotSupportedException(nameof(Rows));

        /// <summary>Adds a <c>ROWS</c> frame that starts <paramref name="startCount"/> rows before or after the current row and ends at the current row.</summary>
        public static OverClause Rows(this OverClause over, WindowFrameBound start, int startCount) => throw new DirectCallNotSupportedException(nameof(Rows));

        /// <summary>Adds a <c>ROWS BETWEEN <paramref name="start"/> AND <paramref name="end"/></c> frame.</summary>
        public static OverClause Rows(this OverClause over, WindowFrameBound start, WindowFrameBound end) => throw new DirectCallNotSupportedException(nameof(Rows));

        /// <summary>Adds a <c>ROWS BETWEEN n PRECEDING/FOLLOWING AND <paramref name="end"/></c> frame.</summary>
        public static OverClause Rows(this OverClause over, WindowFrameBound start, int startCount, WindowFrameBound end) => throw new DirectCallNotSupportedException(nameof(Rows));

        /// <summary>Adds a <c>ROWS BETWEEN <paramref name="start"/> AND n PRECEDING/FOLLOWING</c> frame.</summary>
        public static OverClause Rows(this OverClause over, WindowFrameBound start, WindowFrameBound end, int endCount) => throw new DirectCallNotSupportedException(nameof(Rows));

        /// <summary>Adds a <c>ROWS BETWEEN n PRECEDING/FOLLOWING AND n PRECEDING/FOLLOWING</c> frame.</summary>
        public static OverClause Rows(this OverClause over, WindowFrameBound start, int startCount, WindowFrameBound end, int endCount) => throw new DirectCallNotSupportedException(nameof(Rows));

        /// <summary>Adds a <c>RANGE</c> frame that starts at <paramref name="start"/> and ends at the current row.</summary>
        public static OverClause Range(this OverClause over, WindowFrameBound start) => throw new DirectCallNotSupportedException(nameof(Range));

        /// <summary>Adds a <c>RANGE</c> frame that starts <paramref name="startCount"/> units before or after the current row and ends at the current row.</summary>
        public static OverClause Range(this OverClause over, WindowFrameBound start, int startCount) => throw new DirectCallNotSupportedException(nameof(Range));

        /// <summary>Adds a <c>RANGE BETWEEN <paramref name="start"/> AND <paramref name="end"/></c> frame.</summary>
        public static OverClause Range(this OverClause over, WindowFrameBound start, WindowFrameBound end) => throw new DirectCallNotSupportedException(nameof(Range));

        /// <summary>Adds a <c>RANGE BETWEEN n PRECEDING/FOLLOWING AND <paramref name="end"/></c> frame.</summary>
        public static OverClause Range(this OverClause over, WindowFrameBound start, int startCount, WindowFrameBound end) => throw new DirectCallNotSupportedException(nameof(Range));

        /// <summary>Adds a <c>RANGE BETWEEN <paramref name="start"/> AND n PRECEDING/FOLLOWING</c> frame.</summary>
        public static OverClause Range(this OverClause over, WindowFrameBound start, WindowFrameBound end, int endCount) => throw new DirectCallNotSupportedException(nameof(Range));

        /// <summary>Adds a <c>RANGE BETWEEN n PRECEDING/FOLLOWING AND n PRECEDING/FOLLOWING</c> frame.</summary>
        public static OverClause Range(this OverClause over, WindowFrameBound start, int startCount, WindowFrameBound end, int endCount) => throw new DirectCallNotSupportedException(nameof(Range));

        #endregion

        #region Ranking

        /// <summary>Number of the row inside its partition, starting at 1.</summary>
        public static long RowNumber(OverClause over) => throw new DirectCallNotSupportedException(nameof(RowNumber));

        /// <summary>Rank inside the partition; equal rows share a rank and the next rank skips.</summary>
        public static long Rank(OverClause over) => throw new DirectCallNotSupportedException(nameof(Rank));

        /// <summary>Rank inside the partition; equal rows share a rank and the next rank does not skip.</summary>
        public static long DenseRank(OverClause over) => throw new DirectCallNotSupportedException(nameof(DenseRank));

        /// <summary>Splits the partition into <paramref name="groups"/> groups and returns the group number of the row.</summary>
        public static long Ntile(int groups, OverClause over) => throw new DirectCallNotSupportedException(nameof(Ntile));

        /// <summary>Relative rank of the row, from 0 to 1.</summary>
        public static decimal PercentRank(OverClause over) => throw new DirectCallNotSupportedException(nameof(PercentRank));

        /// <summary>Share of rows that come at or before this row, above 0 up to 1.</summary>
        public static decimal CumeDist(OverClause over) => throw new DirectCallNotSupportedException(nameof(CumeDist));

        #endregion

        #region Value from another row

        /// <summary>Value of <paramref name="value"/> in the previous row of the partition.</summary>
        public static T Lag<T>(T value, OverClause over) => throw new DirectCallNotSupportedException(nameof(Lag));

        /// <summary>Value of <paramref name="value"/> <paramref name="offset"/> rows before this one.</summary>
        public static T Lag<T>(T value, int offset, OverClause over) => throw new DirectCallNotSupportedException(nameof(Lag));

        /// <summary>Like <see cref="Lag{T}(T, int, OverClause)"/>, giving <paramref name="defaultValue"/> when there is no such row.</summary>
        public static T Lag<T>(T value, int offset, T defaultValue, OverClause over) => throw new DirectCallNotSupportedException(nameof(Lag));

        /// <summary>Value of <paramref name="value"/> in the next row of the partition.</summary>
        public static T Lead<T>(T value, OverClause over) => throw new DirectCallNotSupportedException(nameof(Lead));

        /// <summary>Value of <paramref name="value"/> <paramref name="offset"/> rows after this one.</summary>
        public static T Lead<T>(T value, int offset, OverClause over) => throw new DirectCallNotSupportedException(nameof(Lead));

        /// <summary>Like <see cref="Lead{T}(T, int, OverClause)"/>, giving <paramref name="defaultValue"/> when there is no such row.</summary>
        public static T Lead<T>(T value, int offset, T defaultValue, OverClause over) => throw new DirectCallNotSupportedException(nameof(Lead));

        /// <summary>Value of <paramref name="value"/> in the first row of the partition.</summary>
        public static T FirstValue<T>(T value, OverClause over) => throw new DirectCallNotSupportedException(nameof(FirstValue));

        /// <summary>Value of <paramref name="value"/> in the last row of the partition, up to the current row unless the database says otherwise.</summary>
        public static T LastValue<T>(T value, OverClause over) => throw new DirectCallNotSupportedException(nameof(LastValue));

        #endregion
    }
}
