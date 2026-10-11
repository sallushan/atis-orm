using System.Linq;

using Atis.Orm.Translation;
using Atis.SqlExpressionEngine.SqlExpressions;

namespace Atis.Orm.SqlServer
{
    /// <summary>
    ///     <para>
    ///         The SQL Server spelling of names: every part of a table name, every column and every column alias is
    ///         written in square brackets, as SQL Server itself writes them (<c>[dbo].[Employee]</c>, <c>[Key]</c>).
    ///     </para>
    ///     <para>
    ///         Brackets go on every name, not only on the ones that would fail without them. A name that is not a
    ///         reserved word today can become one in a later SQL Server version, and one rule is easier to trust than a
    ///         keyword list. A <c>]</c> inside a name is doubled, which is how SQL Server escapes it.
    ///     </para>
    /// </summary>
    public class SqlServerNaming : SqlNaming
    {
        /// <inheritdoc />
        public override string GetQualifiedTableName(SqlTable table)
        {
            if (table is null)
                return null;

            var parts = new[] { table.Server, table.Database, table.Schema, table.TableName };
            return string.Join(".", parts.Where(x => !string.IsNullOrEmpty(x)).Select(Bracket));
        }

        /// <inheritdoc />
        public override string GetColumnName(string columnName)
            => string.IsNullOrEmpty(columnName) ? columnName : Bracket(columnName);

        private static string Bracket(string name) => "[" + name.Replace("]", "]]") + "]";
    }
}
