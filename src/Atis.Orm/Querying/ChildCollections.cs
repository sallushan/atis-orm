using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Atis.Orm.Querying
{
    /// <summary>
    ///     <para>
    ///         One row of a child query: the child item and the link value of the parent it belongs to.
    ///     </para>
    /// </summary>
    public sealed class ChildRow
    {
        /// <summary>Initializes a new instance of the <see cref="ChildRow"/> class.</summary>
        public ChildRow(object item, object key)
        {
            this.Item = item;
            this.Key = key;
        }

        /// <summary>The link value, see <see cref="ChildCollections.MakeKey"/>. <c>null</c> if any part of it is NULL.</summary>
        public object Key { get; }

        /// <summary>The child item.</summary>
        public object Item { get; }
    }

    /// <summary>
    ///     <para>
    ///         The rows of the child queries of one execution, grouped by the link value of their parent. The child
    ///         queries are run first and read completely; then the parent query is read, and each parent row asks
    ///         this object for the collection that belongs to it, so the collection is complete before the parent
    ///         object is constructed (which is why a parent that is an anonymous type or has only a constructor works).
    ///     </para>
    /// </summary>
    public sealed class ChildCollections
    {
        private static readonly object NoKey = new object();

        private readonly Dictionary<object, List<object>>[] itemsByKey;

        /// <summary>Initializes a new instance of the <see cref="ChildCollections"/> class for <paramref name="childCount"/> child queries.</summary>
        public ChildCollections(int childCount)
        {
            this.itemsByKey = new Dictionary<object, List<object>>[childCount];
            for (var i = 0; i < childCount; i++)
                this.itemsByKey[i] = new Dictionary<object, List<object>>();
        }

        /// <summary>
        ///     Reads the given columns of the current row, <c>null</c> for a NULL. The reader may be sequential, so
        ///     <paramref name="ordinals"/> must be ascending, and each is read once.
        /// </summary>
        public static object[] ReadKeyValues(IDataRecord record, int[] ordinals)
        {
            if (record is null)
                throw new ArgumentNullException(nameof(record));
            if (ordinals is null)
                throw new ArgumentNullException(nameof(ordinals));

            var values = new object[ordinals.Length];
            for (var i = 0; i < ordinals.Length; i++)
                values[i] = record.IsDBNull(ordinals[i]) ? null : record.GetValue(ordinals[i]);
            return values;
        }

        /// <summary>
        ///     Builds the link value from <paramref name="values"/> (see <see cref="ReadKeyValues"/>) at the given
        ///     <paramref name="positions"/>. No positions means the child query is not linked to the parent, so every parent
        ///     gets every row. A NULL in any of them gives <c>null</c>: it matches no parent, as in SQL.
        /// </summary>
        public static object MakeKey(object[] values, int[] positions)
        {
            if (values is null)
                throw new ArgumentNullException(nameof(values));
            if (positions is null)
                throw new ArgumentNullException(nameof(positions));

            if (positions.Length == 0)
                return NoKey;
            if (positions.Length == 1)
                return values[positions[0]];

            var parts = new object[positions.Length];
            for (var i = 0; i < positions.Length; i++)
            {
                parts[i] = values[positions[i]];
                if (parts[i] is null)
                    return null;
            }
            return new CompositeKey(parts);
        }

        /// <summary>Adds a row of the child query <paramref name="childIndex"/>.</summary>
        public void Add(int childIndex, ChildRow row)
        {
            if (row is null)
                throw new ArgumentNullException(nameof(row));
            if (row.Key is null)
                return;
            var byKey = this.itemsByKey[childIndex];
            if (!byKey.TryGetValue(row.Key, out var items))
            {
                items = new List<object>();
                byKey.Add(row.Key, items);
            }
            items.Add(row.Item);
        }

        /// <summary>A new list with the items of the parent whose link value is <paramref name="key"/>; empty if it has none.</summary>
        public List<T> GetList<T>(int childIndex, object key)
        {
            if (key != null && this.itemsByKey[childIndex].TryGetValue(key, out var items))
            {
                var list = new List<T>(items.Count);
                foreach (var item in items)
                    list.Add((T)item);
                return list;
            }
            return new List<T>();
        }

        /// <summary>A new array with the items of the parent whose link value is <paramref name="key"/>; empty if it has none.</summary>
        public T[] GetArray<T>(int childIndex, object key)
        {
            if (key != null && this.itemsByKey[childIndex].TryGetValue(key, out var items))
            {
                var array = new T[items.Count];
                for (var i = 0; i < array.Length; i++)
                    array[i] = (T)items[i];
                return array;
            }
            return new T[0];
        }

        private sealed class CompositeKey : IEquatable<CompositeKey>
        {
            private readonly object[] parts;

            public CompositeKey(object[] parts)
            {
                this.parts = parts;
            }

            public bool Equals(CompositeKey other)
                => other != null && this.parts.SequenceEqual(other.parts);

            public override bool Equals(object obj) => this.Equals(obj as CompositeKey);

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = 17;
                    foreach (var part in this.parts)
                        hash = hash * 31 + part.GetHashCode();
                    return hash;
                }
            }
        }
    }
}
