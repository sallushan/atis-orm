using System;
using System.Threading;
using System.Threading.Tasks;

namespace Atis.Orm.DataAccess
{
    /// <summary>
    ///     <para>
    ///         A result set being read, and everything holding it open: the connection claim, the command
    ///         and the reader. Handed out by <see cref="IDbCommunication.OpenReader"/> and given up again
    ///         by disposing it -- one object to hold, one thing to dispose, in whichever order the pieces
    ///         underneath actually need.
    ///     </para>
    ///     <para>
    ///         Rows come back as finished objects, built by the element factory the session was opened
    ///         with: <see cref="Read"/> moves to the next row and <see cref="Current"/> is what that row
    ///         maps to. The reader itself never leaves the session -- it is passed to the factory,
    ///         positioned on the current row, and is not otherwise reachable. A caller that genuinely wants
    ///         the raw reader asks for it by opening the session with an identity factory
    ///         (<c>r =&gt; r</c>), which is a deliberate choice rather than something the API invites.
    ///     </para>
    ///     <para>
    ///         Disposal releases the connection claim, the command and the reader, and does so even if the
    ///         reader throws on its way out, so a caller that disposes the session never has to reason
    ///         about a half-released connection. Dispose it once; the second call does nothing, and mixing
    ///         <see cref="IDisposable.Dispose"/> with <see cref="IAsyncDisposable.DisposeAsync"/> on one
    ///         session is safe for the same reason.
    ///     </para>
    ///     <para>
    ///         Whether a second command may run while this session is open is the driver's business:
    ///         SQL Server needs <c>MultipleActiveResultSets=True</c> and otherwise fails with its own
    ///         "there is already an open DataReader" error, some providers allow it outright, and others
    ///         never do. The connection is reference counted, so a command running meanwhile releases only
    ///         its own claim and leaves this session's alone.
    ///     </para>
    /// </summary>
    public interface IDbReaderSession : IDisposable, IAsyncDisposable
    {
        /// <summary>
        ///     Advances to the next row, returning <c>false</c> once there are none left.
        /// </summary>
        bool Read();

        /// <summary>The asynchronous <see cref="Read"/>.</summary>
        Task<bool> ReadAsync(CancellationToken cancellationToken);

        /// <summary>
        ///     <para>
        ///         The row <see cref="Read"/> last moved to, mapped by the element factory. Built on first
        ///         access and remembered until the next <see cref="Read"/>, so a row nobody looks at is
        ///         never materialized and a row looked at twice is only materialized once.
        ///     </para>
        ///     <para>
        ///         Valid only between a <see cref="Read"/> that returned <c>true</c> and the following one.
        ///     </para>
        /// </summary>
        object Current { get; }
    }
}
