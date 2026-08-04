using Atis.SqlExpressionEngine.SqlExpressions;
using System.Linq;

namespace Atis.Orm.Translation
{
    /// <summary>
    ///     <para>
    ///         How a <see cref="SqlTable"/> is spelled in SQL. Every statement that names a table —
    ///         a FROM source, an INSERT destination, an INSERT ... SELECT destination — must spell it
    ///         the same way, so the rule lives here instead of being rewritten at each site.
    ///     </para>
    /// </summary>
    public static class SqlTableNaming
    {
        /// <summary>
        ///     Joins whichever of server, database, schema and table name are present. A part is dropped
        ///     rather than emitted empty, so an unqualified table stays <c>Person</c> and not <c>...Person</c>.
        /// </summary>
        public static string GetQualifiedName(SqlTable table)
        {
            if (table is null)
                return null;

            var parts = new[] { table.Server, table.Database, table.Schema, table.TableName };
            return string.Join(".", parts.Where(x => !string.IsNullOrEmpty(x)));
        }
    }
}
