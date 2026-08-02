using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Atis.SqlExpressionEngine.ExpressionExtensions;

namespace Atis.SqlExpressionEngine
{

    /// <summary>
    ///     The first stage of <c>UpdateEntity&lt;T&gt;</c>: collecting the columns to write.
    /// </summary>
    public class UpdateSetStage<T>
    {
        private readonly IQueryProvider provider;
        private readonly List<FieldValuePair> setters = new List<FieldValuePair>();

        internal UpdateSetStage(IQueryProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>Assigns <paramref name="value"/> to the column <paramref name="fieldSelector"/> names.</summary>
        public UpdateSetStage<T> Set<FT>(Expression<Func<T, FT>> fieldSelector, Expression<Func<FT>> value)
        {
            if (fieldSelector is null)
                throw new ArgumentNullException(nameof(fieldSelector));
            if (value is null)
                throw new ArgumentNullException(nameof(value));

            this.setters.Add(new FieldValuePair(fieldSelector, value));
            return this;
        }

        /// <summary>Restricts the update to rows whose <paramref name="keySelector"/> column equals <paramref name="value"/>.</summary>
        public UpdateKeyStage<T> Key<KT>(Expression<Func<T, KT>> keySelector, Expression<Func<KT>> value)
        {
            // The setters are snapshotted: the next stage must not change under a later Set on this one.
            return new UpdateKeyStage<T>(this.provider, this.setters.ToList()).Key(keySelector, value);
        }
    }
}
