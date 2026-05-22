using System;
using System.Collections.Generic;
using System.Text;

namespace Atis.Orm.Annotations
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class CalculatedPropertyAttribute : Attribute
    {
        public string ExpressionPropertyName { get; set; }
        public CalculatedPropertyAttribute(string expressionPropertyName)
        {
            this.ExpressionPropertyName = expressionPropertyName;
        }
    }
}
