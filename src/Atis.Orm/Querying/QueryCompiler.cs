using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
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
            if (queryTranslationResult.Children.Count > 0)
                return this.CompileWithChildren(expression, queryTranslationResult);

            var isNonQuery = (queryTranslationResult.SqlExpression is SqlUpdateExpression sqlUpdate && sqlUpdate.Outputs.Count == 0)
                                || (queryTranslationResult.SqlExpression is SqlInsertExpression sqlInsert && sqlInsert.Outputs.Count == 0)
                                || queryTranslationResult.SqlExpression is SqlInsertIntoExpression
                                || queryTranslationResult.SqlExpression is SqlDeleteExpression;
            Func<IDataReader, object> elementFactory = null;
            if (!isNonQuery)
                elementFactory = this.CreateElementFactory(expression, queryTranslationResult.SqlExpression);
            return this.CreateCompiledQuery(queryTranslationResult.SqlTranslation, isNonQuery, elementFactory);
        }

        private ICompiledQuery CreateCompiledQuery(SqlTranslationResult translation, bool isNonQuery, Func<IDataReader, object> elementFactory)
        {
            // The shape is decided once, here: a query whose SQL text depends on the values (an expandable
            // collection, an optional term that can be dropped, or a comparison against a value that could be
            // null) must re-render per execution; everything else renders its SQL a single time and only
            // rebinds parameters.
            ICompiledQuery compiledQuery = translation.RequiresPerExecutionRendering
                ? new ExpandableCompiledQuery(translation, this.commandRenderer, isNonQuery, elementFactory)
                : (ICompiledQuery)new SimpleCompiledQuery(translation, this.commandRenderer, this.dbParameterFactory, isNonQuery, elementFactory);
            return compiledQuery;
        }

        /// <summary>
        ///     A query with child collections is several statements: the main query and one query per child collection.
        ///     Each is compiled like any other query, the child ones with a factory that reads a child row.
        /// </summary>
        private ICompiledQuery CompileWithChildren(Expression expression, QueryTranslationResult queryTranslationResult)
        {
            var parentQuery = queryTranslationResult.SqlExpression as SqlDerivedTableExpression
                                ??
                                throw new InvalidOperationException($"A query with child collections must be a {nameof(SqlDerivedTableExpression)}, but it is '{queryTranslationResult.SqlExpression.GetType().Name}'.");
            var childQueries = queryTranslationResult.Children.Select(x => x.ChildQuery).ToArray();
            var factory = this.elementFactoryBuilder.CreateChildAwareElementFactory(expression, parentQuery, childQueries);

            var parent = this.CreateCompiledQuery(queryTranslationResult.SqlTranslation, isNonQuery: false, elementFactory: null);
            var children = new ICompiledQuery[childQueries.Length];
            for (var i = 0; i < children.Length; i++)
            {
                var rowFactory = factory.ChildRowFactories[i];
                children[i] = rowFactory is null
                                ? null
                                : this.CreateCompiledQuery(queryTranslationResult.Children[i].Translation, isNonQuery: false, elementFactory: rowFactory);
            }
            return new ChildAwareCompiledQuery(parent, children, factory.ParentFactory);
        }

        private Func<IDataReader, object> CreateElementFactory(Expression expression, SqlExpression sqlExpression)
        {
            return this.elementFactoryBuilder.CreateElementFactory(expression, sqlExpression);
        }
    }
}
