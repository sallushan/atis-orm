using System;
using System.Linq.Expressions;

namespace Atis.Orm
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
        ///     The value remains a lambda so a captured variable stays visible in the expression tree
        ///     and can be rebound when a compiled query is reused.
        /// </summary>
        public LambdaExpression ValueSelector { get; }
    }
}
