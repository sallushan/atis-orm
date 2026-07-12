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
    /// The <see cref="IAsyncQueryProvider"/> is resolved lazily to avoid a service construction cycle
    /// (<c>OrmQueryProvider -&gt; QueryExecutor -&gt; NavigationInitializer -&gt; QueryableFactory -&gt; provider</c>);
    /// by the time a query is created or executed the provider is fully built.
    /// </remarks>
    public class QueryableFactory : IQueryableFactory
    {
        private readonly IServiceProvider serviceProvider;
        private readonly IOrmModel Model;
        private readonly IEntityMetadataBuilder MetadataBuilder;

        private IAsyncQueryProvider provider;
        private IAsyncQueryProvider Provider =>
            this.provider ??
            (this.provider = (IAsyncQueryProvider)this.serviceProvider.GetService(typeof(IAsyncQueryProvider)));

        /// <summary>
        /// Initializes a new instance of the <see cref="QueryableFactory"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider used to lazily resolve the <see cref="IAsyncQueryProvider"/>.</param>
        /// <param name="model">The model to be used by the factory.</param>
        /// <param name="metadataBuilder">The metadata builder to be used by the factory.</param>
        public QueryableFactory(IServiceProvider serviceProvider, IOrmModel model, IEntityMetadataBuilder metadataBuilder)
        {
            this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            this.Model = model;
            this.MetadataBuilder = metadataBuilder;
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
            this.Model.GetOrAdd(typeof(T), t => this.MetadataBuilder.Build(t));
            if(expression == null)
                return new OrmQueryable<T>(this.Provider);
            else
                return new OrmQueryable<T>(this.Provider, expression);
        }
    }
}
