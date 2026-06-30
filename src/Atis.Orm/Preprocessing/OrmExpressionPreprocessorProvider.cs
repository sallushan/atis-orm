using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.Preprocessors;
using Atis.SqlExpressionEngine.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Atis.Orm.Preprocessing
{
    public class OrmExpressionPreprocessorProvider : IExpressionPreprocessorProvider
    {

        private readonly int maxIterations;
        protected List<IExpressionPreprocessor> ExpressionPreprocessors { get; } = new List<IExpressionPreprocessor>();

        public OrmExpressionPreprocessorProvider(IModel model, IReflectionService reflectionService, IExpressionEvaluator expressionEvaluator, IEnumerable<IExpressionPreprocessor> plugins, int maxIterations = 50) 
        {
            this.maxIterations = maxIterations;

            if (plugins != null && plugins.Any())
                this.ExpressionPreprocessors.AddRange(plugins);

            var navigateToManyPreprocessor = new NavigateToManyPreprocessor(model);
            var navigateToOnePreprocessor = new NavigateToOnePreprocessor(model);
            var queryVariablePreprocessor = new QueryVariableReplacementPreprocessor();
            var calculatedPropertyReplacementPreprocessor = new OrmCalculatedPropertyPreprocessor(model);
            var specificationPreprocessor = new SpecificationCallRewriterPreprocessor(reflectionService, expressionEvaluator);
            var convertPreprocessor = new ConvertExpressionReplacementPreprocessor();
            var allToAnyRewriterPreprocessor = new AllToAnyRewriterPreprocessor();
            var inValuesReplacementPreprocessor = new InValuesExpressionReplacementPreprocessor(expressionEvaluator);
            var methodInterfaceTypeReplacementPreprocessor = new QueryMethodGenericTypeReplacementPreprocessor(reflectionService);
            var navigationEqualityPreprocessor = new NavigationNullEqualityPreprocessor(model, reflectionService);
            
            this.ExpressionPreprocessors.AddRange(new IExpressionPreprocessor[]
            {
                queryVariablePreprocessor, 
                methodInterfaceTypeReplacementPreprocessor, 
                navigateToManyPreprocessor, 
                navigateToOnePreprocessor, 
                calculatedPropertyReplacementPreprocessor, 
                specificationPreprocessor, 
                convertPreprocessor, 
                allToAnyRewriterPreprocessor, 
                inValuesReplacementPreprocessor, 
                navigationEqualityPreprocessor
            });
        }

        /// <inheritdoc />
        public Expression Preprocess(Expression expression)
        {
            bool expressionChanged;
            int iterations = 0;

            do
            {
                expressionChanged = false;

                foreach (var postProcessor in this.ExpressionPreprocessors)
                {
                    postProcessor.Initialize();
                    var newSqlExpression = postProcessor.Preprocess(expression);

                    if (newSqlExpression != expression)
                    {
                        expression = newSqlExpression;
                        expressionChanged = true;
                    }
                }

                iterations++;

                if (iterations >= this.maxIterations)
                {
                    throw new PreprocessingThresholdExceededException(this.maxIterations);
                }

            } while (expressionChanged);

            return expression;
        }
    }
}
