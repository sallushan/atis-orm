using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;

using Atis.Orm.Abstractions;
using Atis.Orm.Translation;
namespace Atis.Orm.Querying
{
    public class QueryCompiler : IQueryCompiler
    {
        private readonly IQueryTranslator queryTranslator;
        private readonly ISqlCommandRenderer commandRenderer;
        private readonly IDbParameterFactory dbParameterFactory;
        private readonly IPreprocessingRequirementTester preprocessingRequirementTester;
        private readonly IElementFactoryBuilder elementFactoryBuilder;

        public QueryCompiler(IQueryTranslator queryTranslator, IPreprocessingRequirementTester preprocessingRequirementTester, ISqlCommandRenderer commandRenderer, IDbParameterFactory dbParameterFactory, IElementFactoryBuilder elementFactoryBuilder)
        {
            this.queryTranslator = queryTranslator;
            this.preprocessingRequirementTester = preprocessingRequirementTester;
            this.commandRenderer = commandRenderer;
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
            Func<IDataReader, object> elementFactory = null;
            if (!isNonQuery)
                elementFactory = this.CreateElementFactory(expression, queryTranslationResult.SqlExpression);
            var translation = queryTranslationResult.SqlTranslation;
            // The shape is decided once, here: an expandable query must re-render per execution; everything
            // else renders its SQL a single time and only rebinds parameters.
            ICompiledQuery compiledQuery = translation.HasExpandableParameters
                ? new ExpandableCompiledQuery(translation, this.commandRenderer, isNonQuery, elementFactory, isPreprocessingRequired)
                : (ICompiledQuery)new SimpleCompiledQuery(translation, this.commandRenderer, this.dbParameterFactory, isNonQuery, elementFactory, isPreprocessingRequired);
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
