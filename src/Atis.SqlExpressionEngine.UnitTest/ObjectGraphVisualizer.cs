using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Atis.Orm.Services;
namespace Atis.SqlExpressionEngine.UnitTest
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    public static class ObjectGraphVisualizer
    {
        public static string Dump(object obj, int indentLevel = 0, HashSet<object> visited = null)
        {
            if (obj == null) return "null";

            visited ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
            var indent = new string(' ', indentLevel * 2);
            var type = obj.GetType();

            // 1. Handle Primitive-like types (Stop recursion)
            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) || type == typeof(Guid))
            {
                return obj.ToString();
            }

            // 2. Prevent Infinite Loops for Circular References
            if (visited.Contains(obj)) return $"[Cycle Detected: {type.Name}]";
            visited.Add(obj);

            var sb = new StringBuilder();
            sb.AppendLine($"{type.Name}");

            // 3. Handle Collections (IEnumerable)
            if (obj is IEnumerable enumerable)
            {
                int index = 0;
                foreach (var item in enumerable)
                {
                    sb.AppendLine($"{indent}  [{index++}]: {Dump(item, indentLevel + 2, visited)}");
                }
                return sb.ToString().TrimEnd();
            }

            // 4. Extract all Properties and Fields (Public + Private + Instance)
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

            // Process Fields
            foreach (var field in type.GetFields(flags))
            {
                var value = field.GetValue(obj);
                sb.AppendLine($"{indent}  [F] {field.Name}: {Dump(value, indentLevel + 1, visited)}");
            }

            // Process Properties
            foreach (var prop in type.GetProperties(flags))
            {
                try
                {
                    // Skip indexers
                    if (prop.GetIndexParameters().Length > 0) continue;

                    var value = prop.GetValue(obj);
                    sb.AppendLine($"{indent}  [P] {prop.Name}: {Dump(value, indentLevel + 1, visited)}");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"{indent}  [P] {prop.Name}: [Error reading: {ex.Message}]");
                }
            }

            return sb.ToString().TrimEnd();
        }
    }

    // Helper to check for actual object equality to prevent cycles
    public class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
