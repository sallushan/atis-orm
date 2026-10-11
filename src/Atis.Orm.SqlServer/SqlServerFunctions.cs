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

        #endregion

        #region Numeric and aggregate functions

        /// <summary>Returns a repeatable pseudo-random value for the supplied seed.</summary>
        public static double? Rand(int? seed) => throw new DirectCallNotSupportedException(nameof(Rand));

        /// <summary>Returns the checksum aggregate of the non-null values in a group.</summary>
        public static int? Checksum_Agg(object expr) => throw new DirectCallNotSupportedException(nameof(Checksum_Agg));

        /// <summary>Returns the checksum aggregate of the distinct non-null values in a group.</summary>
        [SqlFunction(Template = "checksum_agg(distinct {0})")]
        public static int? Checksum_AggDistinct(object expr) => throw new DirectCallNotSupportedException(nameof(Checksum_AggDistinct));

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

        /// <summary>CAST(expr AS varbinary(max))</summary>
        [SqlFunction(Template = "cast({0} as varbinary(max))")]
        public static byte[] CastToVarbinary(object expr) => throw new DirectCallNotSupportedException(nameof(CastToVarbinary));

        /// <summary>CAST(expr AS varbinary(length)). <paramref name="length"/> must be a constant.</summary>
        [SqlFunction(Template = "cast({0} as varbinary([1]))")]
        public static byte[] CastToVarbinary(object expr, int length) => throw new DirectCallNotSupportedException(nameof(CastToVarbinary));

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

        /// <summary>Returns the database user name for a database principal ID.</summary>
        public static string User_Name(int? id) => throw new DirectCallNotSupportedException(nameof(User_Name));

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
