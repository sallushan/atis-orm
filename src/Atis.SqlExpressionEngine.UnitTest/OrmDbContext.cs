using Atis.Expressions;
using Atis.Orm;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.Services;
using Atis.SqlExpressionEngine.SqlExpressions;
using Atis.SqlExpressionEngine.UnitTest.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Atis.Orm.SqlServer;

namespace Atis.SqlExpressionEngine.UnitTest
{
    internal class OrmDbContext : DataContext
    {
        public OrmDbContext()
        {
        }

        //protected override void OnCustomFactoriesInitialize(List<IExpressionConverterFactory<Expression, SqlExpression>> customConverterFactories)
        //{
        //    var sqlFunctionConverterFactory = new SqlFunctionConverterFactory();
        //    customConverterFactories?.Add(sqlFunctionConverterFactory);

        //    base.OnCustomFactoriesInitialize(customConverterFactories);
        //}

        protected override void OnConfiguring(DataContextConfiguration config)
        {
            config.UseSqlServer($"Server=.;Database={TestDatabaseSetup.DatabaseName};Integrated Security=true;Encrypt=True;TrustServerCertificate=True");
            config.UseUnitTestCustomConverters();
        }
    }
}
