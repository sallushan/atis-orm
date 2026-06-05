using Atis.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Atis.Orm
{
    public class OrmServiceManager : ServiceManagerBase
    {
        public static readonly OrmServiceManager Instance = new OrmServiceManager();

        protected override ServiceBuilderBase CreateServiceBuilder(IServiceCollection serviceCollection)
        {
            return new OrmServiceBuilder(serviceCollection);
        }
    }
}
