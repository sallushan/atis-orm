using System.Linq.Expressions;

using Atis.Orm.Translation;
namespace Atis.Orm.Abstractions
{
    public interface IQueryTranslator
    {
        QueryTranslationResult Translate(Expression expression);
    }
}
