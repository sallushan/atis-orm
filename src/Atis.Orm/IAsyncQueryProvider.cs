using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;

namespace Atis.Orm
{
    public interface IAsyncQueryProvider : IQueryProvider
    {
        TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default);
    }
}
