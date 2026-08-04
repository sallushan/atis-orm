using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Atis.Orm
{
    /// <summary>The initial InsertEntity stage; one Value is required before execution is available.</summary>
    public class InsertValueStage<T>
    {
        private readonly IQueryProvider provider;

        internal InsertValueStage(IQueryProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>Writes <paramref name="value"/> into the column selected by <paramref name="fieldSelector"/>.</summary>
        public InsertReadyStage<T> Value<FT>(Expression<Func<T, FT>> fieldSelector, Expression<Func<FT>> value)
        {
            if (fieldSelector is null)
                throw new ArgumentNullException(nameof(fieldSelector));
            if (value is null)
                throw new ArgumentNullException(nameof(value));

            return new InsertReadyStage<T>(
                this.provider,
                new List<FieldValuePair> { new FieldValuePair(fieldSelector, value) });
        }
    }
}
