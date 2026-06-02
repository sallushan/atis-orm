using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.ExpressionConverters;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine
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
        /// <param name="userProvidedFactories"></param>
        public LinqToSqlExpressionTreeConverter(IExpressionConverterDependencyProvider dependencyProvider, IEnumerable<IExpressionConverterFactory<Expression, SqlExpression>> userProvidedFactories = null) 
            : base(dependencyProvider, userProvidedFactories)
        {
        }

        /// <inheritdoc />
        protected override IReadOnlyList<IExpressionConverterFactory<Expression, SqlExpression>> GetDefaultFactories()
        {
            var reflectionService = this.ConverterDependencyProvider.GetDependencyRequired<IReflectionService>();
            var expressionEvaluator = this.ConverterDependencyProvider.GetDependencyRequired<IExpressionEvaluator>();

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
                //new ConstantQueryableExpressionConverterFactory(),
                new ParameterExpressionConverterFactory(),
                new GroupByKeyExpressionConverterFactory(reflectionService),
                new DateSubtractConverterFactory(),
                new DateTimeMemberAccessConverterFactory(),
                new VariableMemberExpressionConverterFactory(reflectionService, expressionEvaluator),
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
                //new GroupBySelectMethodCallConverterFactory(),
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
    }
}
