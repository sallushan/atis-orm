using System;
using System.Collections.Generic;
using System.Data;

namespace Atis.Orm.DataAccess
{
    /// <summary>
    ///     <para>
    ///         The rules for turning a result-set row into a dictionary, in one place.
    ///     </para>
    ///     <para>
    ///         Two callers need them and they discover their columns differently:
    ///         <c>ExecuteDictionary</c> reads the names off the reader, while the element factory for an
    ///         UPDATE with an OUTPUT clause already knows them at plan time. What they must agree on is
    ///         everything else — keys compared case-insensitively, a <c>NULL</c> column coming back as
    ///         <c>null</c> rather than <see cref="DBNull"/>, and a duplicate column name being an error
    ///         rather than a silently dropped value.
    ///     </para>
    /// </summary>
    internal static class DictionaryRow
    {
        /// <summary>
        ///     Matches how a database treats identifiers, so a caller can write the column name in a
        ///     different case than the query did.
        /// </summary>
        public static readonly StringComparer DefaultKeyComparer = StringComparer.OrdinalIgnoreCase;

        /// <summary>
        ///     <para>
        ///         Rejects duplicate column names up front. Letting <c>Dictionary</c> overwrite instead
        ///         would silently drop a value; letting <c>Dictionary.Add</c> throw would say only that
        ///         some key was duplicated, once per row, without naming the column.
        ///     </para>
        /// </summary>
        public static void EnsureNoDuplicateColumns(IReadOnlyList<string> columnNames, StringComparer keyComparer)
        {
            var seen = new HashSet<string>(keyComparer);
            for (var i = 0; i < columnNames.Count; i++)
            {
                if (!seen.Add(columnNames[i]))
                {
                    throw new InvalidOperationException(
                        $"The result set has more than one column named '{columnNames[i]}', so it cannot be turned " +
                        "into a dictionary. Alias the duplicate columns in the query.");
                }
            }
        }

        /// <summary>Reads the column names off <paramref name="reader"/>, rejecting duplicates.</summary>
        public static string[] GetColumnNames(IDataRecord reader, StringComparer keyComparer)
        {
            var names = new string[reader.FieldCount];
            for (var i = 0; i < names.Length; i++)
            {
                names[i] = reader.GetName(i);
            }
            EnsureNoDuplicateColumns(names, keyComparer);
            return names;
        }

        /// <summary>
        ///     Builds one row. <paramref name="columnNames"/> is positional: element <c>i</c> names
        ///     ordinal <c>i</c>.
        /// </summary>
        public static Dictionary<string, object> Read(IDataRecord reader, IReadOnlyList<string> columnNames, StringComparer keyComparer)
        {
            var row = new Dictionary<string, object>(columnNames.Count, keyComparer);
            for (var i = 0; i < columnNames.Count; i++)
            {
                var value = reader.GetValue(i);
                row[columnNames[i]] = value is DBNull ? null : value;
            }
            return row;
        }
    }
}
