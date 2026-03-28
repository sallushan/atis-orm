using Atis.SqlExpressionEngine.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;

namespace Atis.Orm
{
    public class OrmQueryProvider : IAsyncQueryProvider
    {
        private static MethodInfo openCreateQueryMethod;
        private static MethodInfo OpenCreateQueryMethod
        {
            get
            {
                if (openCreateQueryMethod is null)
                    openCreateQueryMethod = typeof(OrmQueryProvider)
                                                    .GetMethods(BindingFlags.IgnoreCase | BindingFlags.Public)
                                                    .Single(x => x.Name == nameof(CreateQuery)
                                                                  && x.IsGenericMethodDefinition
                                                                  && x.GetGenericArguments().Length == 1);
                return openCreateQueryMethod;
            }
        }

        private static MethodInfo openExecuteMethod;
        private static MethodInfo OpenExecuteMethod
        {
            get
            {
                if (openExecuteMethod is null)
                    openExecuteMethod = typeof(OrmQueryProvider)
                                            .GetMethods(BindingFlags.IgnoreCase | BindingFlags.Public)
                                            .Single(x => x.Name == nameof(Execute)
                                                         && x.IsGenericMethodDefinition
                                                         && x.GetGenericArguments().Length == 1);
                return openExecuteMethod;
            }
        }
        
        
        private readonly IReflectionService reflectionService;
        private readonly IQueryExecutor queryExecutor;

        public OrmQueryProvider(IReflectionService reflectionService, IQueryExecutor queryExecutor)
        {
            this.reflectionService = reflectionService ?? throw new ArgumentNullException(nameof(reflectionService));
            this.queryExecutor = queryExecutor ?? throw new ArgumentNullException(nameof(queryExecutor));
        }

        public virtual IQueryable CreateQuery(Expression expression)
        {
            return (IQueryable)OpenCreateQueryMethod
                                .MakeGenericMethod(this.reflectionService.GetElementType(expression.Type))
                                .Invoke(this, new[] { expression });
        }

        public virtual IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new OrmQueryable<TElement>(this, expression);
        }

        public virtual object Execute(Expression expression)
        {
            return OpenExecuteMethod.MakeGenericMethod(this.reflectionService.GetElementType(expression.Type))
                    .Invoke(this, new[] { expression });
        }

        public virtual TResult Execute<TResult>(Expression expression)
        {
            return this.queryExecutor.Execute<TResult>(expression);
        }

        public virtual TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            return this.queryExecutor.ExecuteAsync<TResult>(expression, cancellationToken);
        }
    }
}
