using Atis.SqlExpressionEngine.SqlExpressions;

namespace Atis.Orm.Abstractions
{
    /// <summary>
    ///     <para>
    ///         How a table and a column are spelled in this provider's SQL — qualified, and quoted or
    ///         bracketed if the dialect needs it.
    ///     </para>
    ///     <para>
    ///         The one place the spelling is decided. The query translator names tables and columns
    ///         through it, and so does every statement the ORM writes outside the translator, such as
    ///         the chunk statements of a streamed column write. A provider that changes the spelling
    ///         registers its own implementation once, and every statement follows. Two separate
    ///         spellings could drift apart, and a statement would then reach a different table than the
    ///         one the rest of the save wrote to.
    ///     </para>
    /// </summary>
    public interface ISqlNaming
    {
        /// <summary>The name <paramref name="table"/> is written by, with whichever of server, database and schema it has.</summary>
        string GetQualifiedTableName(SqlTable table);

        /// <summary>The name the column <paramref name="columnName"/> is written by, without any table alias in front.</summary>
        string GetColumnName(string columnName);
    }
}
