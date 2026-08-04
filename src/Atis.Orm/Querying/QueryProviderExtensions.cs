using System;
using System.Linq;

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
    }
}
