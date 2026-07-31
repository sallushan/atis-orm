namespace Atis.SqlExpressionEngine.SqlExpressions
{
    /// <summary>
    ///     <para>
    ///         Which image of a row a data-manipulation statement's output column is read from.
    ///     </para>
    ///     <para>
    ///         Names the intent, not the syntax. How a dialect spells it — SQL Server's
    ///         <c>inserted</c> / <c>deleted</c> pseudo-tables, PostgreSQL's <c>RETURNING</c>, which
    ///         has no prefix at all — is the translator's business.
    ///     </para>
    /// </summary>
    public enum SqlOutputSource
    {
        /// <summary>The row as it stands after the statement has been applied.</summary>
        Inserted,

        /// <summary>The row as it stood before the statement was applied.</summary>
        Deleted,
    }
}
