using Atis.DependencyInjection;
using Atis.Orm;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atis.SqlExpressionEngine.UnitTest
{
    internal class ComponentAnnotationExtension : IServiceContextExtension
    {
        public void AddServices(IServiceCollection services)
        {
            var builder = new OrmServiceBuilder(services);
            builder.TryAdd<IEntityMetadataBuilder, ComponentAnnotationMetadataBuilder>();
        }
    }
}
