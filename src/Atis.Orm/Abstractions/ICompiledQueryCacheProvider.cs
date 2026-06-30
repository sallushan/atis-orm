using System.Linq.Expressions;

namespace Atis.Orm.Abstractions
{
    public interface ICompiledQueryCacheProvider
    {
        void Add(Expression expression, ICompiledQuery compiledQuery);
        bool TryGet(Expression expression, out ICompiledQuery compiledQuery);
    }
}