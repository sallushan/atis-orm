using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Atis.SqlExpressionEngine.Services
{
    public class LinqToSqlExpressionTreeConverterFactory : ILinqToSqlExpressionTreeConverterFactory
    {
        private readonly IExpressionConverterDependencyProvider converterDependencyProvider;
        private readonly IEnumerable<IExpressionConverterFactory<Expression, SqlExpression>> factories;

        public LinqToSqlExpressionTreeConverterFactory(IExpressionConverterDependencyProvider converterDependencyProvider, IEnumerable<IExpressionConverterFactory<Expression, SqlExpression>> userProvidedFactories)
        {
            this.converterDependencyProvider = converterDependencyProvider ?? throw new ArgumentNullException(nameof(converterDependencyProvider));
            this.factories = userProvidedFactories;
        }

        public IExpressionTreeConverter<Expression, SqlExpression> Create()
        {
            return new LinqToSqlExpressionTreeConverter(this.converterDependencyProvider, this.factories);
        }
    }
}
