using System;
using System.Data;

namespace Atis.Orm.Benchmarks.Helpers
{
    /// <summary>
    /// Null-tolerant reader accessors used by the hand-coded baseline. Ported from Dapper's
    /// benchmark suite so the baseline does exactly the same work theirs does.
    /// </summary>
    public static class SqlDataReaderHelper
    {
        public static string GetNullableString(this IDataReader reader, int index)
        {
            var tmp = reader.GetValue(index);
            return tmp != DBNull.Value ? (string)tmp : null;
        }

        public static T? GetNullableValue<T>(this IDataReader reader, int index) where T : struct
        {
            var tmp = reader.GetValue(index);
            return tmp != DBNull.Value ? (T)tmp : (T?)null;
        }
    }
}
