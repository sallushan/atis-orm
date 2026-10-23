using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

using Atis.SqlExpressionEngine.Internal;
using Atis.SqlExpressionEngine.Services;

using Atis.Orm.Abstractions;
namespace Atis.Orm.Translation
{
    public class QueryTranslator : IQueryTranslator
    {
        private readonly ILinqToSqlConverter linqToSqlConverter;
        private readonly IExpressionPreprocessorProvider preprocessor;
        private readonly ISqlExpressionTranslator sqlExpressionTranslator;
        private readonly ILogger logger;
        private readonly ChildQueryExtractor childQueryExtractor;

        public QueryTranslator(IExpressionPreprocessorProvider preprocessor, ILinqToSqlConverter linqToSqlConverter, ISqlExpressionTranslator sqlExpressionTranslator, ILogger logger, ISqlExpressionFactory sqlFactory = null)
        {
            this.linqToSqlConverter = linqToSqlConverter;
            this.preprocessor = preprocessor;
            this.sqlExpressionTranslator = sqlExpressionTranslator;
            this.logger = logger;
            this.childQueryExtractor = new ChildQueryExtractor(sqlFactory ?? new SqlExpressionFactory());
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

            // A sub-query selected in the projection (ToList() / ToArray()) is not part of the SQL of the main rows;
            // it is a query of its own, so the final query is split into the main query and one query per sub-query.
            IReadOnlyList<ChildQueryTranslation> children = null;
            if (sqlExpression is SqlDerivedTableExpression derivedTable &&
                derivedTable.NodeType == SqlExpressionType.DerivedTable &&
                derivedTable.SelectColumnCollection.SelectColumns.Any(x => x.ColumnExpression is SqlQueryableExpression))
            {
                var split = this.childQueryExtractor.Extract(derivedTable);
                sqlExpression = split.ParentQuery;
                children = split.ChildQueries
                                .Select(x => new ChildQueryTranslation(x, this.TranslateSqlExpression(x.Query)))
                                .ToArray();
            }

            var translationResult = this.TranslateSqlExpression(sqlExpression);
            var queryTranslationResult = new QueryTranslationResult(sqlExpression, preprocessedExpression, translationResult, children);
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
