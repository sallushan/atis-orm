using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Atis.Orm
{
    public class ExpressionVariableValuesExtractor : ExpressionVisitor, IExpressionVariableValuesExtractor
    {
        private List<object> variableValues = new List<object>();
        public IReadOnlyList<object> ExtractVariableValues(Expression sqlExpression)
        {
            this.variableValues.Clear();
            Visit(sqlExpression);
            return variableValues;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            this.variableValues.Add(node.Value);
            return base.VisitConstant(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (this.IsVariableNode(node))
            {
                var variableValue = this.GetVariableValue(node);
                this.variableValues.Add(variableValue);
                return node;
            }
            return base.VisitMember(node);
        }

        private object GetVariableValue(MemberExpression node)
        {
            if (node.Expression is ConstantExpression constantExpression)
            {
                return constantExpression.Value;
            }
            else if (node.Expression is MemberExpression me)
            {
                var container = this.GetVariableValue(me);
                if (node.Member is System.Reflection.FieldInfo fieldInfo)
                {
                    return fieldInfo.GetValue(container);
                }
                else if (node.Member is System.Reflection.PropertyInfo propertyInfo)
                {
                    return propertyInfo.GetValue(container);
                }
            }
            else if (node.Expression is null)
            {
                if (node.Member is System.Reflection.FieldInfo staticFieldInfo)
                {
                    return staticFieldInfo.GetValue(null);
                }
                else if (node.Member is System.Reflection.PropertyInfo staticPropertyInfo)
                {
                    return staticPropertyInfo.GetValue(null);
                }
            }
            return null;
        }

        private bool IsVariableNode(MemberExpression node)
        {
            if (node.Expression is ConstantExpression)
            {
                return true;
            }
            else if (node.Expression is MemberExpression me)
            {
                return this.IsVariableNode(me);
            }
            else if (node.Expression is null)
            {
                if (node.Member is System.Reflection.FieldInfo staticFieldInfo)
                {
                    return staticFieldInfo.IsStatic;
                }
                else if (node.Member is System.Reflection.PropertyInfo staticPropertyInfo)
                {
                    var getMethod = staticPropertyInfo.GetGetMethod();
                    return getMethod != null && getMethod.IsStatic;
                }
            }
            return false;
        }
    }
}
