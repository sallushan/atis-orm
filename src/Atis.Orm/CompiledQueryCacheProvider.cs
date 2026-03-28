using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Atis.Orm
{
    public sealed class CompiledQueryCacheProvider : ICompiledQueryCacheProvider
    {
        private readonly IExpressionCacheKeyProvider _cacheKeyProvider;
        private readonly ConcurrentDictionary<object, ICompiledQuery> _cache;

        public CompiledQueryCacheProvider(IExpressionCacheKeyProvider cacheKeyProvider)
        {
            _cacheKeyProvider = cacheKeyProvider
                ?? throw new ArgumentNullException(nameof(cacheKeyProvider));

            _cache = new ConcurrentDictionary<object, ICompiledQuery>();
        }

        public void Add(Expression expression, ICompiledQuery compiledQuery)
        {
            if (expression is null)
                throw new ArgumentNullException(nameof(expression));

            if (compiledQuery is null)
                throw new ArgumentNullException(nameof(compiledQuery));

            var key = _cacheKeyProvider.GetCacheKey(expression);
            _cache[key] = compiledQuery;
        }

        public bool TryGet(Expression expression, out ICompiledQuery compiledQuery)
        {
            if (expression is null)
                throw new ArgumentNullException(nameof(expression));

            var key = _cacheKeyProvider.GetCacheKey(expression);
            return _cache.TryGetValue(key, out compiledQuery);
        }
    }
}
