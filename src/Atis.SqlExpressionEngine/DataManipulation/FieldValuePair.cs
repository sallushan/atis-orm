using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Atis.SqlExpressionEngine.ExpressionExtensions;

namespace Atis.SqlExpressionEngine
{
    /// <summary>A column selector paired with the value to write to it.</summary>
    internal sealed class FieldValuePair
    {
        public FieldValuePair(LambdaExpression fieldSelector, LambdaExpression valueSelector)
        {
            this.FieldSelector = fieldSelector ?? throw new ArgumentNullException(nameof(fieldSelector));
            this.ValueSelector = valueSelector ?? throw new ArgumentNullException(nameof(valueSelector));
        }

        public LambdaExpression FieldSelector { get; }

        /// <summary>
        ///     The value, still as a lambda rather than a plain value, so that a captured variable stays
        ///     visible in the expression tree and can be rebound on a compiled-query cache hit.
        /// </summary>
        public LambdaExpression ValueSelector { get; }
    }
}
