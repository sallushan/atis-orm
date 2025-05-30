﻿using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.Exceptions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System.Collections.Generic;

namespace Atis.SqlExpressionEngine.Services
{
    public class SqlExpressionPostprocessorProvider : ISqlExpressionPostprocessorProvider
    {
        private readonly int maxIterations;
        protected List<ISqlExpressionPostprocessor> PostProcessors { get; } = new List<ISqlExpressionPostprocessor>();
        public SqlExpressionPostprocessorProvider(IEnumerable<ISqlExpressionPostprocessor> postprocessors, int maxIterations = 50)
        {
            if (postprocessors != null)
                this.PostProcessors.AddRange(postprocessors);
            //this.PostProcessors.Add(new CteFixPostprocessor());
            //this.PostProcessors.Add(new CteCrossJoinPostprocessor(sqlFactory));
            this.maxIterations = maxIterations;
        }

        public SqlExpression Postprocess(SqlExpression sqlExpression)
        {
            bool expressionChanged;
            int iterations = 0;

            do
            {
                expressionChanged = false;

                foreach (var postProcessor in this.PostProcessors)
                {
                    postProcessor.Initialize();
                    var newSqlExpression = postProcessor.Postprocess(sqlExpression);

                    if (newSqlExpression != sqlExpression)
                    {
                        sqlExpression = newSqlExpression;
                        expressionChanged = true;
                    }
                }

                iterations++;

                if (iterations >= this.maxIterations)
                {
                    throw new PostprocessingThresholdExceededException(this.maxIterations);
                }

            } while (expressionChanged);

            return sqlExpression;
        }
    }
}
