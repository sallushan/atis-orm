using Atis.SqlExpressionEngine.SqlExpressions;

namespace Atis.Orm
{
    public interface ISqlExpressionTranslator
    {
        SqlTranslationResult Translate(SqlExpression sqlExpression);
    }
}