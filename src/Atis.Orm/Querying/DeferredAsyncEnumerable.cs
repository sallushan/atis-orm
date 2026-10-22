using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Atis.Orm.Querying
{
    /// <summary>
    ///     <para>
    ///         An <see cref="IAsyncEnumerable{T}"/> that gets the sequence it enumerates by awaiting a function the first
    ///         time it is advanced. It lets something asynchronous (loading the child collections of a query) happen
    ///         before the sequence is opened, although the caller was handed the sequence synchronously.
    ///     </para>
    /// </summary>
    internal sealed class DeferredAsyncEnumerable<T> : IAsyncEnumerable<T>
    {
        private readonly Func<CancellationToken, Task<IAsyncEnumerable<T>>> source;

        public DeferredAsyncEnumerable(Func<CancellationToken, Task<IAsyncEnumerable<T>>> source)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            => new Enumerator(this.source, cancellationToken);

        private sealed class Enumerator : IAsyncEnumerator<T>
        {
            private readonly Func<CancellationToken, Task<IAsyncEnumerable<T>>> source;
            private readonly CancellationToken cancellationToken;
            private IAsyncEnumerator<T> inner;

            public Enumerator(Func<CancellationToken, Task<IAsyncEnumerable<T>>> source, CancellationToken cancellationToken)
            {
                this.source = source;
                this.cancellationToken = cancellationToken;
            }

            public T Current => this.inner != null ? this.inner.Current : default;

            public async ValueTask<bool> MoveNextAsync()
            {
                if (this.inner is null)
                {
                    var sequence = await this.source(this.cancellationToken).ConfigureAwait(false);
                    this.inner = sequence.GetAsyncEnumerator(this.cancellationToken);
                }
                return await this.inner.MoveNextAsync().ConfigureAwait(false);
            }

            public ValueTask DisposeAsync()
                => this.inner is null ? default : this.inner.DisposeAsync();
        }
    }
}
