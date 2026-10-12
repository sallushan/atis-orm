using Atis.SqlExpressionEngine.Visitors;
using System;

namespace Atis.SqlExpressionEngine.SqlExpressions
{
    /// <summary>
    ///     A <see cref="DateTime"/> built from its parts, produced from <c>new DateTime(year, month, day)</c>,
    ///     <c>new DateTime(year, month, day, hour, minute, second)</c> or the same with a trailing millisecond argument.
    ///     Each part is a named property so a SQL dialect never has to guess what an argument position means.
    /// </summary>
    public sealed class SqlNewDateTimeExpression : SqlExpression
    {
        /// <summary>
        ///     Creates a date-time construction expression. <paramref name="hour"/>, <paramref name="minute"/> and
        ///     <paramref name="second"/> are given together or not at all; <paramref name="millisecond"/> needs them.
        /// </summary>
        public SqlNewDateTimeExpression(
            SqlExpression year,
            SqlExpression month,
            SqlExpression day,
            SqlExpression hour = null,
            SqlExpression minute = null,
            SqlExpression second = null,
            SqlExpression millisecond = null)
        {
            if ((hour == null) != (minute == null) || (minute == null) != (second == null))
                throw new ArgumentException("Hour, minute and second must be given together or not at all.");
            if (millisecond != null && hour == null)
                throw new ArgumentException("Millisecond requires hour, minute and second.");

            this.Year = year ?? throw new ArgumentNullException(nameof(year));
            this.Month = month ?? throw new ArgumentNullException(nameof(month));
            this.Day = day ?? throw new ArgumentNullException(nameof(day));
            this.Hour = hour;
            this.Minute = minute;
            this.Second = second;
            this.Millisecond = millisecond;
        }

        /// <inheritdoc />
        public override SqlExpressionType NodeType => SqlExpressionType.NewDateTime;

        /// <summary>The year part.</summary>
        public SqlExpression Year { get; }

        /// <summary>The month part (1 to 12).</summary>
        public SqlExpression Month { get; }

        /// <summary>The day part.</summary>
        public SqlExpression Day { get; }

        /// <summary>The hour part, or <c>null</c> when the date has no time part.</summary>
        public SqlExpression Hour { get; }

        /// <summary>The minute part, or <c>null</c> when the date has no time part.</summary>
        public SqlExpression Minute { get; }

        /// <summary>The second part, or <c>null</c> when the date has no time part.</summary>
        public SqlExpression Second { get; }

        /// <summary>The millisecond part, or <c>null</c> when not given.</summary>
        public SqlExpression Millisecond { get; }

        /// <summary>Returns this node when no part changed; otherwise returns a new node.</summary>
        public SqlNewDateTimeExpression Update(
            SqlExpression year, SqlExpression month, SqlExpression day,
            SqlExpression hour, SqlExpression minute, SqlExpression second, SqlExpression millisecond)
        {
            if (year == this.Year && month == this.Month && day == this.Day
                && hour == this.Hour && minute == this.Minute && second == this.Second && millisecond == this.Millisecond)
                return this;
            return new SqlNewDateTimeExpression(year, month, day, hour, minute, second, millisecond);
        }

        /// <inheritdoc />
        protected internal override SqlExpression Accept(SqlExpressionVisitor sqlExpressionVisitor)
        {
            return sqlExpressionVisitor.VisitSqlNewDateTime(this);
        }
    }
}
