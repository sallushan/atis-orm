using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.Preprocessors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Atis.Orm.Preprocessing
{
    // This client might not be finalized, need to look at it

    /// <summary>
    /// 
    /// </summary>
    public class OrmCalculatedPropertyPreprocessor : CalculatedPropertyPreprocessorBase
    {
        private readonly IModel model;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="model"></param>
        public OrmCalculatedPropertyPreprocessor(IModel model) : base()
        {
            this.model = model;
        }

        /// <inheritdoc />
        protected override bool TryGetCalculatedExpression(MemberExpression memberExpression, out LambdaExpression calculatedPropertyExpression)
        {
            var modelType = memberExpression.Expression?.Type;
            if (modelType != null)
            {
                if (this.model.CanBeEntity(modelType))
                {
                    var metadata = this.model.GetRequiredEntity(modelType)
                        ??
                        // This should not happen if CanBeEntity returned true, but we throw an exception to be safe
                        throw new InvalidOperationException($"GetRequiredEntity returned null for type {modelType.FullName}. This should not happen if CanBeEntity returned true.");
                    if (metadata.CalculatedProperties.TryGetValue(memberExpression.Member.Name, out var lambdaExpression))
                    {
                        calculatedPropertyExpression = lambdaExpression;
                        return true;
                    }
                }
            }
            calculatedPropertyExpression = null;
            return false;
        }
    }
}
