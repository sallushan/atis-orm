using Atzonix.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Atis.Orm.DataAccess;
namespace Atis.Orm
{
    /// <summary>
    ///     <para>
    ///         Manages the process-wide cache of root <see cref="IServiceProvider"/> instances used by
    ///         <see cref="DataContext"/>. Each provider is cached by configuration type + registered
    ///         extension types (not by <see cref="DataContext"/> subclass), so singletons such as
    ///         <see cref="Atis.Orm.Abstractions.IOrmModel"/> are shared by every context resolving to the
    ///         same key. See docs/ServiceProviderCachingAndModelLifetime.md.
    ///     </para>
    /// </summary>
    public class OrmServiceManager : ServiceManagerBase
    {
        public static readonly OrmServiceManager Instance = new OrmServiceManager();

        protected override ServiceBuilderBase CreateServiceBuilder(IServiceCollection serviceCollection)
        {
            return new OrmServiceBuilder(serviceCollection);
        }

        /// <summary>
        ///     <para>
        ///         Extends the base key (configuration type + extension types) with anything the extensions
        ///         declare through <see cref="IServiceProviderCacheKeyContributor"/>.
        ///     </para>
        ///     <para>
        ///         The base key intentionally ignores instance-level options so that contexts differing only
        ///         in connection string share a provider; those options are supplied per scope via
        ///         <see cref="IDataContextServices"/>. An option that a <em>singleton</em> depends on cannot
        ///         work that way, so it is folded in here and splits the cache instead.
        ///     </para>
        /// </summary>
        protected override int GetKey(IServiceContextConfiguration configuration)
        {
            var key = base.GetKey(configuration);

            // Extensions are an unordered set, so sort by type name to keep the key stable regardless of the
            // order the caller registered them in.
            var contributors = configuration.Extensions
                                            .OfType<IServiceProviderCacheKeyContributor>()
                                            .OrderBy(extension => extension.GetType().FullName, StringComparer.Ordinal);

            foreach (var contributor in contributors)
            {
                var contribution = contributor.GetServiceProviderCacheKey();
                key = unchecked((key * 397) ^ (contribution?.GetHashCode() ?? 0));
            }

            return key;
        }
    }
}
