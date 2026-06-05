using Atis.DependencyInjection;
using Atis.Expressions;
using Atis.Orm;
using Atis.Orm.SqlServer;
using Atis.SqlExpressionEngine.UnitTest.Converters;
using Atis.SqlExpressionEngine.UnitTest.Preprocessors;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atis.SqlExpressionEngine.UnitTest
{
    public static class UnitTestDataContextConfigurationExtensions
    {
        public static DataContextConfiguration UseUnitTestCustomization(this DataContextConfiguration config)
        {
            config.AddOrUpdateExtension(new UnitTestCustomizationContextExtension());
            return config;
        }
    }

    public class UnitTestCustomizationContextExtension : IServiceContextExtension
    {
        public void AddServices(IServiceCollection services)
        {
            var builder = new OrmServiceBuilder(services);
            builder.AddConverterFactory<SqlFunctionConverterFactory>();
            builder.TryAdd<IExpressionPreprocessor, CustomBusinessMethodPreprocessor>();
        }
    }
}
