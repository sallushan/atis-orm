using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    /// <summary>
    /// 
    /// </summary>
    public class LinqToSqlExpressionTreeConverter : ExpressionTreeConverter<Expression, SqlExpression>
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dependencyProvider"></param>
        /// <param name="factoryProvider"></param>
        public LinqToSqlExpressionTreeConverter(IExpressionConverterDependencyProvider dependencyProvider, ILinqToSqlConverterFactoryProvider factoryProvider)
            : base(dependencyProvider, factoryProvider?.GetFactories() ?? throw new ArgumentNullException(nameof(factoryProvider)))
        {
        }
    }
}
