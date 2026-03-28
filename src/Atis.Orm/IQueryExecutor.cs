using System.Linq.Expressions;
using System.Threading;

namespace Atis.Orm
{
    public interface IQueryExecutor
    {
        TResult Execute<TResult>(Expression expression);
        TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken);
    }
}