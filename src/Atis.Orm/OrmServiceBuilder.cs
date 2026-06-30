using Atis.DependencyInjection;
using Atis.Expressions;
using Atis.SqlExpressionEngine;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.ExpressionConverters;
using Atis.SqlExpressionEngine.Services;
using Atis.SqlExpressionEngine.SqlExpressions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Atis.Orm
{
    public class OrmServiceBuilder : ServiceBuilderBase
    {
        // Maps each service type to its characteristics
        private static readonly Dictionary<Type, ServiceCharacteristic> _characteristics
            = new Dictionary<Type, ServiceCharacteristic>
            {
                { typeof(ILogger),    new ServiceCharacteristic(ServiceLifetime.Singleton) },
                { typeof(IExpressionEvaluator),    new ServiceCharacteristic(ServiceLifetime.Singleton) },
                { typeof(IReflectionService), new ServiceCharacteristic(ServiceLifetime.Singleton) },
                { typeof(IOrmReflectionService), new ServiceCharacteristic(ServiceLifetime.Singleton) },
                { typeof(IEntityMetadataBuilder),    new ServiceCharacteristic(ServiceLifetime.Singleton) },
                { typeof(IExpressionCacheKeyProvider),    new ServiceCharacteristic(ServiceLifetime.Singleton) },
                { typeof(ICompiledQueryCacheProvider),    new ServiceCharacteristic(ServiceLifetime.Singleton) },
                { typeof(IPreprocessingRequirementTester),    new ServiceCharacteristic(ServiceLifetime.Singleton) },
                { typeof(ISqlDataTypeFactory),    new ServiceCharacteristic(ServiceLifetime.Singleton) },
                { typeof(ISqlExpressionFactory),    new ServiceCharacteristic(ServiceLifetime.Singleton) },
                { typeof(IModel),    new ServiceCharacteristic(ServiceLifetime.Singleton) },
                { typeof(IOrmModel),    new ServiceCharacteristic(ServiceLifetime.Singleton) },
                { typeof(ILinqToSqlConverterFactoryProvider), new ServiceCharacteristic(ServiceLifetime.Singleton) },
                { typeof(IExpressionConverterDependencyProvider),    new ServiceCharacteristic(ServiceLifetime.Scoped) },
                { typeof(IExpressionTreeConverter<Expression, SqlExpression>), new ServiceCharacteristic(ServiceLifetime.Scoped) },
                { typeof(ILinqToSqlConverter),    new ServiceCharacteristic(ServiceLifetime.Scoped) },
                { typeof(ISqlExpressionTranslator),    new ServiceCharacteristic(ServiceLifetime.Singleton) },
                { typeof(IElementFactoryBuilder),    new ServiceCharacteristic(ServiceLifetime.Singleton) },
                { typeof(IExpressionPreprocessorProvider),    new ServiceCharacteristic(ServiceLifetime.Singleton) },
                { typeof(ISqlExpressionPostprocessorProvider),    new ServiceCharacteristic(ServiceLifetime.Singleton) },
                { typeof(IQueryTranslator),    new ServiceCharacteristic(ServiceLifetime.Scoped) },
                { typeof(IQueryCompiler),    new ServiceCharacteristic(ServiceLifetime.Scoped) },
                { typeof(IExpressionVariableValuesExtractor),    new ServiceCharacteristic(ServiceLifetime.Singleton) },
                { typeof(IDbParameterFactory),    new ServiceCharacteristic(ServiceLifetime.Singleton) },
                { typeof(IDatabaseAdapter),    new ServiceCharacteristic(ServiceLifetime.Scoped) },
                { typeof(IDbCommunication),    new ServiceCharacteristic(ServiceLifetime.Scoped) },
                { typeof(IQueryExecutor),    new ServiceCharacteristic(ServiceLifetime.Scoped) },
                { typeof(INavigationInitializer),    new ServiceCharacteristic(ServiceLifetime.Scoped) },
                { typeof(IQueryProvider),    new ServiceCharacteristic(ServiceLifetime.Scoped) },
                { typeof(IAsyncQueryProvider),    new ServiceCharacteristic(ServiceLifetime.Scoped) },

                { typeof(IExpressionPreprocessor), new ServiceCharacteristic(ServiceLifetime.Singleton, allowMultiple: true) },
                { typeof(IExpressionConverterFactory<Expression, SqlExpression>), new ServiceCharacteristic(ServiceLifetime.Singleton, allowMultiple: true) },

                { typeof(ILambdaParameterToDataSourceMapper),    new ServiceCharacteristic(ServiceLifetime.Transient) },
            };

        public OrmServiceBuilder(IServiceCollection serviceCollection) : base(serviceCollection)
        {
        }

        public override void AddCoreServices()
        {
            this.TryAdd<ILogger, Logger>();
            this.TryAdd<IExpressionEvaluator, ExpressionEvaluator>();
            this.TryAdd<IOrmReflectionService, OrmReflectionService>();
            this.TryAdd<IReflectionService>(p => p.GetRequiredService<IOrmReflectionService>());
            this.TryAdd<IEntityMetadataBuilder, EntityMetadataBuilder>();
            this.TryAdd<IExpressionCacheKeyProvider, ExpressionCacheKeyProvider>();
            this.TryAdd<ICompiledQueryCacheProvider, CompiledQueryCacheProvider>();
            this.TryAdd<ICompiledQueryCacheProvider, CompiledQueryCacheProvider>();
            this.TryAdd<IPreprocessingRequirementTester, PreprocessingRequirementTester>();
            this.TryAdd<ISqlDataTypeFactory, SqlDataTypeFactory>();
            this.TryAdd<ISqlExpressionFactory, SqlExpressionFactory>();
            this.TryAdd<IOrmModel, OrmModel>();
            this.TryAdd<IModel>(p => p.GetRequiredService<IOrmModel>());
            this.TryAdd<ILinqToSqlConverterFactoryProvider, LinqToSqlConverterFactoryProvider>();
            this.TryAdd<IExpressionConverterDependencyProvider, ExpressionConverterDependencyProvider>();
            this.TryAdd<IExpressionTreeConverter<Expression, SqlExpression>, LinqToSqlExpressionTreeConverter>();
            this.TryAdd<ILinqToSqlConverter, LinqToSqlConverter>();
            this.TryAdd<IElementFactoryBuilder, ElementFactoryBuilder>();
            this.TryAdd<IExpressionPreprocessorProvider, OrmExpressionPreprocessorProvider>();
            this.TryAdd<ISqlExpressionPostprocessorProvider, SqlExpressionPostprocessorProvider>();
            this.TryAdd<ISqlExpressionTranslator, SqlExpressionTranslatorBase>();
            this.TryAdd<IQueryTranslator, QueryTranslator>();
            this.TryAdd<IQueryCompiler, QueryCompiler>();
            this.TryAdd<IExpressionVariableValuesExtractor, ExpressionVariableValuesExtractor>();
            this.TryAdd<IDatabaseAdapter, DatabaseAdapter>();
            this.TryAdd<IQueryExecutor, QueryExecutor>();
            this.TryAdd<INavigationInitializer, NavigationInitializer>();
            this.TryAdd<IAsyncQueryProvider, OrmQueryProvider>();
            this.TryAdd<IQueryProvider>(p => p.GetRequiredService<IAsyncQueryProvider>());

            this.TryAdd<ILambdaParameterToDataSourceMapper, LambdaParameterToDataSourceMapper>();
        }

        protected override IReadOnlyCollection<Type> GetCoreServiceTypes()
        {
            return new[] { typeof(IOrmModel), typeof(IEntityMetadataBuilder), typeof(IAsyncQueryProvider) };
        }

        protected override ServiceCharacteristic GetServiceCharacteristic(Type serviceType)
        {
            if (_characteristics.TryGetValue(serviceType, out var characteristic))
            {
                return characteristic;
            }
            throw new InvalidOperationException($"Service type '{serviceType.FullName}' is not registered.");
        }

        public OrmServiceBuilder AddConverterFactory<TImplementation>() 
            where TImplementation : class, IExpressionConverterFactory<Expression, SqlExpression>
        {
            this.TryAdd<IExpressionConverterFactory<Expression, SqlExpression>, TImplementation>();
            return this;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <typeparam name="TImplementation"></typeparam>
        /// <returns></returns>
        public OrmServiceBuilder AddCycledScopeConverterDependency<TService, TImplementation>() 
            where TService : class 
            where TImplementation : class, TService
        {
            this.ServiceCollection.AddTransient<TService, TImplementation>();

            return this;
        }
    }
}
