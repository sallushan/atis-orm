using Atis.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Atis.Orm.DataAccess
{
    /// <summary>
    ///     Helpers for reading an extension's instance-level options out of a configuration, and out of the
    ///     scope a service is being resolved into.
    /// </summary>
    public static class DataContextServiceExtensions
    {
        /// <summary>
        ///     Returns the registered extension assignable to <typeparamref name="TExtension"/>, or
        ///     <c>null</c> if the configuration has none.
        /// </summary>
        public static TExtension FindExtension<TExtension>(this IServiceContextConfiguration configuration)
            where TExtension : class, IServiceContextExtension
        {
            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));

            foreach (var extension in configuration.Extensions)
            {
                if (extension is TExtension typedExtension)
                    return typedExtension;
            }
            return null;
        }

        /// <summary>
        ///     Returns the registered extension assignable to <typeparamref name="TExtension"/>, throwing if
        ///     the configuration has none.
        /// </summary>
        public static TExtension GetRequiredExtension<TExtension>(this IServiceContextConfiguration configuration)
            where TExtension : class, IServiceContextExtension
        {
            return configuration.FindExtension<TExtension>()
                   ?? throw new InvalidOperationException(
                       $"No '{typeof(TExtension).Name}' is registered on this configuration. A service that " +
                       $"depends on it was resolved from a {nameof(DataContext)} that never configured it.");
        }

        /// <summary>
        ///     <para>
        ///         Reads the <typeparamref name="TExtension"/> belonging to the <see cref="DataContext"/> that
        ///         owns <paramref name="serviceProvider"/>'s scope.
        ///     </para>
        ///     <para>
        ///         This is how a provider registration gets at its options. Doing it here, inside the factory
        ///         delegate, is what keeps the delegate from capturing an extension instance — the root
        ///         provider is shared between contexts whose options differ, so a captured instance would
        ///         leak the first context's options into all the rest.
        ///     </para>
        /// </summary>
        public static TExtension GetContextExtension<TExtension>(this IServiceProvider serviceProvider)
            where TExtension : class, IServiceContextExtension
        {
            if (serviceProvider is null)
                throw new ArgumentNullException(nameof(serviceProvider));

            return serviceProvider.GetRequiredService<IDataContextServices>()
                                  .Configuration
                                  .GetRequiredExtension<TExtension>();
        }
    }
}
