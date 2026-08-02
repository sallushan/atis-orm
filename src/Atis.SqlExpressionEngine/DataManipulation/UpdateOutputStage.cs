using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Atis.SqlExpressionEngine.ExpressionExtensions;

namespace Atis.SqlExpressionEngine
{

    /// <summary>
    ///     The stage after at least one <c>Output</c>: the update returns columns from the rows it wrote.
    /// </summary>
    public class UpdateOutputStage<T>
    {
        private readonly IQueryProvider provider;
        private readonly IReadOnlyList<FieldValuePair> setters;
        private readonly IReadOnlyList<FieldValuePair> keys;
        private readonly List<Expression> outputs = new List<Expression>();

        internal UpdateOutputStage(IQueryProvider provider, IReadOnlyList<FieldValuePair> setters, IReadOnlyList<FieldValuePair> keys)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
            this.setters = setters ?? throw new ArgumentNullException(nameof(setters));
            this.keys = keys ?? throw new ArgumentNullException(nameof(keys));
        }

        /// <summary>
        ///     <para>
        ///         Adds a further column to return. Generic in the column type rather than taking
        ///         <c>Expression&lt;Func&lt;T, object&gt;&gt;</c>: an <c>object</c> selector boxes a
        ///         value-type column behind a <c>Convert</c> node, which hides the member access the
        ///         output alias is taken from.
        ///     </para>
        /// </summary>
        public UpdateOutputStage<T> Output<KT>(Expression<Func<T, KT>> outputSelector)
        {
            if (outputSelector is null)
                throw new ArgumentNullException(nameof(outputSelector));

            this.outputs.Add(outputSelector);
            return this;
        }

        /// <summary>
        ///     Runs the update and returns one dictionary per updated row, keyed by output column name
        ///     (matched case-insensitively), with a <c>NULL</c> column coming back as <c>null</c>.
        /// </summary>
        public IReadOnlyList<IReadOnlyDictionary<string, object>> ExecuteDictionary()
        {
            // Typed as the queryable it is handed to, not as the collection the caller gets back:
            // IQueryable<T>.Expression.Type must be assignable to IQueryable<T>, and the materializer
            // reads the element type from here to decide what to build.
            var updateEntityExpression = UpdateEntityExpressionFactory.Create(
                typeof(IQueryable<Dictionary<string, object>>), typeof(T), this.setters, this.keys, this.outputs);
            var query = this.provider.CreateQuery<Dictionary<string, object>>(updateEntityExpression);
            return new List<Dictionary<string, object>>(query);
        }
    }
}
