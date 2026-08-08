using System;
using System.Linq;
using System.Linq.Expressions;
using Atis.Orm.Abstractions;
using Atis.SqlExpressionEngine.Abstractions;

namespace Atis.Orm.Querying
{
    /// <summary>
    /// A factory class for creating instances of <see cref="IQueryable{T}"/> with a specified <see cref="IAsyncQueryProvider"/> and expression.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Breaking the construction cycle is the whole reason this type exists. Resolving
    /// <see cref="IAsyncQueryProvider"/> lazily is what cuts
    /// <c>OrmQueryProvider -&gt; QueryExecutor -&gt; NavigationInitializer -&gt; QueryableFactory -&gt; provider</c>;
    /// by the time a query is created or executed the provider is fully built. A consumer that takes the
    /// provider directly instead of this factory reinstates the cycle.
    /// </para>
    /// </remarks>
    public class QueryableFactory : IQueryableFactory
    {
        private readonly IServiceProvider serviceProvider;

        private IAsyncQueryProvider provider;
        private IAsyncQueryProvider Provider =>
            this.provider ??
            (this.provider = (IAsyncQueryProvider)this.serviceProvider.GetService(typeof(IAsyncQueryProvider)));

        /// <summary>
        /// Initializes a new instance of the <see cref="QueryableFactory"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider used to lazily resolve the <see cref="IAsyncQueryProvider"/>.</param>
        public QueryableFactory(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <inheritdoc />
        public IQueryable<T> CreateQueryable<T>()
        {
            return this.CreateQueryableInternal<T>(null);
        }

        /// <inheritdoc />
        public IQueryable<T> CreateQueryable<T>(Expression expression)
        {
            return this.CreateQueryableInternal<T>(expression);
        }

        private IQueryable<T> CreateQueryableInternal<T>(Expression expression)
        {
            if(expression == null)
                return new OrmQueryable<T>(this.Provider);
            else
                return new OrmQueryable<T>(this.Provider, expression);
        }
    }
}
