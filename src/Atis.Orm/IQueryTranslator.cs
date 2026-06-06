using System.Linq.Expressions;

namespace Atis.Orm
{
    public interface IQueryTranslator
    {
        QueryTranslationResult Translate(Expression expression);
    }
}
