using Atis.Expressions;
using Atis.SqlExpressionEngine;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.Preprocessors;
using Atis.SqlExpressionEngine.Services;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;

namespace Atis.Orm
{
    /// <summary>
    /// 
    /// </summary>
    public class DataContext
    {
        private readonly IDbCommunication dbCommunication;
        private readonly IDbParameterFactory dbParameterFactory;
        private readonly ILogger logger;
        private readonly List<IExpressionConverterFactory<Expression, SqlExpression>> customConverterFactories;
        private readonly IReadOnlyList<IExpressionPreprocessor> customPreprocessors;

        /// <summary>
        /// 
        /// </summary>
        public DataContext(IDbCommunication dbCommunication, IDbParameterFactory dbParameterFactory, ILogger logger, IReadOnlyList<IExpressionPreprocessor> customPreprocessors)
        {
            this.dbCommunication = dbCommunication;
            this.dbParameterFactory = dbParameterFactory;
            this.logger = logger;
            this.customPreprocessors = customPreprocessors;
        }

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
                    this.Initialize();
                    if (this._metadataBuilder is null)
                    {
                        throw new InvalidOperationException("Metadata builder is not initialized properly.");
                    }
                }
                return this._metadataBuilder;
            }
        }

        private IOrmModel _ormModel;
        /// <summary>
        /// 
        /// </summary>
        protected IOrmModel OrmModel
        {
            get
            {
                if (this._ormModel is null)
                {
                    this.Initialize();
                    if (this._ormModel is null)
                    {
                        throw new InvalidOperationException("ORM model is not initialized properly.");
                    }
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
                    this.Initialize();

                    if (this._queryProvider is null)
                    {
                        throw new InvalidOperationException("Query provider is not initialized properly.");
                    }
                }
                return this._queryProvider;
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public virtual IQueryable<T> CreateQuery<T>()
        {
            this.OrmModel.GetOrAdd(typeof(T), t => this.MetadataBuilder.Build(t));
            return new OrmQueryable<T>(this.QueryProvider);
        }

        // IMPORTANT: below method is only because we haven't implemented the DI injection yet.
        // And they are subject to change.

        /// <summary>
        /// 
        /// </summary>
        protected virtual void Initialize()
        {
            var expressionEvaluator = new ExpressionEvaluator();
            var reflectionService = new OrmReflectionService();
            this._metadataBuilder = new EntityMetadataBuilder(reflectionService);
            var dbAdapter = new DatabaseAdapter(reflectionService, dbCommunication);
            var cacheKeyProvider = new ExpressionCacheKeyProvider();
            var queryCacheProvider = new CompiledQueryCacheProvider(cacheKeyProvider);
            var preprocessingRequirementTester = new PreprocessingRequirementTester();
            var sqlDataTypeFactory = new SqlDataTypeFactory();
            var parameterMapper = new LambdaParameterToDataSourceMapper();
            var sqlFactory = new SqlExpressionFactory();
            this._ormModel = new OrmModel();
            var contextExtensions = new object[] { sqlDataTypeFactory, sqlFactory, this._ormModel, parameterMapper, reflectionService, logger, expressionEvaluator };
            var conversionContext = new ConversionContext(contextExtensions);
            this.OnConversionContextInitialized(conversionContext, this.customConverterFactories);
            var expressionConverterProvider = new LinqToSqlExpressionConverterProvider(conversionContext, factories: this.customConverterFactories);
            var preprocessor = GetPreprocessorProvider(reflectionService, expressionEvaluator, this._ormModel);
            var linqToSqlConverter = new LinqToSqlConverter(reflectionService, expressionConverterProvider, new SqlExpressionPostprocessorProvider(null));
            var sqlExpressionTranslator = new SqlExpressionTranslatorBase();
            var elementFactoryBuilder = new ElementFactoryBuilder();
            var queryCompiler = new QueryCompiler(preprocessor, preprocessingRequirementTester, linqToSqlConverter, sqlExpressionTranslator, this.dbParameterFactory, elementFactoryBuilder);
            var expressionVariableValueExtractor = new ExpressionVariableValuesExtractor();
            var queryExecutor = new QueryExecutor(dbAdapter, queryCacheProvider, queryCompiler, expressionVariableValueExtractor, preprocessor);
            this._queryProvider = new OrmQueryProvider(reflectionService, queryExecutor);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="conversionContext"></param>
        /// <param name="customConverterFactories"></param>
        protected virtual void OnConversionContextInitialized(ConversionContext conversionContext, List<IExpressionConverterFactory<Expression, SqlExpression>> customConverterFactories)
        {
            // do nothing
        }

        private IExpressionPreprocessorProvider GetPreprocessorProvider(IReflectionService reflectionService, IExpressionEvaluator expressionEvaluator, IModel model)
        {
            var navigateToManyPreprocessor = new NavigateToManyPreprocessor(model);
            var navigateToOnePreprocessor = new NavigateToOnePreprocessor(model);
            var queryVariablePreprocessor = new QueryVariableReplacementPreprocessor();
            var calculatedPropertyReplacementPreprocessor = new OrmCalculatedPropertyPreprocessor(model);
            var specificationPreprocessor = new SpecificationCallRewriterPreprocessor(reflectionService, expressionEvaluator);
            var convertPreprocessor = new ConvertExpressionReplacementPreprocessor();
            var allToAnyRewriterPreprocessor = new AllToAnyRewriterPreprocessor();
            var inValuesReplacementPreprocessor = new InValuesExpressionReplacementPreprocessor(expressionEvaluator);
            var methodInterfaceTypeReplacementPreprocessor = new QueryMethodGenericTypeReplacementPreprocessor(reflectionService);
            var navigationEqualityPreprocessor = new NavigationNullEqualityPreprocessor(model, reflectionService);
            var preprocessors = new List<IExpressionPreprocessor>(new IExpressionPreprocessor[] { queryVariablePreprocessor, methodInterfaceTypeReplacementPreprocessor, navigateToManyPreprocessor, navigateToOnePreprocessor, calculatedPropertyReplacementPreprocessor, specificationPreprocessor, convertPreprocessor, allToAnyRewriterPreprocessor, inValuesReplacementPreprocessor, navigationEqualityPreprocessor });
            if (this.customPreprocessors != null && this.customPreprocessors.Count > 0)
            {
                preprocessors.AddRange(this.customPreprocessors);
            }
            var preprocessor = new ExpressionPreprocessorProvider(preprocessors);
            return preprocessor;
        }
    }
}
