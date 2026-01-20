using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Atis.Orm
{
    public class QueryCompiler : IQueryCompiler
    {
        private readonly ILinqToSqlConverter linqToSqlConverter;
        private readonly IExpressionPreprocessorProvider preprocessor;
        private readonly ISqlExpressionTranslator sqlExpressionTranslator;
        private readonly IExpressionVariableValuesExtractor queryParameterExtractor;
        private readonly IDbParameterFactory dbParameterFactory;
        private readonly IPreprocessingRequirementTester preprocessingRequirementTester;

        public QueryCompiler(IExpressionPreprocessorProvider preprocessor, IPreprocessingRequirementTester preprocessingRequirementTester, ILinqToSqlConverter linqToSqlConverter, ISqlExpressionTranslator sqlExpressionTranslator, IExpressionVariableValuesExtractor queryParameterExtractor, IDbParameterFactory dbParameterFactory)
        {
            this.linqToSqlConverter = linqToSqlConverter;
            this.preprocessor = preprocessor;
            this.sqlExpressionTranslator = sqlExpressionTranslator;
            this.queryParameterExtractor = queryParameterExtractor;
            this.dbParameterFactory = dbParameterFactory;
            this.preprocessingRequirementTester = preprocessingRequirementTester;
        }

        public ICompiledQuery Compile(Expression expression)
        {
            if (expression is null)
                throw new ArgumentNullException(nameof(expression));

            var preprocessedExpression = this.PreprocessExpression(expression);
            var isPreprocessingRequired = this.DeterminePreprocessingRequirement(expression, preprocessedExpression);
            var sqlExpression = this.ConvertToSqlExpression(preprocessedExpression);
            var translationResult = this.TranslateSqlExpression(sqlExpression);
            var compiledQuery = new CompiledQuery(translationResult.Sql, translationResult.QueryParameters, this.dbParameterFactory);
            return compiledQuery;
        }

        protected virtual bool DeterminePreprocessingRequirement(Expression originalExpression, Expression preprocessedExpression)
        {
            if (originalExpression == preprocessedExpression)
                return false;
            return this.preprocessingRequirementTester.IsPreprocessingRequired(originalExpression, preprocessedExpression);
        }

        protected virtual TranslationResult TranslateSqlExpression(SqlExpression sqlExpression)
        {
            return this.sqlExpressionTranslator.Translate(sqlExpression);
        }

        protected virtual SqlExpression ConvertToSqlExpression(Expression preprocessedExpression)
        {
            return this.linqToSqlConverter.Convert(preprocessedExpression);
        }

        protected virtual Expression PreprocessExpression(Expression expression)
        {
            return this.preprocessor?.Preprocess(expression) ?? expression;
        }
    }
}
