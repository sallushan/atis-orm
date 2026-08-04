using Atis.Orm.Querying;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Atis.Orm
{
    /// <summary>The DeleteEntity stage after at least one key, so the delete is filtered and can be executed.</summary>
    public class DeleteReadyStage<T>
    {
        private readonly IQueryProvider provider;
        private readonly List<FieldValuePair> keys;

        internal DeleteReadyStage(IQueryProvider provider, IEnumerable<FieldValuePair> keys)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
            this.keys = keys?.ToList() ?? throw new ArgumentNullException(nameof(keys));
        }

        /// <summary>Adds another key equality; multiple keys are combined with AND.</summary>
        public DeleteReadyStage<T> Key<KT>(Expression<Func<T, KT>> keySelector, Expression<Func<KT>> value)
        {
            if (keySelector is null)
                throw new ArgumentNullException(nameof(keySelector));
            if (value is null)
                throw new ArgumentNullException(nameof(value));

            this.keys.Add(new FieldValuePair(keySelector, value));
            return this;
        }

        /// <summary>Executes the delete and returns its affected-row count.</summary>
        public int Execute()
        {
            var deleteCall = DeleteEntityMethodCallFactory.CreateAffectedRowsCall<T>(this.keys);
            return this.provider.Execute<int>(deleteCall);
        }

        /// <summary>The asynchronous <see cref="Execute"/>. Submits the same expression.</summary>
        public Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var deleteCall = DeleteEntityMethodCallFactory.CreateAffectedRowsCall<T>(this.keys);
            return this.provider.RequireAsync().ExecuteAsync<Task<int>>(deleteCall, cancellationToken);
        }
    }
}
