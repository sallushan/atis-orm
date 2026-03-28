using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;

namespace Atis.Orm
{
    public class OrmQueryable<T> : IOrderedQueryable<T>, IAsyncEnumerable<T>
    {
        private readonly IAsyncQueryProvider queryProvider;
        public OrmQueryable(IAsyncQueryProvider provider)
        {
            this.Expression = Expression.Constant(this);
            this.queryProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public OrmQueryable(IAsyncQueryProvider provider, Expression expression)
        {
            this.Expression = expression;
            this.queryProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public Type ElementType => typeof(T);

        public virtual Expression Expression { get; }

        public virtual IQueryProvider Provider => this.queryProvider;

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return this.queryProvider.ExecuteAsync<IAsyncEnumerable<T>>(this.Expression, cancellationToken)
                                        .GetAsyncEnumerator(cancellationToken);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return this.queryProvider.Execute<IEnumerable<T>>(this.Expression)
                                        .GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
