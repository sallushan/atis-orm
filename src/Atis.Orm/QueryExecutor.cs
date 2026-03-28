using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Atis.Orm
{
    public class QueryExecutor : IQueryExecutor
    {
        private readonly IDatabaseAdapter dbAdapter;
        private readonly ICompiledQueryCacheProvider queryCacheProvider;
        private readonly IQueryCompiler queryCompiler;
        private readonly IExpressionVariableValuesExtractor expressionVariableValuesExtractor;
        private readonly IExpressionPreprocessorProvider preprocessor;

        public QueryExecutor(IDatabaseAdapter dbAdapter, ICompiledQueryCacheProvider queryCacheProvider, IQueryCompiler queryCompiler, IExpressionVariableValuesExtractor expressionVariableValuesExtractor, IExpressionPreprocessorProvider preprocessor)
        {
            this.dbAdapter = dbAdapter ?? throw new ArgumentNullException(nameof(dbAdapter));
            this.queryCacheProvider = queryCacheProvider ?? throw new ArgumentNullException(nameof(queryCacheProvider));
            this.queryCompiler = queryCompiler ?? throw new ArgumentNullException(nameof(queryCompiler));
            this.expressionVariableValuesExtractor = expressionVariableValuesExtractor ?? throw new ArgumentNullException(nameof(expressionVariableValuesExtractor));
            this.preprocessor = preprocessor;
        }

        public virtual TResult Execute<TResult>(Expression expression)
        {
            IExecutionContext executionContext = this.GetExecutionContext(expression);
            if (executionContext.IsNonQuery)
            {
                var result = this.dbAdapter.ExecuteNonQuery(executionContext.Sql, executionContext.DbParameters);
                return (TResult)(object)result;
            }
            else
            {
                return this.dbAdapter.Execute<TResult>(executionContext.Sql, executionContext.DbParameters, executionContext.ElementFactory);
            }
        }

        public virtual TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken)
        {
            IExecutionContext executionContext = this.GetExecutionContext(expression);
            if (executionContext.IsNonQuery)
            {
                var result = this.dbAdapter.ExecuteNonQueryAsync(executionContext.Sql, executionContext.DbParameters, cancellationToken);
                return (TResult)(object)result;
            }
            else
            {
                return this.dbAdapter.ExecuteAsync<TResult>(executionContext.Sql, executionContext.DbParameters, executionContext.ElementFactory, cancellationToken);
            }
        }


        protected virtual IExecutionContext GetExecutionContext(Expression expression)
        {
            ICompiledQuery compiledQuery = this.GetOrAddCompiledQuery(expression, out bool cacheHit);
            IReadOnlyList<object> parameterValues = null;
            if (cacheHit)
            {
                Expression expressionToUseToExtractVariableValues = expression;
                if (compiledQuery.IsPreprocessingRequired)
                    expressionToUseToExtractVariableValues = this.PreprocessExpression(expression);
                parameterValues = this.expressionVariableValuesExtractor.ExtractVariableValues(expressionToUseToExtractVariableValues);
            }
            IExecutionContext queryExecutionParameter = compiledQuery.GetExecutionContext(parameterValues, useInitialValues: !cacheHit);
            return queryExecutionParameter;
        }

        protected virtual Expression PreprocessExpression(Expression expression)
        {
            return this.preprocessor?.Preprocess(expression) ?? expression;
        }

        protected virtual ICompiledQuery GetOrAddCompiledQuery(Expression expression, out bool cacheHit)
        {
            if (!this.queryCacheProvider.TryGet(expression, out ICompiledQuery compiledQuery))
            {
                compiledQuery = this.queryCompiler.Compile(expression);
                this.queryCacheProvider.Add(expression, compiledQuery);
                cacheHit = false;
            }
            else
            {
                cacheHit = true;
            }
            return compiledQuery;
        }
    }
}
