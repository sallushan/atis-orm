namespace Atis.Orm
{
    /// <summary>
    ///     <para>
    ///         How far a streamed column write has got, reported by
    ///         <see cref="DataContext.SaveWithProgress{T}(T, System.Action{ColumnWriteProgress}, System.Action{StreamColumnMap{T}}, int)"/>
    ///         after every chunk it sends.
    ///     </para>
    ///     <para>
    ///         The counts are in the unit the column is written in: bytes for a binary column,
    ///         characters for a text column. A text value is chunked by character, not by encoded byte,
    ///         because that is the unit the database appends it in.
    ///     </para>
    /// </summary>
    public sealed class ColumnWriteProgress
    {
        /// <summary>Constructs the progress report.</summary>
        public ColumnWriteProgress(string columnName, int columnIndex, int totalColumns, long bytesWritten, long? totalBytes)
        {
            this.ColumnName = columnName;
            this.ColumnIndex = columnIndex;
            this.TotalColumns = totalColumns;
            this.BytesWritten = bytesWritten;
            this.TotalBytes = totalBytes;
        }

        /// <summary>The entity member being written.</summary>
        public string ColumnName { get; }

        /// <summary>Which of the streamed columns this is, starting at 1.</summary>
        public int ColumnIndex { get; }

        /// <summary>How many columns this save is streaming.</summary>
        public int TotalColumns { get; }

        /// <summary>How much of this column has been written so far.</summary>
        public long BytesWritten { get; }

        /// <summary>
        ///     How much there is to write, or <c>null</c> when that cannot be known in advance — a
        ///     <see cref="System.IO.Stream"/> that cannot seek, or any <see cref="System.IO.TextReader"/>.
        /// </summary>
        public long? TotalBytes { get; }
    }
}
