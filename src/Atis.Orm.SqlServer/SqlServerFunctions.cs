using System;

using Atis.Orm.Annotations;
using Atis.SqlExpressionEngine.Exceptions;

namespace Atis.Orm.SqlServer
{
    /// <summary>
    ///     <para>
    ///         SQL Server functions, for queries that run on SQL Server only. They are markers: a query names
    ///         them and the translator writes the SQL; calling one directly throws.
    ///     </para>
    ///     <para>
    ///         This provider surface may repeat functionality that C# or the neutral <see cref="Sql"/> surface
    ///         already offers, so SQL Server-specific code can explicitly request the native function.
    ///     </para>
    ///     <para>
    ///         Without <see cref="SqlFunctionAttribute"/> a method is written as
    ///         <c>MethodName(args)</c>. In a template, <c>{n}</c> is a translated argument and <c>[n]</c> is a
    ///         constant written into the SQL text.
    ///     </para>
    /// </summary>
    [SqlFunctionClass]
    public static class SqlServerFunctions
    {
        #region String functions

        /// <summary>Returns the ASCII code of the leftmost character.</summary>
        public static int? Ascii(string str) => throw new DirectCallNotSupportedException(nameof(Ascii));

        /// <summary>Returns the position of <paramref name="exprToFind"/> in <paramref name="searchIn"/>, or 0.</summary>
        public static int? CharIndex(string exprToFind, string searchIn) => throw new DirectCallNotSupportedException(nameof(CharIndex));

        /// <summary>Returns the position of <paramref name="exprToFind"/> in <paramref name="searchIn"/>, starting the search at <paramref name="startLocation"/>, or 0.</summary>
        public static int? CharIndex(string exprToFind, string searchIn, int? startLocation) => throw new DirectCallNotSupportedException(nameof(CharIndex));

        /// <summary>Returns how different the <see cref="Soundex"/> codes of two strings are, from 0 to 4.</summary>
        public static int? Difference(string str1, string str2) => throw new DirectCallNotSupportedException(nameof(Difference));

        /// <summary>Formats a number with a .NET-style format string.</summary>
        public static string Format(int? value, string format) => throw new DirectCallNotSupportedException(nameof(Format));

        /// <summary>Formats a number with a .NET-style format string.</summary>
        public static string Format(decimal? value, string format) => throw new DirectCallNotSupportedException(nameof(Format));

        /// <summary>Formats a date with a .NET-style format string.</summary>
        public static string Format(DateTime? value, string format) => throw new DirectCallNotSupportedException(nameof(Format));

        /// <summary>Formats a number with a .NET-style format string for the given culture.</summary>
        public static string Format(int? value, string format, string culture) => throw new DirectCallNotSupportedException(nameof(Format));

        /// <summary>Formats a number with a .NET-style format string for the given culture.</summary>
        public static string Format(decimal? value, string format, string culture) => throw new DirectCallNotSupportedException(nameof(Format));

        /// <summary>Formats a date with a .NET-style format string for the given culture.</summary>
        public static string Format(DateTime? value, string format, string culture) => throw new DirectCallNotSupportedException(nameof(Format));

        /// <summary>Returns the number of characters, not counting trailing spaces.</summary>
        public static int? Len(string str) => throw new DirectCallNotSupportedException(nameof(Len));

        /// <summary>Returns where <paramref name="pattern"/> (with % wildcards) first matches in <paramref name="str"/>, or 0.</summary>
        public static int? PatIndex(string pattern, string str) => throw new DirectCallNotSupportedException(nameof(PatIndex));

        /// <summary>Returns the four-character SOUNDEX code of a string.</summary>
        public static string Soundex(string str) => throw new DirectCallNotSupportedException(nameof(Soundex));

        #endregion

        #region Date and time functions

        /// <summary>Adds <paramref name="number"/> units of <paramref name="datepart"/> to a date.</summary>
        [SqlFunction(Template = "dateadd([0], {1}, {2})")]
        public static DateTime? DateAdd(DatePart datepart, int? number, DateTime? date) => throw new DirectCallNotSupportedException(nameof(DateAdd));

        /// <summary>Counts the <paramref name="datepart"/> boundaries crossed between two dates.</summary>
        [SqlFunction(Template = "datediff([0], {1}, {2})")]
        public static int? DateDiff(DatePart datepart, DateTime? startDate, DateTime? endDate) => throw new DirectCallNotSupportedException(nameof(DateDiff));

        /// <summary>Like <see cref="DateDiff"/>, returning a bigint.</summary>
        [SqlFunction(Template = "datediff_big([0], {1}, {2})")]
        public static long? DateDiff_Big(DatePart datepart, DateTime? startDate, DateTime? endDate) => throw new DirectCallNotSupportedException(nameof(DateDiff_Big));

        /// <summary>Returns the name of a part of a date, as text.</summary>
        [SqlFunction(Template = "datename([0], {1})")]
        public static string DateName(DatePart datepart, DateTime? date) => throw new DirectCallNotSupportedException(nameof(DateName));

        /// <summary>Returns a part of a date, as a number.</summary>
        [SqlFunction(Template = "datepart([0], {1})")]
        public static int? DatePart(DatePart datepart, DateTime? date) => throw new DirectCallNotSupportedException(nameof(DatePart));

        #endregion

        #region Conversion functions

        /// <summary>CAST(expr AS varchar(max))</summary>
        [SqlFunction(Template = "cast({0} as varchar(max))")]
        public static string CastToVarchar(object expr) => throw new DirectCallNotSupportedException(nameof(CastToVarchar));

        /// <summary>CAST(expr AS varchar(length)). <paramref name="length"/> must be a constant.</summary>
        [SqlFunction(Template = "cast({0} as varchar([1]))")]
        public static string CastToVarchar(object expr, int length) => throw new DirectCallNotSupportedException(nameof(CastToVarchar));

        /// <summary>CAST(expr AS nvarchar(max))</summary>
        [SqlFunction(Template = "cast({0} as nvarchar(max))")]
        public static string CastToNvarchar(object expr) => throw new DirectCallNotSupportedException(nameof(CastToNvarchar));

        /// <summary>CAST(expr AS nvarchar(length)). <paramref name="length"/> must be a constant.</summary>
        [SqlFunction(Template = "cast({0} as nvarchar([1]))")]
        public static string CastToNvarchar(object expr, int length) => throw new DirectCallNotSupportedException(nameof(CastToNvarchar));

        /// <summary>CAST(expr AS int)</summary>
        [SqlFunction(Template = "cast({0} as int)")]
        public static int? CastToInt(object expr) => throw new DirectCallNotSupportedException(nameof(CastToInt));

        /// <summary>CAST(expr AS decimal(19, 5))</summary>
        [SqlFunction(Template = "cast({0} as decimal(19, 5))")]
        public static decimal? CastToDecimal(object expr) => throw new DirectCallNotSupportedException(nameof(CastToDecimal));

        /// <summary>CAST(expr AS decimal(precision, scale)). Both must be constants.</summary>
        [SqlFunction(Template = "cast({0} as decimal([1], [2]))")]
        public static decimal? CastToDecimal(object expr, int precision, int scale) => throw new DirectCallNotSupportedException(nameof(CastToDecimal));

        /// <summary>CONVERT(varchar(max), expr, style)</summary>
        [SqlFunction(Template = "convert(varchar(max), {0}, {1})")]
        public static string ConvertToVarchar(object expr, int? style) => throw new DirectCallNotSupportedException(nameof(ConvertToVarchar));

        /// <summary>CONVERT(varchar(length), expr, style). <paramref name="length"/> must be a constant.</summary>
        [SqlFunction(Template = "convert(varchar([2]), {0}, {1})")]
        public static string ConvertToVarchar(object expr, int? style, int length) => throw new DirectCallNotSupportedException(nameof(ConvertToVarchar));

        /// <summary>CONVERT(int, expr, style)</summary>
        [SqlFunction(Template = "convert(int, {0}, {1})")]
        public static int? ConvertToInt(object expr, int? style) => throw new DirectCallNotSupportedException(nameof(ConvertToInt));

        #endregion
    }

    /// <summary>The part of a date that <see cref="SqlServerFunctions.DateAdd"/> and its relatives work on.</summary>
    public enum DatePart
    {
        /// <summary>year</summary>
        year,
        /// <summary>quarter</summary>
        quarter,
        /// <summary>month</summary>
        month,
        /// <summary>dayofyear</summary>
        dayofyear,
        /// <summary>day</summary>
        day,
        /// <summary>week</summary>
        week,
        /// <summary>weekday</summary>
        weekday,
        /// <summary>hour</summary>
        hour,
        /// <summary>minute</summary>
        minute,
        /// <summary>second</summary>
        second,
        /// <summary>millisecond</summary>
        millisecond,
        /// <summary>microsecond</summary>
        microsecond,
        /// <summary>nanosecond</summary>
        nanosecond,
    }
}
