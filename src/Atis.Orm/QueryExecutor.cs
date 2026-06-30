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
        private readonly INavigationInitializer navigationInitializer;

        public QueryExecutor(IDatabaseAdapter dbAdapter, ICompiledQueryCacheProvider queryCacheProvider, IQueryCompiler queryCompiler, IExpressionVariableValuesExtractor expressionVariableValuesExtractor, IExpressionPreprocessorProvider preprocessor, INavigationInitializer navigationInitializer)
        {
            this.dbAdapter = dbAdapter ?? throw new ArgumentNullException(nameof(dbAdapter));
            this.queryCacheProvider = queryCacheProvider ?? throw new ArgumentNullException(nameof(queryCacheProvider));
            this.queryCompiler = queryCompiler ?? throw new ArgumentNullException(nameof(queryCompiler));
            this.expressionVariableValuesExtractor = expressionVariableValuesExtractor ?? throw new ArgumentNullException(nameof(expressionVariableValuesExtractor));
            this.preprocessor = preprocessor;
            this.navigationInitializer = navigationInitializer ?? throw new ArgumentNullException(nameof(navigationInitializer));
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
                return this.dbAdapter.Execute<TResult>(executionContext.Sql, executionContext.DbParameters, this.WrapWithNavigationInitializer(executionContext.ElementFactory));
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
                return this.dbAdapter.ExecuteAsync<TResult>(executionContext.Sql, executionContext.DbParameters, this.WrapWithNavigationInitializer(executionContext.ElementFactory), cancellationToken);
            }
        }

        /// <summary>
        ///     Wraps the (singleton-cached) element factory so that each materialized object has its
        ///     lazy navigation properties initialized via the scoped <see cref="INavigationInitializer"/>.
        ///     The wrapper only constructs lazy queries/delegates (no DB I/O), so it is safe to run while
        ///     the parent reader is open.
        /// </summary>
        protected virtual Func<IDataReader, object> WrapWithNavigationInitializer(Func<IDataReader, object> elementFactory)
        {
            return dr =>
            {
                var obj = elementFactory(dr);
                this.navigationInitializer.Initialize(obj);
                return obj;
            };
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
