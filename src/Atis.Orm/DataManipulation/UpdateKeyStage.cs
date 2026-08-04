using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

using Atis.Orm.Querying;
namespace Atis.Orm
{
    /// <summary>The stage after at least one Key, so the update is filtered and can be executed.</summary>
    public class UpdateKeyStage<T>
    {
        private readonly IQueryProvider provider;
        private readonly IReadOnlyList<FieldValuePair> setters;
        private readonly List<FieldValuePair> keys = new List<FieldValuePair>();

        internal UpdateKeyStage(IQueryProvider provider, IReadOnlyList<FieldValuePair> setters)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
            this.setters = setters ?? throw new ArgumentNullException(nameof(setters));
        }

        /// <summary>Adds another key equality; multiple keys are combined with AND.</summary>
        public UpdateKeyStage<T> Key<KT>(Expression<Func<T, KT>> keySelector, Expression<Func<KT>> value)
        {
            if (keySelector is null)
                throw new ArgumentNullException(nameof(keySelector));
            if (value is null)
                throw new ArgumentNullException(nameof(value));

            this.keys.Add(new FieldValuePair(keySelector, value));
            return this;
        }

        /// <summary>Adds a column to return from the updated row image.</summary>
        public UpdateOutputStage<T> Output<KT>(Expression<Func<T, KT>> outputSelector)
        {
            return new UpdateOutputStage<T>(this.provider, this.setters, this.keys.ToList()).Output(outputSelector);
        }

        /// <summary>Executes the update and returns its affected-row count.</summary>
        public int Execute()
        {
            var updateCall = UpdateEntityMethodCallFactory.CreateAffectedRowsCall<T>(this.setters, this.keys);
            return this.provider.Execute<int>(updateCall);
        }

        /// <summary>The asynchronous <see cref="Execute"/>. Submits the same expression.</summary>
        public Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var updateCall = UpdateEntityMethodCallFactory.CreateAffectedRowsCall<T>(this.setters, this.keys);
            return this.provider.RequireAsync().ExecuteAsync<Task<int>>(updateCall, cancellationToken);
        }
    }
}
