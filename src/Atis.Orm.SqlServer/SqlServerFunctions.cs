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

        /// <summary>Returns the character represented by an ASCII code.</summary>
        public static string Char(int? ascii) => throw new DirectCallNotSupportedException(nameof(Char));

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

        /// <summary>Returns a valid delimited SQL Server identifier using square brackets.</summary>
        public static string QuoteName(string str) => throw new DirectCallNotSupportedException(nameof(QuoteName));

        /// <summary>Returns a valid SQL Server identifier delimited by <paramref name="quoteCharacter"/>.</summary>
        public static string QuoteName(string str, string quoteCharacter) => throw new DirectCallNotSupportedException(nameof(QuoteName));

        /// <summary>Returns the four-character SOUNDEX code of a string.</summary>
        public static string Soundex(string str) => throw new DirectCallNotSupportedException(nameof(Soundex));

        /// <summary>Converts a number to character data.</summary>
        public static string Str(decimal? value) => throw new DirectCallNotSupportedException(nameof(Str));

        /// <summary>Converts a number to character data of the requested length.</summary>
        public static string Str(decimal? value, int? length) => throw new DirectCallNotSupportedException(nameof(Str));

        /// <summary>Converts a number to character data with the requested length and decimal places.</summary>
        public static string Str(decimal? value, int? length, int? decimals) => throw new DirectCallNotSupportedException(nameof(Str));

        /// <summary>Concatenates two or more strings end to end. NULL is treated as an empty string.</summary>
        public static string Concat(string str1, string str2, params string[] otherStrings) => throw new DirectCallNotSupportedException(nameof(Concat));

        /// <summary>Concatenates two or more strings, separated by <paramref name="separator"/>. NULL values are skipped. SQL Server 2017 and later.</summary>
        public static string Concat_WS(string separator, string str1, string str2, params string[] otherStrings) => throw new DirectCallNotSupportedException(nameof(Concat_WS));

        /// <summary>Returns the leftmost <paramref name="length"/> characters of a string.</summary>
        public static string Left(string str, int? length) => throw new DirectCallNotSupportedException(nameof(Left));

        /// <summary>Returns the string with every character converted to lowercase.</summary>
        public static string Lower(string str) => throw new DirectCallNotSupportedException(nameof(Lower));

        /// <summary>Removes leading spaces.</summary>
        public static string LTrim(string str) => throw new DirectCallNotSupportedException(nameof(LTrim));

        /// <summary>Returns the Unicode character with the given code point.</summary>
        public static string NChar(int? code) => throw new DirectCallNotSupportedException(nameof(NChar));

        /// <summary>Replaces every occurrence of <paramref name="pattern"/> in <paramref name="str"/> with <paramref name="replacement"/>.</summary>
        public static string Replace(string str, string pattern, string replacement) => throw new DirectCallNotSupportedException(nameof(Replace));

        /// <summary>Repeats a string <paramref name="count"/> times.</summary>
        public static string Replicate(string str, int? count) => throw new DirectCallNotSupportedException(nameof(Replicate));

        /// <summary>Reverses the characters of a string.</summary>
        public static string Reverse(string str) => throw new DirectCallNotSupportedException(nameof(Reverse));

        /// <summary>Returns the rightmost <paramref name="length"/> characters of a string.</summary>
        public static string Right(string str, int? length) => throw new DirectCallNotSupportedException(nameof(Right));

        /// <summary>Removes trailing spaces.</summary>
        public static string RTrim(string str) => throw new DirectCallNotSupportedException(nameof(RTrim));

        /// <summary>Returns a string of <paramref name="count"/> spaces.</summary>
        public static string Space(int? count) => throw new DirectCallNotSupportedException(nameof(Space));

        /// <summary>Escapes special characters in text. <paramref name="type"/> is currently only "json".</summary>
        public static string String_Escape(string str, string type) => throw new DirectCallNotSupportedException(nameof(String_Escape));

        /// <summary>Deletes <paramref name="length"/> characters from <paramref name="str"/> at <paramref name="start"/> and inserts <paramref name="replaceWith"/> there.</summary>
        public static string Stuff(string str, int? start, int? length, string replaceWith) => throw new DirectCallNotSupportedException(nameof(Stuff));

        /// <summary>Returns part of a string, starting at the 1-based <paramref name="start"/>.</summary>
        public static string SubString(string str, int? start, int? length) => throw new DirectCallNotSupportedException(nameof(SubString));

        /// <summary>Replaces each character of <paramref name="characters"/> found in <paramref name="str"/> with the character at the same position in <paramref name="translations"/>. SQL Server 2017 and later.</summary>
        public static string Translate(string str, string characters, string translations) => throw new DirectCallNotSupportedException(nameof(Translate));

        /// <summary>Removes leading and trailing spaces.</summary>
        public static string Trim(string str) => throw new DirectCallNotSupportedException(nameof(Trim));

        /// <summary>Removes the given <paramref name="characters"/> from both ends of <paramref name="str"/>. SQL Server 2017 and later.</summary>
        [SqlFunction(Template = "trim({0} from {1})")]
        public static string Trim(string characters, string str) => throw new DirectCallNotSupportedException(nameof(Trim));

        /// <summary>Returns the Unicode code point of the leftmost character.</summary>
        public static int? Unicode(string str) => throw new DirectCallNotSupportedException(nameof(Unicode));

        /// <summary>Returns the string with every character converted to uppercase.</summary>
        public static string Upper(string str) => throw new DirectCallNotSupportedException(nameof(Upper));

        #endregion

        #region Numeric and aggregate functions

        /// <summary>Returns a pseudo-random float between 0 and 1.</summary>
        public static double? Rand() => throw new DirectCallNotSupportedException(nameof(Rand));

        /// <summary>Returns a repeatable pseudo-random value for the supplied seed.</summary>
        public static double? Rand(int? seed) => throw new DirectCallNotSupportedException(nameof(Rand));

        /// <summary>Returns the checksum aggregate of the non-null values in a group.</summary>
        public static int? Checksum_Agg(object expr) => throw new DirectCallNotSupportedException(nameof(Checksum_Agg));

        /// <summary>Returns the checksum aggregate of the distinct non-null values in a group.</summary>
        [SqlFunction(Template = "checksum_agg(distinct {0})")]
        public static int? Checksum_AggDistinct(object expr) => throw new DirectCallNotSupportedException(nameof(Checksum_AggDistinct));

        #endregion

        #region Aggregate functions

        // These are ordinary functions: the ORM writes Sum(x) and nothing more. Using one without a GROUP BY, or
        // next to a column that is not grouped, is a query the server will refuse, and that is left to the caller.
        // Sum and Avg return the type of their argument (Avg of an int column is an int), so each has one overload
        // per numeric type; Stdev and Var always return float.

        /// <summary>Counts the rows in a group.</summary>
        [SqlFunction(Template = "count(*)")]
        public static int Count() => throw new DirectCallNotSupportedException(nameof(Count));

        /// <summary>Counts the rows in a group where the expression is not NULL.</summary>
        public static int Count(object expr) => throw new DirectCallNotSupportedException(nameof(Count));

        /// <summary>Counts the distinct non-NULL values of the expression in a group.</summary>
        [SqlFunction(Template = "count(distinct {0})")]
        public static int CountDistinct(object expr) => throw new DirectCallNotSupportedException(nameof(CountDistinct));

        /// <summary>Counts the rows in a group, as a bigint.</summary>
        [SqlFunction(Template = "count_big(*)")]
        public static long Count_Big() => throw new DirectCallNotSupportedException(nameof(Count_Big));

        /// <summary>Counts the rows in a group where the expression is not NULL, as a bigint.</summary>
        public static long Count_Big(object expr) => throw new DirectCallNotSupportedException(nameof(Count_Big));

        /// <summary>Counts the distinct non-NULL values of the expression in a group, as a bigint.</summary>
        [SqlFunction(Template = "count_big(distinct {0})")]
        public static long Count_BigDistinct(object expr) => throw new DirectCallNotSupportedException(nameof(Count_BigDistinct));

        /// <summary>Returns an approximate count of the distinct non-NULL values in a group.</summary>
        public static long Approx_Count_Distinct(object expr) => throw new DirectCallNotSupportedException(nameof(Approx_Count_Distinct));

        /// <summary>Returns the sum of the non-NULL values in a group.</summary>
        public static int? Sum(int? expr) => throw new DirectCallNotSupportedException(nameof(Sum));
        /// <summary>Returns the sum of the non-NULL values in a group.</summary>
        public static long? Sum(long? expr) => throw new DirectCallNotSupportedException(nameof(Sum));
        /// <summary>Returns the sum of the non-NULL values in a group.</summary>
        public static decimal? Sum(decimal? expr) => throw new DirectCallNotSupportedException(nameof(Sum));
        /// <summary>Returns the sum of the non-NULL values in a group.</summary>
        public static double? Sum(double? expr) => throw new DirectCallNotSupportedException(nameof(Sum));

        /// <summary>Returns the sum of the distinct non-NULL values in a group.</summary>
        [SqlFunction(Template = "sum(distinct {0})")]
        public static int? SumDistinct(int? expr) => throw new DirectCallNotSupportedException(nameof(SumDistinct));
        /// <summary>Returns the sum of the distinct non-NULL values in a group.</summary>
        [SqlFunction(Template = "sum(distinct {0})")]
        public static long? SumDistinct(long? expr) => throw new DirectCallNotSupportedException(nameof(SumDistinct));
        /// <summary>Returns the sum of the distinct non-NULL values in a group.</summary>
        [SqlFunction(Template = "sum(distinct {0})")]
        public static decimal? SumDistinct(decimal? expr) => throw new DirectCallNotSupportedException(nameof(SumDistinct));
        /// <summary>Returns the sum of the distinct non-NULL values in a group.</summary>
        [SqlFunction(Template = "sum(distinct {0})")]
        public static double? SumDistinct(double? expr) => throw new DirectCallNotSupportedException(nameof(SumDistinct));

        /// <summary>Returns the average of the non-NULL values in a group, in the type of the argument.</summary>
        public static int? Avg(int? expr) => throw new DirectCallNotSupportedException(nameof(Avg));
        /// <summary>Returns the average of the non-NULL values in a group, in the type of the argument.</summary>
        public static long? Avg(long? expr) => throw new DirectCallNotSupportedException(nameof(Avg));
        /// <summary>Returns the average of the non-NULL values in a group, in the type of the argument.</summary>
        public static decimal? Avg(decimal? expr) => throw new DirectCallNotSupportedException(nameof(Avg));
        /// <summary>Returns the average of the non-NULL values in a group, in the type of the argument.</summary>
        public static double? Avg(double? expr) => throw new DirectCallNotSupportedException(nameof(Avg));

        /// <summary>Returns the average of the distinct non-NULL values in a group, in the type of the argument.</summary>
        [SqlFunction(Template = "avg(distinct {0})")]
        public static int? AvgDistinct(int? expr) => throw new DirectCallNotSupportedException(nameof(AvgDistinct));
        /// <summary>Returns the average of the distinct non-NULL values in a group, in the type of the argument.</summary>
        [SqlFunction(Template = "avg(distinct {0})")]
        public static long? AvgDistinct(long? expr) => throw new DirectCallNotSupportedException(nameof(AvgDistinct));
        /// <summary>Returns the average of the distinct non-NULL values in a group, in the type of the argument.</summary>
        [SqlFunction(Template = "avg(distinct {0})")]
        public static decimal? AvgDistinct(decimal? expr) => throw new DirectCallNotSupportedException(nameof(AvgDistinct));
        /// <summary>Returns the average of the distinct non-NULL values in a group, in the type of the argument.</summary>
        [SqlFunction(Template = "avg(distinct {0})")]
        public static double? AvgDistinct(double? expr) => throw new DirectCallNotSupportedException(nameof(AvgDistinct));

        /// <summary>Returns the largest value in a group.</summary>
        public static T Max<T>(T expr) => throw new DirectCallNotSupportedException(nameof(Max));

        /// <summary>Returns the largest of the distinct values in a group.</summary>
        [SqlFunction(Template = "max(distinct {0})")]
        public static T MaxDistinct<T>(T expr) => throw new DirectCallNotSupportedException(nameof(MaxDistinct));

        /// <summary>Returns the smallest value in a group.</summary>
        public static T Min<T>(T expr) => throw new DirectCallNotSupportedException(nameof(Min));

        /// <summary>Returns the smallest of the distinct values in a group.</summary>
        [SqlFunction(Template = "min(distinct {0})")]
        public static T MinDistinct<T>(T expr) => throw new DirectCallNotSupportedException(nameof(MinDistinct));

        /// <summary>Joins the non-NULL values of a group into one string, separated by <paramref name="separator"/>. SQL Server 2017 and later.</summary>
        public static string String_Agg(string expr, string separator) => throw new DirectCallNotSupportedException(nameof(String_Agg));

        /// <summary>
        ///     Joins the non-NULL values of a group into one string in the given order:
        ///     <c>String_Agg(x.Name, ", ", Sql.Over().OrderBy(x.Name))</c>. The clause must be built inline and
        ///     cannot have a <c>PartitionBy</c>. SQL Server 2017 and later.
        /// </summary>
        [SqlFunction(Template = "string_agg({0}, {1}) within group ({~2})")]
        public static string String_Agg(string expr, string separator, OverClause withinGroupOrder) => throw new DirectCallNotSupportedException(nameof(String_Agg));

        /// <summary>
        ///     Returns the interpolated value at <paramref name="percentile"/> (0 to 1) of the ordered group, as float:
        ///     <c>Percentile_Cont(0.5, Sql.Over().OrderBy(x.Salary), Sql.Over().PartitionBy(x.DepartmentId))</c>.
        ///     SQL Server evaluates it as a window function, so the second clause is required and may only partition.
        /// </summary>
        [SqlFunction(Template = "percentile_cont({0}) within group ({~1}) {2}")]
        public static double? Percentile_Cont(double percentile, OverClause withinGroupOrder, OverClause over) => throw new DirectCallNotSupportedException(nameof(Percentile_Cont));

        /// <summary>
        ///     Returns the value at <paramref name="percentile"/> (0 to 1) of the ordered group, picked from the group itself.
        ///     The result has the type of the ordering expression, which cannot be worked out from the arguments, so
        ///     name it: <c>Percentile_Disc&lt;decimal?&gt;(0.5, Sql.Over().OrderBy(x.Salary), Sql.Over())</c>.
        /// </summary>
        [SqlFunction(Template = "percentile_disc({0}) within group ({~1}) {2}")]
        public static T Percentile_Disc<T>(double percentile, OverClause withinGroupOrder, OverClause over) => throw new DirectCallNotSupportedException(nameof(Percentile_Disc));

        /// <summary>Returns the statistical standard deviation of the values in a group (a sample).</summary>
        public static double? Stdev(object expr) => throw new DirectCallNotSupportedException(nameof(Stdev));

        /// <summary>Returns the statistical standard deviation of the distinct values in a group (a sample).</summary>
        [SqlFunction(Template = "stdev(distinct {0})")]
        public static double? StdevDistinct(object expr) => throw new DirectCallNotSupportedException(nameof(StdevDistinct));

        /// <summary>Returns the statistical standard deviation of the values in a group (the whole population).</summary>
        public static double? Stdevp(object expr) => throw new DirectCallNotSupportedException(nameof(Stdevp));

        /// <summary>Returns the statistical standard deviation of the distinct values in a group (the whole population).</summary>
        [SqlFunction(Template = "stdevp(distinct {0})")]
        public static double? StdevpDistinct(object expr) => throw new DirectCallNotSupportedException(nameof(StdevpDistinct));

        /// <summary>Returns the statistical variance of the values in a group (a sample).</summary>
        public static double? Var(object expr) => throw new DirectCallNotSupportedException(nameof(Var));

        /// <summary>Returns the statistical variance of the distinct values in a group (a sample).</summary>
        [SqlFunction(Template = "var(distinct {0})")]
        public static double? VarDistinct(object expr) => throw new DirectCallNotSupportedException(nameof(VarDistinct));

        /// <summary>Returns the statistical variance of the values in a group (the whole population).</summary>
        public static double? Varp(object expr) => throw new DirectCallNotSupportedException(nameof(Varp));

        /// <summary>Returns the statistical variance of the distinct values in a group (the whole population).</summary>
        [SqlFunction(Template = "varp(distinct {0})")]
        public static double? VarpDistinct(object expr) => throw new DirectCallNotSupportedException(nameof(VarpDistinct));

        #endregion

        #region Mathematical functions

        // Functions that return the type of their argument have one overload per numeric type, so the CLR
        // result type always matches what the server sends back. Functions that always return float take double.

        /// <summary>Returns the absolute value.</summary>
        public static int? Abs(int? value) => throw new DirectCallNotSupportedException(nameof(Abs));
        /// <summary>Returns the absolute value.</summary>
        public static long? Abs(long? value) => throw new DirectCallNotSupportedException(nameof(Abs));
        /// <summary>Returns the absolute value.</summary>
        public static decimal? Abs(decimal? value) => throw new DirectCallNotSupportedException(nameof(Abs));
        /// <summary>Returns the absolute value.</summary>
        public static double? Abs(double? value) => throw new DirectCallNotSupportedException(nameof(Abs));

        /// <summary>Returns the smallest integer greater than or equal to the value.</summary>
        public static int? Ceiling(int? value) => throw new DirectCallNotSupportedException(nameof(Ceiling));
        /// <summary>Returns the smallest integer greater than or equal to the value.</summary>
        public static long? Ceiling(long? value) => throw new DirectCallNotSupportedException(nameof(Ceiling));
        /// <summary>Returns the smallest integer greater than or equal to the value.</summary>
        public static decimal? Ceiling(decimal? value) => throw new DirectCallNotSupportedException(nameof(Ceiling));
        /// <summary>Returns the smallest integer greater than or equal to the value.</summary>
        public static double? Ceiling(double? value) => throw new DirectCallNotSupportedException(nameof(Ceiling));

        /// <summary>Returns the largest integer less than or equal to the value.</summary>
        public static int? Floor(int? value) => throw new DirectCallNotSupportedException(nameof(Floor));
        /// <summary>Returns the largest integer less than or equal to the value.</summary>
        public static long? Floor(long? value) => throw new DirectCallNotSupportedException(nameof(Floor));
        /// <summary>Returns the largest integer less than or equal to the value.</summary>
        public static decimal? Floor(decimal? value) => throw new DirectCallNotSupportedException(nameof(Floor));
        /// <summary>Returns the largest integer less than or equal to the value.</summary>
        public static double? Floor(double? value) => throw new DirectCallNotSupportedException(nameof(Floor));

        /// <summary>Returns -1, 0 or 1 for a negative, zero or positive value.</summary>
        public static int? Sign(int? value) => throw new DirectCallNotSupportedException(nameof(Sign));
        /// <summary>Returns -1, 0 or 1 for a negative, zero or positive value.</summary>
        public static long? Sign(long? value) => throw new DirectCallNotSupportedException(nameof(Sign));
        /// <summary>Returns -1, 0 or 1 for a negative, zero or positive value.</summary>
        public static decimal? Sign(decimal? value) => throw new DirectCallNotSupportedException(nameof(Sign));
        /// <summary>Returns -1, 0 or 1 for a negative, zero or positive value.</summary>
        public static double? Sign(double? value) => throw new DirectCallNotSupportedException(nameof(Sign));

        /// <summary>Rounds to <paramref name="length"/> decimal places (a negative length rounds to the left of the point).</summary>
        public static decimal? Round(decimal? value, int? length) => throw new DirectCallNotSupportedException(nameof(Round));
        /// <summary>Rounds to <paramref name="length"/> decimal places (a negative length rounds to the left of the point).</summary>
        public static double? Round(double? value, int? length) => throw new DirectCallNotSupportedException(nameof(Round));
        /// <summary>Rounds, or truncates when <paramref name="function"/> is not 0, to <paramref name="length"/> decimal places.</summary>
        public static decimal? Round(decimal? value, int? length, int? function) => throw new DirectCallNotSupportedException(nameof(Round));
        /// <summary>Rounds, or truncates when <paramref name="function"/> is not 0, to <paramref name="length"/> decimal places.</summary>
        public static double? Round(double? value, int? length, int? function) => throw new DirectCallNotSupportedException(nameof(Round));

        /// <summary>Returns the value raised to a power, in the type of the first argument.</summary>
        public static decimal? Power(decimal? value, decimal? power) => throw new DirectCallNotSupportedException(nameof(Power));
        /// <summary>Returns the value raised to a power, in the type of the first argument.</summary>
        public static double? Power(double? value, double? power) => throw new DirectCallNotSupportedException(nameof(Power));

        /// <summary>Converts radians to degrees.</summary>
        public static decimal? Degrees(decimal? value) => throw new DirectCallNotSupportedException(nameof(Degrees));
        /// <summary>Converts radians to degrees.</summary>
        public static double? Degrees(double? value) => throw new DirectCallNotSupportedException(nameof(Degrees));

        /// <summary>Converts degrees to radians.</summary>
        public static decimal? Radians(decimal? value) => throw new DirectCallNotSupportedException(nameof(Radians));
        /// <summary>Converts degrees to radians.</summary>
        public static double? Radians(double? value) => throw new DirectCallNotSupportedException(nameof(Radians));

        /// <summary>Returns the arccosine, in radians.</summary>
        public static double? Acos(double? value) => throw new DirectCallNotSupportedException(nameof(Acos));
        /// <summary>Returns the arcsine, in radians.</summary>
        public static double? Asin(double? value) => throw new DirectCallNotSupportedException(nameof(Asin));
        /// <summary>Returns the arctangent, in radians.</summary>
        public static double? Atan(double? value) => throw new DirectCallNotSupportedException(nameof(Atan));
        /// <summary>Returns the angle, in radians, between the positive x-axis and the ray from the origin to the point (<paramref name="x"/>, <paramref name="y"/>).</summary>
        public static double? Atn2(double? y, double? x) => throw new DirectCallNotSupportedException(nameof(Atn2));
        /// <summary>Returns the cosine of an angle in radians.</summary>
        public static double? Cos(double? value) => throw new DirectCallNotSupportedException(nameof(Cos));
        /// <summary>Returns the cotangent of an angle in radians.</summary>
        public static double? Cot(double? value) => throw new DirectCallNotSupportedException(nameof(Cot));
        /// <summary>Returns e raised to the power of the value.</summary>
        public static double? Exp(double? value) => throw new DirectCallNotSupportedException(nameof(Exp));
        /// <summary>Returns the natural logarithm.</summary>
        public static double? Log(double? value) => throw new DirectCallNotSupportedException(nameof(Log));
        /// <summary>Returns the logarithm to the given base.</summary>
        public static double? Log(double? value, int? numberBase) => throw new DirectCallNotSupportedException(nameof(Log));
        /// <summary>Returns the base-10 logarithm.</summary>
        public static double? Log10(double? value) => throw new DirectCallNotSupportedException(nameof(Log10));
        /// <summary>Returns the constant pi.</summary>
        public static double? Pi() => throw new DirectCallNotSupportedException(nameof(Pi));
        /// <summary>Returns the sine of an angle in radians.</summary>
        public static double? Sin(double? value) => throw new DirectCallNotSupportedException(nameof(Sin));
        /// <summary>Returns the square root.</summary>
        public static double? Sqrt(double? value) => throw new DirectCallNotSupportedException(nameof(Sqrt));
        /// <summary>Returns the square of the value, as float.</summary>
        public static double? Square(double? value) => throw new DirectCallNotSupportedException(nameof(Square));
        /// <summary>Returns the tangent of an angle in radians.</summary>
        public static double? Tan(double? value) => throw new DirectCallNotSupportedException(nameof(Tan));

        #endregion

        #region Logical functions

        /// <summary>Returns <paramref name="trueValue"/> when the condition holds, otherwise <paramref name="falseValue"/>.</summary>
        [SqlFunction(Template = "iif({?0}, {1}, {2})")]
        public static R Iif<R>(bool condition, R trueValue, R falseValue) => throw new DirectCallNotSupportedException(nameof(Iif));

        /// <summary>Returns the item at the 1-based <paramref name="index"/> of the list, or NULL when the index is out of range.</summary>
        public static R Choose<R>(int? index, R value1, params R[] otherValues) => throw new DirectCallNotSupportedException(nameof(Choose));

        /// <summary>Returns <paramref name="replacement"/> when <paramref name="check"/> is NULL.</summary>
        public static T IsNull<T>(T check, T replacement) => throw new DirectCallNotSupportedException(nameof(IsNull));

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

        /// <summary>Builds a date from its parts.</summary>
        public static DateTime? DateFromParts(int? year, int? month, int? day) => throw new DirectCallNotSupportedException(nameof(DateFromParts));

        /// <summary>Builds a datetime2 from its parts. <paramref name="precision"/> is 0 to 7 and must be a constant.</summary>
        [SqlFunction(Template = "datetime2fromparts({0}, {1}, {2}, {3}, {4}, {5}, {6}, [7])")]
        public static DateTime? DateTime2FromParts(int? year, int? month, int? day, int? hour, int? minute, int? seconds, int? fractions, int precision) => throw new DirectCallNotSupportedException(nameof(DateTime2FromParts));

        /// <summary>Builds a datetime from its parts.</summary>
        public static DateTime? DateTimeFromParts(int? year, int? month, int? day, int? hour, int? minute, int? seconds, int? milliseconds) => throw new DirectCallNotSupportedException(nameof(DateTimeFromParts));

        /// <summary>Builds a time from its parts. <paramref name="precision"/> is 0 to 7 and must be a constant.</summary>
        [SqlFunction(Template = "timefromparts({0}, {1}, {2}, {3}, [4])")]
        public static TimeSpan? TimeFromParts(int? hour, int? minute, int? seconds, int? fractions, int precision) => throw new DirectCallNotSupportedException(nameof(TimeFromParts));

        /// <summary>Returns the day of the month.</summary>
        public static int? Day(DateTime? date) => throw new DirectCallNotSupportedException(nameof(Day));

        /// <summary>Returns the month number.</summary>
        public static int? Month(DateTime? date) => throw new DirectCallNotSupportedException(nameof(Month));

        /// <summary>Returns the year.</summary>
        public static int? Year(DateTime? date) => throw new DirectCallNotSupportedException(nameof(Year));

        /// <summary>Returns the last day of the month that contains <paramref name="date"/>.</summary>
        public static DateTime? EOMonth(DateTime? date) => throw new DirectCallNotSupportedException(nameof(EOMonth));

        /// <summary>Returns the last day of the month, <paramref name="monthOffset"/> months after the one that contains <paramref name="date"/>.</summary>
        public static DateTime? EOMonth(DateTime? date, int? monthOffset) => throw new DirectCallNotSupportedException(nameof(EOMonth));

        /// <summary>Returns the current database server date and time, as datetime.</summary>
        public static DateTime GetDate() => throw new DirectCallNotSupportedException(nameof(GetDate));

        /// <summary>Returns the current UTC date and time, as datetime.</summary>
        public static DateTime GetUTCDate() => throw new DirectCallNotSupportedException(nameof(GetUTCDate));

        /// <summary>Returns the current database server date and time, as datetime2(7).</summary>
        public static DateTime SysDateTime() => throw new DirectCallNotSupportedException(nameof(SysDateTime));

        /// <summary>Returns the current UTC date and time, as datetime2(7).</summary>
        public static DateTime SysUTCDateTime() => throw new DirectCallNotSupportedException(nameof(SysUTCDateTime));

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

        /// <summary>CAST(expr AS varbinary(max))</summary>
        [SqlFunction(Template = "cast({0} as varbinary(max))")]
        public static byte[] CastToVarbinary(object expr) => throw new DirectCallNotSupportedException(nameof(CastToVarbinary));

        /// <summary>CAST(expr AS varbinary(length)). <paramref name="length"/> must be a constant.</summary>
        [SqlFunction(Template = "cast({0} as varbinary([1]))")]
        public static byte[] CastToVarbinary(object expr, int length) => throw new DirectCallNotSupportedException(nameof(CastToVarbinary));

        /// <summary>CAST(expr AS bit)</summary>
        [SqlFunction(Template = "cast({0} as bit)")]
        public static bool? CastToBit(object expr) => throw new DirectCallNotSupportedException(nameof(CastToBit));

        /// <summary>CAST(expr AS date)</summary>
        [SqlFunction(Template = "cast({0} as date)")]
        public static DateTime? CastToDate(object expr) => throw new DirectCallNotSupportedException(nameof(CastToDate));

        /// <summary>CAST(expr AS datetime)</summary>
        [SqlFunction(Template = "cast({0} as datetime)")]
        public static DateTime? CastToDateTime(object expr) => throw new DirectCallNotSupportedException(nameof(CastToDateTime));

        /// <summary>CONVERT(varchar(max), expr, style)</summary>
        [SqlFunction(Template = "convert(varchar(max), {0}, {1})")]
        public static string ConvertToVarchar(object expr, int? style) => throw new DirectCallNotSupportedException(nameof(ConvertToVarchar));

        /// <summary>CONVERT(varchar(length), expr, style). <paramref name="length"/> must be a constant.</summary>
        [SqlFunction(Template = "convert(varchar([2]), {0}, {1})")]
        public static string ConvertToVarchar(object expr, int? style, int length) => throw new DirectCallNotSupportedException(nameof(ConvertToVarchar));

        /// <summary>CONVERT(nvarchar(max), expr, style)</summary>
        [SqlFunction(Template = "convert(nvarchar(max), {0}, {1})")]
        public static string ConvertToNvarchar(object expr, int? style) => throw new DirectCallNotSupportedException(nameof(ConvertToNvarchar));

        /// <summary>CONVERT(nvarchar(length), expr, style). <paramref name="length"/> must be a constant.</summary>
        [SqlFunction(Template = "convert(nvarchar([2]), {0}, {1})")]
        public static string ConvertToNvarchar(object expr, int? style, int length) => throw new DirectCallNotSupportedException(nameof(ConvertToNvarchar));

        /// <summary>CONVERT(int, expr, style)</summary>
        [SqlFunction(Template = "convert(int, {0}, {1})")]
        public static int? ConvertToInt(object expr, int? style) => throw new DirectCallNotSupportedException(nameof(ConvertToInt));

        /// <summary>CONVERT(decimal(19, 5), expr, style)</summary>
        [SqlFunction(Template = "convert(decimal(19, 5), {0}, {1})")]
        public static decimal? ConvertToDecimal(object expr, int? style) => throw new DirectCallNotSupportedException(nameof(ConvertToDecimal));

        /// <summary>CONVERT(decimal(precision, scale), expr, style). Precision and scale must be constants.</summary>
        [SqlFunction(Template = "convert(decimal([2], [3]), {0}, {1})")]
        public static decimal? ConvertToDecimal(object expr, int? style, int precision, int scale) => throw new DirectCallNotSupportedException(nameof(ConvertToDecimal));

        /// <summary>CONVERT(bit, expr, style)</summary>
        [SqlFunction(Template = "convert(bit, {0}, {1})")]
        public static bool? ConvertToBit(object expr, int? style) => throw new DirectCallNotSupportedException(nameof(ConvertToBit));

        /// <summary>CONVERT(varbinary(max), expr, style)</summary>
        [SqlFunction(Template = "convert(varbinary(max), {0}, {1})")]
        public static byte[] ConvertToVarbinary(object expr, int? style) => throw new DirectCallNotSupportedException(nameof(ConvertToVarbinary));

        /// <summary>CONVERT(varbinary(length), expr, style). <paramref name="length"/> must be a constant.</summary>
        [SqlFunction(Template = "convert(varbinary([2]), {0}, {1})")]
        public static byte[] ConvertToVarbinary(object expr, int? style, int length) => throw new DirectCallNotSupportedException(nameof(ConvertToVarbinary));

        /// <summary>CONVERT(datetime, expr, style)</summary>
        [SqlFunction(Template = "convert(datetime, {0}, {1})")]
        public static DateTime? ConvertToDateTime(object expr, int? style) => throw new DirectCallNotSupportedException(nameof(ConvertToDateTime));

        /// <summary>CONVERT(date, expr, style)</summary>
        [SqlFunction(Template = "convert(date, {0}, {1})")]
        public static DateTime? ConvertToDate(object expr, int? style) => throw new DirectCallNotSupportedException(nameof(ConvertToDate));

        #endregion

        #region Metadata and validation functions

        /// <summary>
        ///     Returns the number of bytes used to represent an expression. The server returns int for most
        ///     types and bigint only for the (max) types, so the result is cast to bigint to give the
        ///     materializer one stable type.
        /// </summary>
        [SqlFunction(Template = "cast(datalength({0}) as bigint)")]
        public static long? DataLength(object expr) => throw new DirectCallNotSupportedException(nameof(DataLength));

        /// <summary>Returns whether an expression can be converted to a date or time value.</summary>
        public static int? IsDate(object expr) => throw new DirectCallNotSupportedException(nameof(IsDate));

        #endregion

        #region Security and identity functions

        /// <summary>Returns the database principal ID for a principal name.</summary>
        public static int? Database_Principal_ID(string principalName) => throw new DirectCallNotSupportedException(nameof(Database_Principal_ID));

        /// <summary>Returns whether the current user can access a database.</summary>
        public static int? Has_DBAccess(string databaseName) => throw new DirectCallNotSupportedException(nameof(Has_DBAccess));

        /// <summary>Returns whether the current database principal belongs to a database role or Windows group.</summary>
        public static int? Is_Member(string groupOrRole) => throw new DirectCallNotSupportedException(nameof(Is_Member));

        /// <summary>Returns whether the current database principal belongs to a database role.</summary>
        public static int? Is_RoleMember(string role) => throw new DirectCallNotSupportedException(nameof(Is_RoleMember));

        /// <summary>Returns whether a database principal belongs to a database role.</summary>
        public static int? Is_RoleMember(string role, string databasePrincipal) => throw new DirectCallNotSupportedException(nameof(Is_RoleMember));

        /// <summary>Returns whether the current login belongs to a fixed server role.</summary>
        public static int? Is_SrvRoleMember(string role) => throw new DirectCallNotSupportedException(nameof(Is_SrvRoleMember));

        /// <summary>Returns whether a login belongs to a fixed server role.</summary>
        public static int? Is_SrvRoleMember(string role, string login) => throw new DirectCallNotSupportedException(nameof(Is_SrvRoleMember));

        /// <summary>Returns the login name that connected to SQL Server.</summary>
        public static string Original_Login() => throw new DirectCallNotSupportedException(nameof(Original_Login));

        /// <summary>Returns a permission bitmap for an object.</summary>
        public static int? Permissions(int? objectId) => throw new DirectCallNotSupportedException(nameof(Permissions));

        /// <summary>Returns a permission bitmap for a column of an object.</summary>
        public static int? Permissions(int? objectId, string column) => throw new DirectCallNotSupportedException(nameof(Permissions));

        /// <summary>Returns the current execution-context database user.</summary>
        [SqlFunction(Template = "session_user")]
        public static string Session_User() => throw new DirectCallNotSupportedException(nameof(Session_User));

        /// <summary>Returns the server principal ID of the current login.</summary>
        public static int? SUser_Id() => throw new DirectCallNotSupportedException(nameof(SUser_Id));

        /// <summary>Returns the server principal ID of a login.</summary>
        public static int? SUser_Id(string login) => throw new DirectCallNotSupportedException(nameof(SUser_Id));

        /// <summary>Returns the login name for a server principal ID.</summary>
        public static string SUser_Name(int? serverUserId) => throw new DirectCallNotSupportedException(nameof(SUser_Name));

        /// <summary>Returns the security identifier of the current login.</summary>
        public static byte[] SUser_SID() => throw new DirectCallNotSupportedException(nameof(SUser_SID));

        /// <summary>Returns the security identifier of a login.</summary>
        public static byte[] SUser_SID(string login) => throw new DirectCallNotSupportedException(nameof(SUser_SID));

        /// <summary>Returns the security identifier of a login, optionally allowing the login to be absent.</summary>
        public static byte[] SUser_SID(string login, int? param2) => throw new DirectCallNotSupportedException(nameof(SUser_SID));

        /// <summary>Returns the login name of the current security context.</summary>
        public static string SUser_SName() => throw new DirectCallNotSupportedException(nameof(SUser_SName));

        /// <summary>Returns the login name for a security identifier.</summary>
        public static string SUser_SName(byte[] serverUserId) => throw new DirectCallNotSupportedException(nameof(SUser_SName));

        /// <summary>Returns the current login name.</summary>
        [SqlFunction(Template = "system_user")]
        public static string System_User() => throw new DirectCallNotSupportedException(nameof(System_User));

        /// <summary>Returns the name of the current database user.</summary>
        public static string User_Name() => throw new DirectCallNotSupportedException(nameof(User_Name));

        /// <summary>Returns the database user name for a database principal ID.</summary>
        public static string User_Name(int? id) => throw new DirectCallNotSupportedException(nameof(User_Name));

        /// <summary>Returns the current database user name.</summary>
        [SqlFunction(Template = "current_user")]
        public static string Current_User() => throw new DirectCallNotSupportedException(nameof(Current_User));

        /// <summary>Returns the current database user name.</summary>
        [SqlFunction(Template = "user")]
        public static string User() => throw new DirectCallNotSupportedException(nameof(User));

        #endregion

        #region System functions

        /// <summary>Returns the binary checksum of the current row.</summary>
        [SqlFunction(Template = "binary_checksum(*)")]
        public static int? Binary_Checksum() => throw new DirectCallNotSupportedException(nameof(Binary_Checksum));

        /// <summary>Returns a binary checksum over the supplied expressions.</summary>
        public static int? Binary_Checksum(object expr1, params object[] otherExprs) => throw new DirectCallNotSupportedException(nameof(Binary_Checksum));

        /// <summary>Returns the checksum of the current row.</summary>
        [SqlFunction(Template = "checksum(*)")]
        public static int? Checksum() => throw new DirectCallNotSupportedException(nameof(Checksum));

        /// <summary>Returns a checksum over the supplied expressions.</summary>
        public static int? Checksum(object expr1, params object[] otherExprs) => throw new DirectCallNotSupportedException(nameof(Checksum));

        /// <summary>Returns a new unique identifier, evaluated on the server for each row.</summary>
        public static Guid NewID() => throw new DirectCallNotSupportedException(nameof(NewID));

        /// <summary>Returns whether an expression is a valid numeric type.</summary>
        public static int IsNumeric(object expr) => throw new DirectCallNotSupportedException(nameof(IsNumeric));

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
