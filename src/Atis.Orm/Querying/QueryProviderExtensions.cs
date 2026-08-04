using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Atis.Orm.Abstractions;
namespace Atis.Orm.Querying
{
    internal static class QueryProviderExtensions
    {
        /// <summary>
        ///     <para>
        ///         The provider an asynchronous terminal needs. A fluent builder is handed a plain
        ///         <see cref="IQueryProvider"/> and only the async terminals require more, so the
        ///         demand is made here rather than when the builder is created — a caller that never
        ///         awaits anything must keep working on a synchronous provider.
        ///     </para>
        /// </summary>
        public static IAsyncQueryProvider RequireAsync(this IQueryProvider provider)
        {
            if (provider is null)
                throw new ArgumentNullException(nameof(provider));

            return provider as IAsyncQueryProvider
                ?? throw new InvalidOperationException("The query provider does not support asynchronous operations.");
        }

        /// <summary>
        ///     <para>
        ///         Reads an asynchronous result sequence to the end. Every asynchronous terminal that
        ///         returns a materialized collection ends this way, so the enumerator's disposal — which
        ///         is what releases the reader and the connection span behind it — is written once here
        ///         rather than at each terminal.
        ///     </para>
        /// </summary>
        public static async Task<List<T>> DrainAsync<T>(this IAsyncEnumerable<T> source, CancellationToken cancellationToken)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));

            var items = new List<T>();
            var enumerator = source.GetAsyncEnumerator(cancellationToken);
            try
            {
                while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                    items.Add(enumerator.Current);
            }
            finally
            {
                await enumerator.DisposeAsync().ConfigureAwait(false);
            }
            return items;
        }
    }
}
