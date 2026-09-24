using System;
using System.Collections.Generic;
using System.Reflection;

namespace Atis.Orm.DataManipulation
{
    /// <summary>
    ///     <para>
    ///         Tells an <see cref="Abstractions.IEntityPersister"/> to write an entity's large columns in
    ///         chunks, after the statement that writes the rest of the row, rather than inside it.
    ///     </para>
    ///     <para>
    ///         A column is streamed for one of two reasons. Either the caller supplied a
    ///         <see cref="ColumnWriteSource"/> for it (see <see cref="Sources"/>), or it is a <c>byte[]</c>
    ///         or <c>string</c> member whose value is larger than <see cref="ChunkSizeBytes"/>. The second
    ///         case does not save memory — the value is already in memory — but it does keep every command
    ///         small, so none of them runs into the command timeout, and it makes progress reportable.
    ///     </para>
    /// </summary>
    public sealed class StreamedColumnWrite
    {
        /// <summary>The chunk size used when the caller does not pick one: 1 MB.</summary>
        public const int DefaultChunkSizeBytes = 1024 * 1024;

        /// <summary>Constructs the write options.</summary>
        /// <param name="chunkSizeBytes">
        ///     Both the size of each chunk sent and the size above which an in-memory value is streamed.
        ///     A text chunk is half this many characters, since a character is two bytes on the wire.
        /// </param>
        /// <param name="onProgress">Called after every chunk. May be <c>null</c>.</param>
        /// <param name="sources">The caller-supplied sources, by the member each one writes. May be <c>null</c>.</param>
        public StreamedColumnWrite(
            int chunkSizeBytes,
            Action<ColumnWriteProgress> onProgress,
            IReadOnlyList<KeyValuePair<MemberInfo, ColumnWriteSource>> sources)
        {
            // Two bytes is the smallest size that still holds one character of a text chunk.
            if (chunkSizeBytes < 2)
                throw new ArgumentOutOfRangeException(nameof(chunkSizeBytes), chunkSizeBytes, "The chunk size must be at least 2 bytes.");

            this.ChunkSizeBytes = chunkSizeBytes;
            this.OnProgress = onProgress;
            this.Sources = sources ?? Array.Empty<KeyValuePair<MemberInfo, ColumnWriteSource>>();

            foreach (var source in this.Sources)
            {
                if (source.Value is null)
                    throw new ArgumentException($"The source for '{source.Key.Name}' is null.", nameof(sources));
            }
        }

        /// <summary>See the constructor.</summary>
        public int ChunkSizeBytes { get; }

        /// <summary>Called after every chunk. May be <c>null</c>.</summary>
        public Action<ColumnWriteProgress> OnProgress { get; }

        /// <summary>The caller-supplied sources, in the order they were mapped.</summary>
        public IReadOnlyList<KeyValuePair<MemberInfo, ColumnWriteSource>> Sources { get; }
    }
}
