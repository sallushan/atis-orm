using Atis.SqlExpressionEngine.SqlExpressions;

namespace Atis.Orm
{
    public interface ISqlExpressionTranslator
    {
        TranslationResult Translate(SqlExpression sqlExpression);
    }
}