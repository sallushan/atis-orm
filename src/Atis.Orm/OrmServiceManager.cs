using Atis.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Atis.Orm
{
    /// <summary>
    ///     <para>
    ///         Manages the process-wide cache of root <see cref="IServiceProvider"/> instances used by
    ///         <see cref="DataContext"/>. Each provider is cached by configuration type + registered
    ///         extension types (not by <see cref="DataContext"/> subclass), so singletons such as
    ///         <see cref="IOrmModel"/> are shared by every context resolving to the same key.
    ///         See docs/ServiceProviderCachingAndModelLifetime.md.
    ///     </para>
    /// </summary>
    public class OrmServiceManager : ServiceManagerBase
    {
        public static readonly OrmServiceManager Instance = new OrmServiceManager();

        protected override ServiceBuilderBase CreateServiceBuilder(IServiceCollection serviceCollection)
        {
            return new OrmServiceBuilder(serviceCollection);
        }
    }
}
