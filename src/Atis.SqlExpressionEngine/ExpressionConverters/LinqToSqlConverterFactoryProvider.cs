using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    public class LinqToSqlConverterFactoryProvider : ILinqToSqlConverterFactoryProvider
    {
        private readonly IReadOnlyList<IExpressionConverterFactory<Expression, SqlExpression>> _factories;

        public LinqToSqlConverterFactoryProvider(
            IReflectionService reflectionService,
            IExpressionEvaluator expressionEvaluator,
            IVariableIdentityProvider variableIdentityProvider,
            IEnumerable<IExpressionConverterFactory<Expression, SqlExpression>> userProvidedFactories)
        {
            var factories = new List<IExpressionConverterFactory<Expression, SqlExpression>>();
            // user factories first (higher priority)
            if (userProvidedFactories != null)
                factories.AddRange(userProvidedFactories);
            // then default factories
            factories.AddRange(BuildDefaultFactories(reflectionService, expressionEvaluator, variableIdentityProvider));
            _factories = factories;
        }

        private static IEnumerable<IExpressionConverterFactory<Expression, SqlExpression>> BuildDefaultFactories(IReflectionService reflectionService, IExpressionEvaluator expressionEvaluator, IVariableIdentityProvider variableIdentityProvider)
        {
            var defaultFactories = new IExpressionConverterFactory<Expression, SqlExpression>[]
            {
                new QueryRootExpressionConverterFactory(),
                new SchemaExpressionConverterFactory(),
                new TableExpressionConverterFactory(),
                new NullableValueExpressionConverterFactory(),
                new NegateExpressionConverterFactory(),
                new NotExpressionConverterFactory(),
                new CastExpressionConverterFactory(),
                new StringLengthExpressionConverterFactory(),
                new ParameterExpressionConverterFactory(),
                new GroupByKeyExpressionConverterFactory(reflectionService),
                new DateSubtractConverterFactory(),
                new DateTimeMemberAccessConverterFactory(),
                new VariableMemberExpressionConverterFactory(expressionEvaluator, variableIdentityProvider),
                new MemberExpressionConverterFactory(),
                new QuoteExpressionConverterFactory(),
                new LambdaExpressionConverterFactory(),
                new StringCompareToConverterFactory(),
                new BinaryExpressionConverterFactory(),
                new MemberInitExpressionConverterFactory(),
                new NewExpressionConverterFactory(),
                new StringFunctionsConverterFactory(),
                new NavigationMemberExpressionConverterFacotry(),
                new NavigationJoinCallExpressionConverterFactory(),
                new FromQueryMethodExpressionConverterFactory(),
                new WhereQueryMethodExpressionConverterFactory(),
                new LetLinqKeywordConverterFactory(),
                new SelectQueryMethodExpressionConverterFactory(),
                new AnyQueryMethodExpressionConverterFactory(),
                new SkipQueryMethodExpressionConverterFactory(),
                new TakeAfterSkipQueryMethodExpressionConverterFactory(),
                new TakeQueryMethodExpressionConverterFactory(),
                new FirstOrDefaultQueryMethodExpressionConverterFactory(),
                new OrderByQueryMethodExpressionConverterFactory(),
                new JoinQueryMethodExpressionConverterFactory(),
                new StandardJoinQueryMethodExpressionConverterFactory(),
                new GroupByQueryMethodExpressionConverterFactory(),
                new AggregateStringFunctionConverterFactory(reflectionService),
                new AggregateMethodExpressionConverterFactory(),
                new PagingQueryMethodExpressionConverterFactory(),
                new UnionQueryMethodExpressionConverterFactory(),
                new RecursiveUnionQueryMethodExpressionConverterFactory(),
                new DataSetQueryMethodExpressionConverterFactory(),
                new ConstantExpressionConverterFactory(),
                new SelectManyQueryMethodExpressionConverterFactory(),
                new DefaultIfEmptyExpressionConverterFactory(),
                new SubQueryNavigationExpressionConverterFactory(),
                new ConditionalExpressionConverterFactory(),
                new DistinctQueryMethodExpressionConverterFactory(),
                new UpdateQueryMethodExpressionConverterFactory(),
                new DeleteQueryMethodExpressionConverterFactory(),
                new InValuesExpressionConverterFactory(),
                new NewArrayExpressionConverterFactory(),
                new StandaloneSelectQueryMethodExpressionConverterFactory(),
                new DateFunctionsConverterFactory(),
                new ToStringConverterFactory(),
                new GetValueOrDefaultConverterFactory(),
                new CastQueryMethodExpressionConverterFactory(),
                new BulkInsertConverterFactory(),
                new GuidNewConverterFactory(),
            };
            return defaultFactories;
        }

        public IReadOnlyList<IExpressionConverterFactory<Expression, SqlExpression>> GetFactories() => _factories;
    }
}
