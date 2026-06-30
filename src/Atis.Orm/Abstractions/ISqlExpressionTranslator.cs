using Atis.SqlExpressionEngine.SqlExpressions;

using Atis.Orm.Translation;
namespace Atis.Orm.Abstractions
{
    public interface ISqlExpressionTranslator
    {
        SqlTranslationResult Translate(SqlExpression sqlExpression);
    }
}