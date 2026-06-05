using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.Preprocessors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Atis.Orm
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
                var metadata = this.model.GetEntity(modelType);
                if (metadata != null)
                {
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
