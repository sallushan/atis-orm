using Atis.Expressions;
using Atis.SqlExpressionEngine;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.ExpressionConverters;
using Atis.SqlExpressionEngine.Preprocessors;
using Atis.SqlExpressionEngine.Services;
using Atis.SqlExpressionEngine.SqlExpressions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;

using Atis.Orm.Abstractions;
using Atis.Orm.DataAccess;
using Atis.Orm.Metadata;
using Atis.Orm.Querying;
namespace Atis.Orm
{
    /// <summary>
    /// 
    /// </summary>
    public class DataContext : IDisposable
    {
        private readonly DataContextConfiguration _config;
        private IServiceScope _serviceScope;
        private IServiceProvider _serviceProvider;

        protected DataContext() : this(new DataContextConfiguration()) { }

        public DataContext(DataContextConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        private IServiceProvider ServiceProvider
        {
            get
            {
                if (_serviceProvider == null)
                {
                    OnConfiguring(_config);
                    _serviceScope = OrmServiceManager.Instance
                        .GetOrAdd(_config)
                        .GetRequiredService<IServiceScopeFactory>()
                        .CreateScope();
                    _serviceProvider = _serviceScope.ServiceProvider;
                }
                return _serviceProvider;
            }
        }

        protected virtual void OnConfiguring(DataContextConfiguration config) { }
        protected virtual void OnModelCreating(ModelBuilder mb) { }


        private IEntityMetadataBuilder _metadataBuilder;
        /// <summary>
        /// 
        /// </summary>
        protected IEntityMetadataBuilder MetadataBuilder
        {
            get
            {
                if (this._metadataBuilder is null)
                {
                    this._metadataBuilder = this.ServiceProvider.GetRequiredService<IEntityMetadataBuilder>();
                }
                return this._metadataBuilder;
            }
        }

        private IOrmModel _ormModel;
        /// <summary>
        ///     <para>
        ///         Gets the <see cref="IOrmModel"/> for this context, building it via
        ///         <see cref="OnModelCreating(ModelBuilder)"/> on first access.
        ///     </para>
        ///     <para>
        ///         <see cref="IOrmModel"/> is a singleton within its root <see cref="IServiceProvider"/>,
        ///         and that provider is cached per configuration type + extension set (not per
        ///         <see cref="DataContext"/> subclass). As a result, contexts that share the same
        ///         configuration type and extensions share one model, and only the first context to
        ///         access this property runs its <see cref="OnModelCreating(ModelBuilder)"/>. To isolate
        ///         the model per context, use a distinct <see cref="DataContextConfiguration"/> subclass.
        ///         See docs/ServiceProviderCachingAndModelLifetime.md.
        ///     </para>
        /// </summary>
        protected IOrmModel Model
        {
            get
            {
                if (this._ormModel is null)
                {
                    this._ormModel = this.ServiceProvider.GetRequiredService<IOrmModel>();
                    this._ormModel.EnsureModelInitialized(() =>
                    {
                        var metadataBuilder = this.ServiceProvider.GetRequiredService<IEntityMetadataBuilder>();
                        var mb = new ModelBuilder(metadataBuilder, this._ormModel);
                        this.OnModelCreating(mb);
                        mb.Build();
                    });
                }
                return this._ormModel;
            }
        }


        private IAsyncQueryProvider _queryProvider;
        /// <summary>
        ///
        /// </summary>
        protected IAsyncQueryProvider QueryProvider
        {
            get
            {
                if (this._queryProvider is null)
                {
                    this._queryProvider = this.ServiceProvider.GetRequiredService<IAsyncQueryProvider>();
                }
                return this._queryProvider;
            }
        }


        private INavigationInitializer _navigationInitializer;
        /// <summary>
        ///     Gets the <see cref="INavigationInitializer"/> for this context. It populates the lazy
        ///     navigation properties of materialized entities during query execution.
        /// </summary>
        protected INavigationInitializer NavigationInitializer
        {
            get
            {
                if (this._navigationInitializer is null)
                {
                    this._navigationInitializer = this.ServiceProvider.GetRequiredService<INavigationInitializer>();
                }
                return this._navigationInitializer;
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public virtual IQueryable<T> CreateQuery<T>()
        {
            this.Model.GetOrAdd(typeof(T), t => this.MetadataBuilder.Build(t));
            return new OrmQueryable<T>(this.QueryProvider);
        }

        public string TranslateToSql<T>(IQueryable<T> query)
        {
            if (query is null)
                throw new ArgumentNullException(nameof(query));
            var queryTranslator = this.ServiceProvider.GetRequiredService<IQueryTranslator>();
            var queryTranslationResult = queryTranslator.Translate(query.Expression);
            return queryTranslationResult.SqlTranslation.Sql;
        }

        /// <inheritdoc />
        public void Dispose() => _serviceScope?.Dispose();
    }
}
