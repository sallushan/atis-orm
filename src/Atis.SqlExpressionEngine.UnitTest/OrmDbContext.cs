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

namespace Atis.SqlExpressionEngine.UnitTest
{
    internal class OrmDbContext : DataContext
    {
        public OrmDbContext(IDbCommunication dbCommunication, IDbParameterFactory dbParameterFactory, ILogger logger, IReadOnlyList<IExpressionPreprocessor> customPreprocessors) : base(dbCommunication, dbParameterFactory, logger, customPreprocessors)
        {
        }

        protected override void OnConversionContextInitialized(ConversionContext conversionContext, List<IExpressionConverterFactory<Expression, SqlExpression>> customConverterFactories)
        {
            var sqlFunctionConverterFactory = new SqlFunctionConverterFactory(conversionContext);
            customConverterFactories?.Add(sqlFunctionConverterFactory);

            base.OnConversionContextInitialized(conversionContext, customConverterFactories);
        }
    }
}
