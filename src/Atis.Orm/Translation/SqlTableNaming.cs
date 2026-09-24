using Atis.SqlExpressionEngine.SqlExpressions;
using System.Linq;

namespace Atis.Orm.Translation
{
    /// <summary>
    ///     <para>
    ///         The default spelling of a <see cref="SqlTable"/>: its non-empty parts joined with dots.
    ///     </para>
    ///     <para>
    ///         Production code asks <see cref="Abstractions.ISqlNaming"/>, which a provider can replace;
    ///         the default <see cref="SqlNaming"/> delegates here. Use this directly only where no
    ///         provider is involved, such as a test translator.
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
