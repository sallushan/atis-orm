using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;

namespace Atis.Orm
{
    public class QueryCompiler : IQueryCompiler
    {
        private readonly IQueryTranslator queryTranslator;
        private readonly IDbParameterFactory dbParameterFactory;
        private readonly IPreprocessingRequirementTester preprocessingRequirementTester;
        private readonly IElementFactoryBuilder elementFactoryBuilder;

        public QueryCompiler(IQueryTranslator queryTranslator, IPreprocessingRequirementTester preprocessingRequirementTester, IDbParameterFactory dbParameterFactory, IElementFactoryBuilder elementFactoryBuilder)
        {
            this.queryTranslator = queryTranslator;
            this.preprocessingRequirementTester = preprocessingRequirementTester;
            this.dbParameterFactory = dbParameterFactory;
            this.elementFactoryBuilder = elementFactoryBuilder;
        }

        public ICompiledQuery Compile(Expression expression)
        {
            if (expression is null)
                throw new ArgumentNullException(nameof(expression));

            var queryTranslationResult = this.queryTranslator.Translate(expression);
            var isPreprocessingRequired = this.DeterminePreprocessingRequirement(expression, queryTranslationResult.PreprocessedExpression);
            var isNonQuery = queryTranslationResult.SqlExpression is SqlUpdateExpression || queryTranslationResult.SqlExpression is SqlInsertIntoExpression || queryTranslationResult.SqlExpression is SqlDeleteExpression;
            Func<IDataReader, object> elementFactory = this.CreateElementFactory(expression, queryTranslationResult.SqlExpression);
            var compiledQuery = new CompiledQuery(queryTranslationResult.SqlTranslation.Sql, queryTranslationResult.SqlTranslation.QueryParameters, this.dbParameterFactory, isNonQuery, elementFactory, isPreprocessingRequired);
            return compiledQuery;
        }

        private Func<IDataReader, object> CreateElementFactory(Expression expression, SqlExpression sqlExpression)
        {
            return this.elementFactoryBuilder.CreateElementFactory(expression, sqlExpression);
        }

        private bool DeterminePreprocessingRequirement(Expression originalExpression, Expression preprocessedExpression)
        {
            if (originalExpression == preprocessedExpression)
                return false;
            return this.preprocessingRequirementTester.IsPreprocessingRequired(originalExpression, preprocessedExpression);
        }
    }
}
