using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

using Atis.Orm.DataAccess;
using Atis.SqlExpressionEngine;

namespace Atis.Orm.Querying
{
    /// <summary>
    ///     What the four streaming terminals in <see cref="OrmQueryExtensions"/> share: turning the
    ///     caller's selectors into the query that is actually run, taking the value off the reader, and
    ///     making sure a session that turns out to have nothing to hand back is not left holding the
    ///     connection.
    /// </summary>
    internal static class FieldStreamSupport
    {
        /// <summary>
        ///     <para>
        ///         The query the terminals run: <c>SelectFields</c> over the one or two members named,
        ///         limited to a single row.
        ///     </para>
        ///     <para>
        ///         <c>SelectFields</c> rather than <c>Select</c> because the column list is decided here at
        ///         run time from what the caller passed, which is the case it was built for, and because it
        ///         is already restricted to plain member selectors -- the same restriction that applies
        ///         here, since a computed value is not something a database can stream.
        ///     </para>
        ///     <para>
        ///         <strong>The size column comes first, deliberately.</strong> The reader is sequential, so
        ///         a column can only be read before the reader has moved past it, and reading the value is
        ///         what moves it. Ordering the projection is the only thing that makes the size readable at
        ///         all.
        ///     </para>
        ///     <para>
        ///         <strong>And the row limit is in the SQL, not applied afterwards.</strong> Whether a
        ///         second row exists cannot be checked here: finding out means advancing the reader, and
        ///         advancing the reader is what invalidates the stream that has just been handed out. So
        ///         the statement is made to return one row rather than the result being trimmed to one.
        ///     </para>
        /// </summary>
        public static Expression BuildQuery<T>(IQueryable<T> query, LambdaExpression field, LambdaExpression size)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var selected = new List<Expression>(2);
            if (size != null)
                selected.Add(SelectMember<T>(size, parameter));
            selected.Add(SelectMember<T>(field, parameter));

            var fields = Expression.Lambda<Func<T, object[]>>(
                Expression.NewArrayInit(typeof(object), selected),
                parameter);

            return query.SelectFields(fields).Take(1).Expression;
        }

        /// <summary>
        ///     Rebuilds one selector as a member access on the shared parameter. Each selector arrives
        ///     written against its own parameter, and a widening conversion the compiler inserted -- an
        ///     <c>int</c> size column read as <c>long</c> -- is dropped here rather than left for the
        ///     translator to see through.
        /// </summary>
        private static Expression SelectMember<T>(LambdaExpression selector, ParameterExpression parameter)
        {
            var member = EntityLambdaFactory.GetSelectedMember(selector);
            return Expression.Convert(Expression.MakeMemberAccess(parameter, member), typeof(object));
        }

        /// <summary>
        ///     <para>
        ///         Takes the size and the value off the row the session is positioned on. Returned as a
        ///         <see cref="FieldStreamRow"/> rather than the value alone because the size has to be read
        ///         first and there is only one chance to do it.
        ///     </para>
        ///     <para>
        ///         The cast to <see cref="DbDataReader"/> is what the whole feature rests on:
        ///         <c>GetStream</c> and <c>GetTextReader</c> exist only there, while the element factory
        ///         contract is written in terms of <see cref="IDataReader"/>, which has neither. Every
        ///         reader the ORM opens is a <see cref="DbDataReader"/>, so this is a cast rather than a
        ///         widening of that contract for one caller.
        ///     </para>
        /// </summary>
        public static Func<IDataReader, object> CreateReaderFactory(bool hasSize, bool binary)
        {
            var valueOrdinal = hasSize ? 1 : 0;

            return dr =>
            {
                if (!(dr is DbDataReader reader))
                {
                    throw new NotSupportedException(
                        $"Streaming a column needs a {nameof(DbDataReader)}, but this query was read with " +
                        $"'{dr?.GetType().Name ?? "null"}'. Only {nameof(DbDataReader)} offers GetStream and " +
                        "GetTextReader, so a provider handing back anything else cannot stream a column.");
                }

                long? size = null;
                if (hasSize && !reader.IsDBNull(0))
                    size = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);

                if (reader.IsDBNull(valueOrdinal))
                    return new FieldStreamRow(size, null);

                var value = binary
                        ? (object)reader.GetStream(valueOrdinal)
                        : reader.GetTextReader(valueOrdinal);
                return new FieldStreamRow(size, value);
            };
        }

        /// <summary>
        ///     <para>
        ///         Reads the one row and wraps what it holds, or gives the session back. Both "no row" and
        ///         "the column is null" come out as <c>null</c>: the terminals return a live handle on a
        ///         value, and the absence of a value is one situation to the caller however it arose. A
        ///         caller that needs to tell them apart is asking whether a row exists, which is an
        ///         ordinary query and a far cheaper one than this.
        ///     </para>
        ///     <para>
        ///         Nothing but a successfully wrapped value takes ownership of the session, so every other
        ///         path -- no row, a null column, a failure part way through -- releases the connection
        ///         here rather than leaving it to a caller who has been handed <c>null</c> and has nothing
        ///         to dispose.
        ///     </para>
        /// </summary>
        public static DbFieldStream ReadStream(IDbReaderSession session, IProgress<long> progress)
        {
            var handedOver = false;
            try
            {
                if (!session.Read())
                    return null;

                var row = (FieldStreamRow)session.Current;
                if (row.Value is null)
                    return null;

                var stream = new DbFieldStream(session, (Stream)row.Value, row.Size, progress);
                handedOver = true;
                return stream;
            }
            finally
            {
                if (!handedOver)
                    session.Dispose();
            }
        }

        /// <inheritdoc cref="ReadStream"/>
        public static DbFieldTextReader ReadTextReader(IDbReaderSession session, IProgress<long> progress)
        {
            var handedOver = false;
            try
            {
                if (!session.Read())
                    return null;

                var row = (FieldStreamRow)session.Current;
                if (row.Value is null)
                    return null;

                var textReader = new DbFieldTextReader(session, (TextReader)row.Value, row.Size, progress);
                handedOver = true;
                return textReader;
            }
            finally
            {
                if (!handedOver)
                    session.Dispose();
            }
        }

        /// <inheritdoc cref="ReadStream"/>
        public static async Task<DbFieldStream> ReadStreamAsync(IDbReaderSession session, IProgress<long> progress, CancellationToken cancellationToken)
        {
            var handedOver = false;
            try
            {
                if (!await session.ReadAsync(cancellationToken).ConfigureAwait(false))
                    return null;

                var row = (FieldStreamRow)session.Current;
                if (row.Value is null)
                    return null;

                var stream = new DbFieldStream(session, (Stream)row.Value, row.Size, progress);
                handedOver = true;
                return stream;
            }
            finally
            {
                if (!handedOver)
                    await session.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <inheritdoc cref="ReadStream"/>
        public static async Task<DbFieldTextReader> ReadTextReaderAsync(IDbReaderSession session, IProgress<long> progress, CancellationToken cancellationToken)
        {
            var handedOver = false;
            try
            {
                if (!await session.ReadAsync(cancellationToken).ConfigureAwait(false))
                    return null;

                var row = (FieldStreamRow)session.Current;
                if (row.Value is null)
                    return null;

                var textReader = new DbFieldTextReader(session, (TextReader)row.Value, row.Size, progress);
                handedOver = true;
                return textReader;
            }
            finally
            {
                if (!handedOver)
                    await session.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>The two things a streamed row carries: the value, and how long it was said to be.</summary>
        private sealed class FieldStreamRow
        {
            public FieldStreamRow(long? size, object value)
            {
                this.Size = size;
                this.Value = value;
            }

            /// <summary>The size column's value, or <c>null</c> when none was asked for or it was null.</summary>
            public long? Size { get; }

            /// <summary>The open <see cref="Stream"/> or <see cref="TextReader"/>, or <c>null</c> for a null column.</summary>
            public object Value { get; }
        }
    }
}
