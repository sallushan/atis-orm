using Atis.Orm.DataManipulation;
using System.Threading;
using System.Threading.Tasks;

namespace Atis.Orm.Abstractions
{
    /// <summary>
    ///     <para>
    ///         Writes a single entity to the database, driven by its mapping metadata: which columns take
    ///         part in an Insert or an Update, which identify the row, which the database owns and must
    ///         therefore be read back into the entity afterwards.
    ///     </para>
    ///     <para>
    ///         It builds the same <c>QueryExtensions.Insert</c> / <c>Update</c> / <c>Delete</c> calls the
    ///         fluent stages build, straight from the entity's metadata. The fluent API remains the way to
    ///         write a statement column by column; this is the way to write a whole entity.
    ///     </para>
    /// </summary>
    public interface IEntityPersister
    {
        /// <summary>
        ///     Inserts <paramref name="entity"/>, writing every insertable column, and assigns any
        ///     database generated value back onto it. Returns the number of rows inserted.
        /// </summary>
        int Insert<T>(T entity);

        /// <summary>The asynchronous <see cref="Insert{T}(T)"/>.</summary>
        Task<int> InsertAsync<T>(T entity, CancellationToken cancellationToken = default);

        /// <summary>
        ///     <para>
        ///         Updates the row <paramref name="entity"/>'s primary key identifies, writing every
        ///         updatable column, and assigns any database generated value back onto it. Returns the
        ///         number of rows updated.
        ///     </para>
        /// </summary>
        /// <param name="optimisticConcurrency">
        ///     When <c>true</c>, the entity's row version columns join the <c>WHERE</c> clause, so an
        ///     update loses — affecting no rows — if the row changed since it was read. An entity with no
        ///     row version column has nothing to compare and is last-writer-wins regardless of this flag.
        /// </param>
        int Update<T>(T entity, bool optimisticConcurrency);

        /// <summary>The asynchronous <see cref="Update{T}(T, bool)"/>.</summary>
        Task<int> UpdateAsync<T>(T entity, bool optimisticConcurrency, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Deletes the row <paramref name="entity"/>'s primary key identifies. Returns the number of
        ///     rows deleted. <paramref name="optimisticConcurrency"/> behaves as it does on
        ///     <see cref="Update{T}(T, bool)"/>.
        /// </summary>
        int Delete<T>(T entity, bool optimisticConcurrency);

        /// <summary>The asynchronous <see cref="Delete{T}(T, bool)"/>.</summary>
        Task<int> DeleteAsync<T>(T entity, bool optimisticConcurrency, CancellationToken cancellationToken = default);

        /// <summary>
        ///     <para>
        ///         <see cref="Insert{T}(T)"/>, with the large columns <paramref name="streaming"/> selects
        ///         written in chunks after the row, inside one transaction with it. With nothing to stream
        ///         it is a plain <see cref="Insert{T}(T)"/>.
        ///     </para>
        ///     <para>
        ///         The entity's streamed members are left holding what the caller gave them.
        ///     </para>
        /// </summary>
        int Insert<T>(T entity, StreamedColumnWrite streaming);

        /// <summary>The asynchronous <see cref="Insert{T}(T, StreamedColumnWrite)"/>.</summary>
        Task<int> InsertAsync<T>(T entity, StreamedColumnWrite streaming, CancellationToken cancellationToken = default);

        /// <summary>
        ///     <see cref="Update{T}(T, bool)"/>, with the large columns written in chunks as
        ///     <see cref="Insert{T}(T, StreamedColumnWrite)"/> describes. When the update matches no row,
        ///     no chunk is written.
        /// </summary>
        int Update<T>(T entity, bool optimisticConcurrency, StreamedColumnWrite streaming);

        /// <summary>The asynchronous <see cref="Update{T}(T, bool, StreamedColumnWrite)"/>.</summary>
        Task<int> UpdateAsync<T>(
            T entity, bool optimisticConcurrency, StreamedColumnWrite streaming, CancellationToken cancellationToken = default);
    }
}
