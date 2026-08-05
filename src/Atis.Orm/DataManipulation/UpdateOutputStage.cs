using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

using Atis.Orm.Querying;
namespace Atis.Orm
{
    /// <summary>The stage after at least one Output call.</summary>
    public class UpdateOutputStage<T>
    {
        private readonly IQueryProvider provider;
        private readonly IReadOnlyList<FieldValuePair> setters;
        private readonly IReadOnlyList<FieldValuePair> keys;
        private readonly List<LambdaExpression> outputs = new List<LambdaExpression>();

        internal UpdateOutputStage(IQueryProvider provider, IReadOnlyList<FieldValuePair> setters, IReadOnlyList<FieldValuePair> keys)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
            this.setters = setters ?? throw new ArgumentNullException(nameof(setters));
            this.keys = keys ?? throw new ArgumentNullException(nameof(keys));
        }

        /// <summary>Adds another column to return from the updated row image.</summary>
        public UpdateOutputStage<T> Output<KT>(Expression<Func<T, KT>> outputSelector)
        {
            if (outputSelector is null)
                throw new ArgumentNullException(nameof(outputSelector));

            this.outputs.Add(outputSelector);
            return this;
        }

        // TODO: see if this should be changed to FirstOrDefault instead of List, because update will
        // is designed to update only 1 row.
        /// <summary>Executes the update and returns one dictionary per updated row.</summary>
        public IReadOnlyList<IReadOnlyDictionary<string, object>> ExecuteDictionary()
        {
            var updateCall = UpdateEntityMethodCallFactory.CreateOutputCall<T>(this.setters, this.keys, this.outputs);
            var outputQuery = this.provider.CreateQuery<Dictionary<string, object>>(updateCall);
            return new List<Dictionary<string, object>>(outputQuery);
        }

        /// <summary>
        ///     <para>
        ///         The asynchronous <see cref="ExecuteDictionary"/>. Submits the same expression, but
        ///         asks the provider for the rows directly instead of going through
        ///         <c>CreateQuery</c> — the queryable in the synchronous path exists only to be
        ///         enumerated on the spot, and <c>GetEnumerator</c> hands this very expression straight
        ///         back to the provider.
        ///     </para>
        /// </summary>
        public async Task<IReadOnlyList<IReadOnlyDictionary<string, object>>> ExecuteDictionaryAsync(CancellationToken cancellationToken = default)
        {
            var updateCall = UpdateEntityMethodCallFactory.CreateOutputCall<T>(this.setters, this.keys, this.outputs);
            var outputRows = this.provider.RequireAsync()
                                 .ExecuteAsync<IAsyncEnumerable<Dictionary<string, object>>>(updateCall, cancellationToken);

            return await outputRows.DrainAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
