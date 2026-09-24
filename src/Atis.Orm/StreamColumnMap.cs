using Atis.Orm.DataManipulation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;

namespace Atis.Orm
{
    /// <summary>
    ///     <para>
    ///         Names the columns whose value the caller supplies as a stream rather than as the entity's
    ///         own member value, for
    ///         <see cref="DataContext.SaveWithProgress{T}(T, Action{ColumnWriteProgress}, Action{StreamColumnMap{T}}, int)"/>.
    ///     </para>
    ///     <para>
    ///         A mapped column is read from its source one chunk at a time and never held in memory as a
    ///         whole, which is the point of mapping it: the entity member is ignored, and is left as it
    ///         was. The source stays the caller's — it is read from its current position to its end and
    ///         is not disposed.
    ///     </para>
    /// </summary>
    public sealed class StreamColumnMap<T>
    {
        private readonly List<KeyValuePair<MemberInfo, ColumnWriteSource>> sources = new List<KeyValuePair<MemberInfo, ColumnWriteSource>>();

        internal StreamColumnMap()
        {
        }

        internal IReadOnlyList<KeyValuePair<MemberInfo, ColumnWriteSource>> Sources => this.sources;

        /// <summary>Writes the binary column <paramref name="column"/> from <paramref name="source"/>.</summary>
        public StreamColumnMap<T> Stream(Expression<Func<T, byte[]>> column, Stream source)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));
            return this.Add(column, ColumnWriteSource.FromStream(source));
        }

        /// <summary>Writes the text column <paramref name="column"/> from <paramref name="source"/>.</summary>
        public StreamColumnMap<T> Text(Expression<Func<T, string>> column, TextReader source)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));
            return this.Add(column, ColumnWriteSource.FromReader(source));
        }

        private StreamColumnMap<T> Add(LambdaExpression column, ColumnWriteSource source)
        {
            if (column is null)
                throw new ArgumentNullException(nameof(column));

            var member = EntityLambdaFactory.GetSelectedMember(column);
            foreach (var existing in this.sources)
            {
                if (existing.Key.Name == member.Name)
                    throw new ArgumentException($"'{typeof(T).Name}.{member.Name}' is mapped to a stream more than once.", nameof(column));
            }
            this.sources.Add(new KeyValuePair<MemberInfo, ColumnWriteSource>(member, source));
            return this;
        }
    }
}
