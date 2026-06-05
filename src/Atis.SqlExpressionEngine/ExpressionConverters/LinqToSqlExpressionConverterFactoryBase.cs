using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    /// <summary>
    ///     <para>
    ///         Abstract base class for creating expression converters that transform LINQ expressions to SQL expressions.
    ///     </para>
    /// </summary>
    /// <typeparam name="TSource">The type of the source expression.</typeparam>
    public abstract class LinqToSqlExpressionConverterFactoryBase<TSource> : IExpressionConverterFactory<Expression, SqlExpression> where TSource : Expression
    {
        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="LinqToSqlExpressionConverterFactoryBase{TSource}"/> class.
        ///     </para>
        /// </summary>
        public LinqToSqlExpressionConverterFactoryBase()
        {
        }

        /// <inheritdoc />
        public virtual IReadOnlyList<Type> GetConverterDependencyTypes()
        {
            return new[] { typeof(ISqlExpressionFactory), typeof(IReflectionService), typeof(IExpressionEvaluator), typeof(ILambdaParameterToDataSourceMapper), typeof(ILogger) };
        }

        /// <inheritdoc />
        public abstract bool TryCreate(IConverterDependencies dependencyContainer, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] convertersStack, out ExpressionConverterBase<Expression, SqlExpression> converter);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="converterDependencies"></param>
        /// <returns></returns>
        protected LinqToSqlExpressionConverterDependencies GetConverterDependencies(IConverterDependencies converterDependencies)
        {
            return new LinqToSqlExpressionConverterDependencies(
                sqlFactory: converterDependencies.GetRequired<ISqlExpressionFactory>(),
                reflectionService: converterDependencies.GetRequired<IReflectionService>(),
                lambdaParamMapper: converterDependencies.GetRequired<ILambdaParameterToDataSourceMapper>(),
                logger: converterDependencies.GetRequired<ILogger>()
            );
        }
    }
}
