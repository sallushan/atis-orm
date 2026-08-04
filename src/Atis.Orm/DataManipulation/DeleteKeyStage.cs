using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Atis.Orm
{
    /// <summary>
    ///     The initial DeleteEntity stage. It has no terminal, so a delete without a key — which
    ///     would empty the table — cannot be written.
    /// </summary>
    public class DeleteKeyStage<T>
    {
        private readonly IQueryProvider provider;

        internal DeleteKeyStage(IQueryProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>Restricts the delete to rows whose selected key column equals <paramref name="value"/>.</summary>
        public DeleteReadyStage<T> Key<KT>(Expression<Func<T, KT>> keySelector, Expression<Func<KT>> value)
        {
            if (keySelector is null)
                throw new ArgumentNullException(nameof(keySelector));
            if (value is null)
                throw new ArgumentNullException(nameof(value));

            return new DeleteReadyStage<T>(
                this.provider,
                new List<FieldValuePair> { new FieldValuePair(keySelector, value) });
        }
    }
}
