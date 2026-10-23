using Atis.Expressions;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Atis.SqlExpressionEngine.Preprocessors
{
    public class CollectionHoistingPreprocessor : ExpressionVisitor, IExpressionPreprocessor
    {
        public void Initialize()
        {
            // 
        }

        public Expression Preprocess(Expression expression)
        {
            return Visit(expression);
        }
    }
}
