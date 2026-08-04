using Atis.Orm.Querying;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Atis.Orm
{
    /// <summary>The InsertEntity stage after at least one value has been supplied.</summary>
    public class InsertReadyStage<T>
    {
        private readonly IQueryProvider provider;
        private readonly List<FieldValuePair> values;

        internal InsertReadyStage(IQueryProvider provider, IEnumerable<FieldValuePair> values)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
            this.values = values?.ToList() ?? throw new ArgumentNullException(nameof(values));
        }

        /// <summary>Writes <paramref name="value"/> into the column selected by <paramref name="fieldSelector"/>.</summary>
        public InsertReadyStage<T> Value<FT>(Expression<Func<T, FT>> fieldSelector, Expression<Func<FT>> value)
        {
            if (fieldSelector is null)
                throw new ArgumentNullException(nameof(fieldSelector));
            if (value is null)
                throw new ArgumentNullException(nameof(value));
            this.values.Add(new FieldValuePair(fieldSelector, value));
            return this;
        }

        /// <summary>
        ///     Moves to the output stage, returning <paramref name="outputSelector"/>'s column from the
        ///     inserted row image. The values collected so far are snapshotted, so a later
        ///     <see cref="Value{FT}"/> on this stage does not reach the returned one.
        /// </summary>
        public InsertOutputStage<T> Output<FT>(Expression<Func<T, FT>> outputSelector)
        {
            return new InsertOutputStage<T>(this.provider, this.values.ToList()).Output(outputSelector);
        }

        /// <summary>Executes the insert and returns the affected row count.</summary>
        public int Execute()
        {
            var insertCall = InsertEntityMethodCallFactory.CreateAffectedRowsCall<T>(this.values);
            return this.provider.Execute<int>(insertCall);
        }

        /// <summary>The asynchronous <see cref="Execute"/>. Throws synchronously if the provider is not asynchronous.</summary>
        public Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var insertCall = InsertEntityMethodCallFactory.CreateAffectedRowsCall<T>(this.values);
            return this.provider.RequireAsync().ExecuteAsync<Task<int>>(insertCall, cancellationToken);
        }
    }
}
