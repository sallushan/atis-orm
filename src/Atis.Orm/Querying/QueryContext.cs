using System;
using System.Collections.Generic;

using Atis.Orm.Abstractions;

namespace Atis.Orm.Querying
{
    /// <summary>
    ///     <para>
    ///         Default <see cref="IQueryContext"/>: a plain dictionary the application fills in, for example
    ///         once per request. Applications with their own ambient state (an HTTP context, a claims
    ///         principal) register their own <see cref="IQueryContext"/> instead.
    ///     </para>
    /// </summary>
    public class QueryContext : IQueryContext
    {
        private readonly Dictionary<string, object> values = new Dictionary<string, object>();

        /// <summary>Sets the value returned for <paramref name="key"/>. May be <c>null</c>.</summary>
        public QueryContext Set(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("A context key must not be blank.", nameof(key));
            this.values[key] = value;
            return this;
        }

        /// <summary>Removes the value for <paramref name="key"/>, if any.</summary>
        public bool Remove(string key) => this.values.Remove(key);

        /// <inheritdoc />
        public bool TryGetValue(string key, out object value) => this.values.TryGetValue(key, out value);
    }
}
