using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Atis.SqlExpressionEngine.ExpressionExtensions;

namespace Atis.SqlExpressionEngine
{

    /// <summary>
    ///     The stage after at least one <c>Key</c>, so the update is filtered to rows and can be run.
    /// </summary>
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

        /// <summary>Adds a further key column; keys are combined with AND.</summary>
        public UpdateKeyStage<T> Key<KT>(Expression<Func<T, KT>> keySelector, Expression<Func<KT>> value)
        {
            if (keySelector is null)
                throw new ArgumentNullException(nameof(keySelector));
            if (value is null)
                throw new ArgumentNullException(nameof(value));

            this.keys.Add(new FieldValuePair(keySelector, value));
            return this;
        }

        /// <summary>Asks for <paramref name="outputSelector"/>'s column to be returned from the updated rows.</summary>
        public UpdateOutputStage<T> Output<KT>(Expression<Func<T, KT>> outputSelector)
        {
            return new UpdateOutputStage<T>(this.provider, this.setters, this.keys.ToList()).Output(outputSelector);
        }

        /// <summary>Runs the update and returns the number of rows affected.</summary>
        public int Execute()
        {
            // No output clause, so the statement yields the affected-row count.
            var updateEntityExpression = UpdateEntityExpressionFactory.Create(
                typeof(int), typeof(T), this.setters, this.keys, outputs: null);
            return this.provider.Execute<int>(updateEntityExpression);
        }
    }
}
