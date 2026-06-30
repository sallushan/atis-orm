using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using System.Text;

using Atis.Orm.Abstractions;
namespace Atis.Orm.Translation
{
    public class QueryTranslator : IQueryTranslator
    {
        private readonly ILinqToSqlConverter linqToSqlConverter;
        private readonly IExpressionPreprocessorProvider preprocessor;
        private readonly ISqlExpressionTranslator sqlExpressionTranslator;
        private readonly ILogger logger;

        public QueryTranslator(IExpressionPreprocessorProvider preprocessor, ILinqToSqlConverter linqToSqlConverter, ISqlExpressionTranslator sqlExpressionTranslator, ILogger logger)
        {
            this.linqToSqlConverter = linqToSqlConverter;
            this.preprocessor = preprocessor;
            this.sqlExpressionTranslator = sqlExpressionTranslator;
            this.logger = logger;
        }

        public QueryTranslationResult Translate(Expression expression)
        {
            if (expression is null)
                throw new ArgumentNullException(nameof(expression));

            this.logger.Log("Before preprocessing:");
            this.logger.Log(expression.ToString());
            var preprocessedExpression = this.PreprocessExpression(expression);
            this.logger.Log("After preprocessing:");
            this.logger.Log(preprocessedExpression.ToString());
            var sqlExpression = this.ConvertExpressionToSqlExpression(preprocessedExpression);
            var translationResult = this.TranslateSqlExpression(sqlExpression);
            var queryTranslationResult = new QueryTranslationResult(sqlExpression, preprocessedExpression, translationResult);
            return queryTranslationResult;
        }

        private SqlTranslationResult TranslateSqlExpression(SqlExpression sqlExpression)
        {
            return this.sqlExpressionTranslator.Translate(sqlExpression);
        }

        private SqlExpression ConvertExpressionToSqlExpression(Expression preprocessedExpression)
        {
            return this.linqToSqlConverter.Convert(preprocessedExpression);
        }

        private Expression PreprocessExpression(Expression expression)
        {
            return this.preprocessor?.Preprocess(expression) ?? expression;
        }
    }
}
