using Atis.Orm.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;

namespace Atis.Orm.Translation
{
    /// <summary>
    ///     <para>
    ///         The default <see cref="ISqlNaming"/>: a table is its non-empty parts joined with dots (see
    ///         <see cref="SqlTableNaming"/>), and a column is written as it is, unquoted.
    ///     </para>
    ///     <para>
    ///         Unquoted because that is how this ORM has always written names. A provider whose
    ///         dialect needs quoting derives from this and overrides the method it needs.
    ///     </para>
    /// </summary>
    public class SqlNaming : ISqlNaming
    {
        /// <inheritdoc />
        public virtual string GetQualifiedTableName(SqlTable table) => SqlTableNaming.GetQualifiedName(table);

        /// <inheritdoc />
        public virtual string GetColumnName(string columnName) => columnName;
    }
}
