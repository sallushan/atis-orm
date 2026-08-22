using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;

using Atis.Orm.Abstractions;
using Atis.Orm.Services;
using Atis.Orm.Translation;
namespace Atis.Orm.Querying
{
    public class QueryCompiler : IQueryCompiler
    {
        private readonly IQueryTranslator queryTranslator;
        private readonly ICommandRenderer commandRenderer;
        private readonly IDbParameterFactory dbParameterFactory;
        private readonly IElementFactoryBuilder elementFactoryBuilder;

        public QueryCompiler(IQueryTranslator queryTranslator, ICommandRenderer commandRenderer, IDbParameterFactory dbParameterFactory, IElementFactoryBuilder elementFactoryBuilder)
        {
            this.queryTranslator = queryTranslator;
            this.commandRenderer = commandRenderer;
            this.dbParameterFactory = dbParameterFactory;
            this.elementFactoryBuilder = elementFactoryBuilder;
        }

        public ICompiledQuery Compile(Expression expression)
        {
            if (expression is null)
                throw new ArgumentNullException(nameof(expression));

            // On the ORIGINAL tree — the one values are re-extracted from on a cache hit — and once per cache
            // miss rather than per execution. Without it a duplicated parameter name compiles and returns
            // correct rows the first time (each node keeps its own translation-time value) and only throws
            // once the query is served from cache.
            NamedParameterValidator.Validate(expression);

            var queryTranslationResult = this.queryTranslator.Translate(expression);
            var isNonQuery = (queryTranslationResult.SqlExpression is SqlUpdateExpression sqlUpdate && sqlUpdate.Outputs.Count == 0)
                                || (queryTranslationResult.SqlExpression is SqlInsertExpression sqlInsert && sqlInsert.Outputs.Count == 0)
                                || queryTranslationResult.SqlExpression is SqlInsertIntoExpression
                                || queryTranslationResult.SqlExpression is SqlDeleteExpression;
            Func<IDataReader, object> elementFactory = null;
            if (!isNonQuery)
                elementFactory = this.CreateElementFactory(expression, queryTranslationResult.SqlExpression);
            var translation = queryTranslationResult.SqlTranslation;
            // The shape is decided once, here: a query whose SQL text depends on the values (an expandable
            // collection, an optional term that can be dropped, or a comparison against a value that could be
            // null) must re-render per execution; everything else renders its SQL a single time and only
            // rebinds parameters.
            ICompiledQuery compiledQuery = translation.RequiresPerExecutionRendering
                ? new ExpandableCompiledQuery(translation, this.commandRenderer, isNonQuery, elementFactory)
                : (ICompiledQuery)new SimpleCompiledQuery(translation, this.commandRenderer, this.dbParameterFactory, isNonQuery, elementFactory);
            return compiledQuery;
        }

        private Func<IDataReader, object> CreateElementFactory(Expression expression, SqlExpression sqlExpression)
        {
            return this.elementFactoryBuilder.CreateElementFactory(expression, sqlExpression);
        }
    }
}
