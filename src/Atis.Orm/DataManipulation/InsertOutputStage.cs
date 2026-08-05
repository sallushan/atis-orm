using Atis.Orm.Querying;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Atis.Orm
{
    /// <summary>The InsertEntity stage after at least one output column has been selected.</summary>
    public class InsertOutputStage<T>
    {
        private readonly IQueryProvider provider;
        private readonly IReadOnlyList<FieldValuePair> values;
        private readonly List<LambdaExpression> outputs = new List<LambdaExpression>();

        internal InsertOutputStage(IQueryProvider provider, IReadOnlyList<FieldValuePair> values)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
            this.values = values ?? throw new ArgumentNullException(nameof(values));
        }

        /// <summary>Adds another column to return from the inserted row image.</summary>
        public InsertOutputStage<T> Output<FT>(Expression<Func<T, FT>> outputSelector)
        {
            if (outputSelector is null)
                throw new ArgumentNullException(nameof(outputSelector));
            this.outputs.Add(outputSelector);
            return this;
        }

        // TODO: check if we should be returning FirstOrDefault instead of List<>
        /// <summary>Executes the insert and returns one dictionary per inserted row.</summary>
        public IReadOnlyList<IReadOnlyDictionary<string, object>> ExecuteDictionary()
        {
            var insertCall = InsertEntityMethodCallFactory.CreateOutputCall<T>(this.values, this.outputs);
            var outputQuery = this.provider.CreateQuery<Dictionary<string, object>>(insertCall);
            return new List<Dictionary<string, object>>(outputQuery);
        }

        /// <summary>
        ///     <para>
        ///         The asynchronous <see cref="ExecuteDictionary"/>. Submits the same expression, but
        ///         asks the provider for the rows directly instead of going through <c>CreateQuery</c> —
        ///         the queryable in the synchronous path exists only to be enumerated on the spot, and
        ///         <c>GetEnumerator</c> hands this very expression straight back to the provider.
        ///     </para>
        /// </summary>
        public async Task<IReadOnlyList<IReadOnlyDictionary<string, object>>> ExecuteDictionaryAsync(
            CancellationToken cancellationToken = default)
        {
            var insertCall = InsertEntityMethodCallFactory.CreateOutputCall<T>(this.values, this.outputs);
            var outputRows = this.provider.RequireAsync()
                .ExecuteAsync<IAsyncEnumerable<Dictionary<string, object>>>(insertCall, cancellationToken);

            return await outputRows.DrainAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
