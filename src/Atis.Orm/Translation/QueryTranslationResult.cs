using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Atis.Orm.Translation
{
    public class QueryTranslationResult
    {
        public QueryTranslationResult(SqlExpression sqlExpression, Expression preprocessedExpression, SqlTranslationResult translationResult)
        {
            this.SqlExpression = sqlExpression;
            this.PreprocessedExpression = preprocessedExpression;
            this.SqlTranslation = translationResult;
        }

        public SqlExpression SqlExpression { get; }
        public Expression PreprocessedExpression { get; }
        public SqlTranslationResult SqlTranslation { get; }
    }
}
