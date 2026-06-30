using Atis.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Atis.Orm.Services
{
    public class ExpressionConverterDependencyProvider : IExpressionConverterDependencyProvider
    {
        private readonly IServiceProvider serviceProvider;

        public ExpressionConverterDependencyProvider(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public object GetDependencyRequired(Type type)
        {
            return this.serviceProvider.GetService(type)
                    ??
                    throw new InvalidOperationException($"Required dependency of type {type.FullName} is not registered in the service provider.");
        }

        public T GetDependencyRequired<T>()
        {
            return (T)this.GetDependencyRequired(typeof(T));
        }
    }

    public class ExpressionConverterDependencyProviderByCollection : IExpressionConverterDependencyProvider
    {
        private readonly IReadOnlyList<object> serviceInstances;

        public ExpressionConverterDependencyProviderByCollection(IReadOnlyList<object> serviceInstances)
        {
            this.serviceInstances = serviceInstances ?? throw new ArgumentNullException(nameof(serviceInstances));
            if (serviceInstances.Any(x => x is null))
                throw new InvalidOperationException($"Service instances collection cannot contain null values.");
        }

        public object GetDependencyRequired(Type type)
        {
            foreach (var instance in this.serviceInstances)
            {
                if (type.IsAssignableFrom(instance.GetType()))
                {
                    return instance;
                }
            }

            throw new InvalidOperationException($"Required dependency of type {type.FullName} is not registered in the service provider.");
        }

        public T GetDependencyRequired<T>()
        {
            return (T)this.GetDependencyRequired(typeof(T));
        }
    }
}
