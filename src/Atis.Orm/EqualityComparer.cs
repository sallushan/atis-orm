using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Atis.Orm
{
    public class ExpressionEqualityComparer : IEqualityComparer<Expression>
    {
        private ExpressionEqualityComparer() { }

        public static ExpressionEqualityComparer Instance { get; } = new ExpressionEqualityComparer();

        public bool Equals(Expression x, Expression y)
            => new ExpressionComparer().Compare(x, y);

        public virtual int GetHashCode(Expression obj)
        {
            if (obj == null)
            {
                return 0;
            }

            unchecked
            {
                var hash = new HashCode();
                hash.Add(obj.NodeType);
                hash.Add(obj.Type);

                switch (obj.NodeType)
                {
                    case ExpressionType.Negate:
                    case ExpressionType.NegateChecked:
                    case ExpressionType.Not:
                    case ExpressionType.Convert:
                    case ExpressionType.ConvertChecked:
                    case ExpressionType.ArrayLength:
                    case ExpressionType.Quote:
                    case ExpressionType.TypeAs:
                    case ExpressionType.UnaryPlus:
                        {
                            var unaryExpression = (UnaryExpression)obj;

                            if (unaryExpression.Method != null)
                            {
                                hash.Add(unaryExpression.Method);
                            }

                            hash.Add(unaryExpression.Operand, this);

                            break;
                        }
                    case ExpressionType.Add:
                    case ExpressionType.AddChecked:
                    case ExpressionType.Subtract:
                    case ExpressionType.SubtractChecked:
                    case ExpressionType.Multiply:
                    case ExpressionType.MultiplyChecked:
                    case ExpressionType.Divide:
                    case ExpressionType.Modulo:
                    case ExpressionType.And:
                    case ExpressionType.AndAlso:
                    case ExpressionType.Or:
                    case ExpressionType.OrElse:
                    case ExpressionType.LessThan:
                    case ExpressionType.LessThanOrEqual:
                    case ExpressionType.GreaterThan:
                    case ExpressionType.GreaterThanOrEqual:
                    case ExpressionType.Equal:
                    case ExpressionType.NotEqual:
                    case ExpressionType.Coalesce:
                    case ExpressionType.ArrayIndex:
                    case ExpressionType.RightShift:
                    case ExpressionType.LeftShift:
                    case ExpressionType.ExclusiveOr:
                    case ExpressionType.Power:
                        {
                            var binaryExpression = (BinaryExpression)obj;

                            hash.Add(binaryExpression.Left, this);
                            hash.Add(binaryExpression.Right, this);

                            break;
                        }
                    case ExpressionType.TypeIs:
                        {
                            var typeBinaryExpression = (TypeBinaryExpression)obj;

                            hash.Add(typeBinaryExpression.Expression, this);
                            hash.Add(typeBinaryExpression.TypeOperand);

                            break;
                        }
                    case ExpressionType.Constant:
                        {
                            var constantExpression = (ConstantExpression)obj;

                            if (constantExpression.Value != null
                                && !(constantExpression.Value is IQueryable))
                            {
                                hash.Add(constantExpression.Value);
                            }

                            break;
                        }
                    case ExpressionType.Parameter:
                        {
                            var parameterExpression = (ParameterExpression)obj;

                            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                            if (parameterExpression.Name != null)
                            {
                                hash.Add(parameterExpression.Name);
                            }

                            break;
                        }
                    case ExpressionType.MemberAccess:
                        {
                            var memberExpression = (MemberExpression)obj;


                            if (memberExpression.Expression is ConstantExpression && (memberExpression.Member is PropertyInfo || memberExpression.Member is FieldInfo))
                            {
                                hash.Add(memberExpression.Expression.NodeType);
                                hash.Add(memberExpression.Member.Name);
                                hash.Add(GetMemberInfoType(memberExpression.Member));
                            }
                            else
                            {
                                hash.Add(memberExpression.Member);
                                hash.Add(memberExpression.Expression, this);
                            }


                            break;
                        }
                    case ExpressionType.Call:
                        {
                            var methodCallExpression = (MethodCallExpression)obj;

                            hash.Add(methodCallExpression.Method);
                            hash.Add(methodCallExpression.Object, this);
                            AddListToHash(ref hash, methodCallExpression.Arguments);

                            break;
                        }
                    case ExpressionType.Lambda:
                        {
                            var lambdaExpression = (LambdaExpression)obj;

                            hash.Add(lambdaExpression.ReturnType);
                            hash.Add(lambdaExpression.Body, this);
                            AddListToHash(ref hash, lambdaExpression.Parameters);

                            break;
                        }
                    case ExpressionType.New:
                        {
                            var newExpression = (NewExpression)obj;

                            hash.Add(newExpression.Constructor);

                            if (newExpression.Members != null)
                            {
                                for (var i = 0; i < newExpression.Members.Count; i++)
                                {
                                    hash.Add(newExpression.Members[i]);
                                }
                            }

                            AddListToHash(ref hash, newExpression.Arguments);

                            break;
                        }
                    case ExpressionType.NewArrayInit:
                    case ExpressionType.NewArrayBounds:
                        {
                            var newArrayExpression = (NewArrayExpression)obj;
                            AddListToHash(ref hash, newArrayExpression.Expressions);

                            break;
                        }
                    case ExpressionType.Invoke:
                        {
                            var invocationExpression = (InvocationExpression)obj;

                            hash.Add(invocationExpression.Expression, this);
                            AddListToHash(ref hash, invocationExpression.Arguments);

                            break;
                        }
                    case ExpressionType.MemberInit:
                        {
                            var memberInitExpression = (MemberInitExpression)obj;

                            hash.Add(memberInitExpression.NewExpression, this);

                            for (var i = 0; i < memberInitExpression.Bindings.Count; i++)
                            {
                                var memberBinding = memberInitExpression.Bindings[i];

                                hash.Add(memberBinding.Member);
                                hash.Add(memberBinding.BindingType);

                                switch (memberBinding.BindingType)
                                {
                                    case MemberBindingType.Assignment:
                                        var memberAssignment = (MemberAssignment)memberBinding;
                                        hash.Add(memberAssignment.Expression, this);
                                        break;
                                    case MemberBindingType.ListBinding:
                                        var memberListBinding = (MemberListBinding)memberBinding;
                                        for (var j = 0; j < memberListBinding.Initializers.Count; j++)
                                        {
                                            AddListToHash(ref hash, memberListBinding.Initializers[j].Arguments);
                                        }

                                        break;
                                    default:
                                        throw new NotImplementedException($"Unhandled binding type: {memberBinding}");
                                }
                            }

                            break;
                        }
                    case ExpressionType.ListInit:
                        {
                            var listInitExpression = (ListInitExpression)obj;

                            hash.Add(listInitExpression.NewExpression, this);

                            for (var i = 0; i < listInitExpression.Initializers.Count; i++)
                            {
                                AddListToHash(ref hash, listInitExpression.Initializers[i].Arguments);
                            }

                            break;
                        }
                    case ExpressionType.Conditional:
                        {
                            var conditionalExpression = (ConditionalExpression)obj;

                            hash.Add(conditionalExpression.Test, this);
                            hash.Add(conditionalExpression.IfTrue, this);
                            hash.Add(conditionalExpression.IfFalse, this);

                            break;
                        }
                    case ExpressionType.Default:
                        {
                            hash.Add(obj.Type);
                            break;
                        }
                    case ExpressionType.Extension:
                        {
                            hash.Add(obj);
                            break;
                        }
                    case ExpressionType.Index:
                        {
                            var indexExpression = (IndexExpression)obj;

                            hash.Add(indexExpression.Indexer);
                            hash.Add(indexExpression.Object, this);
                            AddListToHash(ref hash, indexExpression.Arguments);

                            break;
                        }
                    default:
                        throw new NotImplementedException($"Unhandled expression node type: {obj.NodeType}");
                }

                return hash.ToHashCode();
            }
        }

        static Type GetMemberInfoType(MemberInfo member)
        {
            return (member as PropertyInfo)?.PropertyType ?? (member as FieldInfo)?.FieldType;
        }

        private void AddListToHash<T>(ref HashCode hash, IReadOnlyList<T> expressions)
            where T : Expression
        {
            for (var i = 0; i < expressions.Count; i++)
            {
                hash.Add(expressions[i], this);
            }
        }

        private class ExpressionComparer
        {
            public bool Compare(Expression a, Expression b)
            {
                if (a == b)
                    return true;
                if (a == null || b == null)
                    return false;
                if (a.GetType() != b.GetType())
                    return false;
                if (a.NodeType != b.NodeType)
                    return false;
                if (a.Type != b.Type)
                    return false;

                switch (a.NodeType)
                {
                    case ExpressionType.Negate:
                    case ExpressionType.NegateChecked:
                    case ExpressionType.Not:
                    case ExpressionType.Convert:
                    case ExpressionType.ConvertChecked:
                    case ExpressionType.ArrayLength:
                    case ExpressionType.Quote:
                    case ExpressionType.TypeAs:
                    case ExpressionType.UnaryPlus:
                        return CompareUnary((UnaryExpression)a, (UnaryExpression)b);
                    case ExpressionType.Add:
                    case ExpressionType.AddChecked:
                    case ExpressionType.Subtract:
                    case ExpressionType.SubtractChecked:
                    case ExpressionType.Multiply:
                    case ExpressionType.MultiplyChecked:
                    case ExpressionType.Divide:
                    case ExpressionType.Modulo:
                    case ExpressionType.And:
                    case ExpressionType.AndAlso:
                    case ExpressionType.Or:
                    case ExpressionType.OrElse:
                    case ExpressionType.LessThan:
                    case ExpressionType.LessThanOrEqual:
                    case ExpressionType.GreaterThan:
                    case ExpressionType.GreaterThanOrEqual:
                    case ExpressionType.Equal:
                    case ExpressionType.NotEqual:
                    case ExpressionType.Coalesce:
                    case ExpressionType.ArrayIndex:
                    case ExpressionType.RightShift:
                    case ExpressionType.LeftShift:
                    case ExpressionType.ExclusiveOr:
                    case ExpressionType.Power:
                        return CompareBinary((BinaryExpression)a, (BinaryExpression)b);
                    case ExpressionType.TypeIs:
                        return CompareTypeIs((TypeBinaryExpression)a, (TypeBinaryExpression)b);
                    case ExpressionType.Conditional:
                        return CompareConditional((ConditionalExpression)a, (ConditionalExpression)b);
                    case ExpressionType.Constant:
                        return CompareConstant((ConstantExpression)a, (ConstantExpression)b);
                    case ExpressionType.Parameter:
                        return CompareParameter((ParameterExpression)a, (ParameterExpression)b);
                    case ExpressionType.MemberAccess:
                        return CompareMemberAccess((MemberExpression)a, (MemberExpression)b);
                    case ExpressionType.Call:
                        return CompareMethodCall((MethodCallExpression)a, (MethodCallExpression)b);
                    case ExpressionType.Lambda:
                        return CompareLambda((LambdaExpression)a, (LambdaExpression)b);
                    case ExpressionType.New:
                        return CompareNew((NewExpression)a, (NewExpression)b);
                    case ExpressionType.NewArrayInit:
                    case ExpressionType.NewArrayBounds:
                        return CompareNewArray((NewArrayExpression)a, (NewArrayExpression)b);
                    case ExpressionType.Invoke:
                        return CompareInvocation((InvocationExpression)a, (InvocationExpression)b);
                    case ExpressionType.MemberInit:
                        return CompareMemberInit((MemberInitExpression)a, (MemberInitExpression)b);
                    case ExpressionType.ListInit:
                        return CompareListInit((ListInitExpression)a, (ListInitExpression)b);
                    case ExpressionType.Extension:
                        return CompareExtension(a, b);
                    case ExpressionType.Default:
                        return CompareDefault((DefaultExpression)a, (DefaultExpression)b);
                    case ExpressionType.Index:
                        return CompareIndex((IndexExpression)a, (IndexExpression)b);
                    default:
                        throw new NotImplementedException($"Node Type '{a.NodeType}' is not yet implemented");
                }
            }

            private bool CompareIndex(IndexExpression a, IndexExpression b)
            {
                return Compare(a.Object, b.Object)
                        &&
                        Equals(a.Indexer, b.Indexer)
                        &&
                        CompareExpressions(a.Arguments, b.Arguments);
            }

            private bool CompareDefault(DefaultExpression a, DefaultExpression b)
                => a.Type == b.Type;

            private bool CompareExtension(Expression a, Expression b)
                => a.Equals(b);

            private bool CompareListInit(ListInitExpression a, ListInitExpression b)
            {
                return Compare(a.NewExpression, b.NewExpression)
                        &&
                        CompareInitializers(a.Initializers, b.Initializers)
                        ;
            }

            private bool CompareMemberInit(MemberInitExpression a, MemberInitExpression b)
            {
                return Compare(a.NewExpression, b.NewExpression)
                        &&
                        CompareBindings(a.Bindings, b.Bindings)
                        ;
            }

            private bool CompareMemberAssignment(MemberAssignment a, MemberAssignment b)
            {
                return Equals(a.Member, b.Member)
                        &&
                        Compare(a.Expression, b.Expression);
            }

            private bool CompareMemberListBinding(MemberListBinding a, MemberListBinding b)
            {
                return Equals(a.Member, b.Member)
                        &&
                        CompareInitializers(a.Initializers, b.Initializers);
            }

            private bool CompareInitializers(IReadOnlyList<ElementInit> a, IReadOnlyList<ElementInit> b)
            {
                if (a == b)
                    return true;
                if (a == null || b == null)
                    return false;
                if (a.Count != b.Count)
                    return false;
                for (var i = 0; i < a.Count; i++)
                    if (
                            !(
                                Equals(a[i].AddMethod, b[i].AddMethod)
                                &&
                                CompareExpressions(a[i].Arguments, b[i].Arguments)
                            )
                        )
                        return false;
                return true;
            }


            private bool CompareMemberMemberBinding(MemberMemberBinding a, MemberMemberBinding b)
            {
                return Equals(a.Member, b.Member)
                        &&
                        CompareBindings(a.Bindings, b.Bindings);
            }

            private bool CompareBindings(IReadOnlyList<MemberBinding> a, IReadOnlyList<MemberBinding> b)
            {
                if (a == b)
                    return true;
                if (a == null || b == null)
                    return false;
                if (a.Count != b.Count)
                    return false;
                for (var i = 0; i < a.Count; i++)
                {
                    if (!CompareBinding(a[i], b[i]))
                        return false;
                }
                return true;
            }

            private bool CompareBinding(MemberBinding a, MemberBinding b)
            {
                if (a == b)
                    return true;

                if (a == null || b == null)
                    return false;

                if (a.BindingType != b.BindingType)
                    return false;

                if (!Equals(a.Member, b.Member))
                    return false;

                switch (a.BindingType)
                {
                    case MemberBindingType.Assignment:
                        return CompareMemberAssignment((MemberAssignment)a, (MemberAssignment)b);
                    case MemberBindingType.ListBinding:
                        return CompareMemberListBinding((MemberListBinding)a, (MemberListBinding)b);
                    case MemberBindingType.MemberBinding:
                        return CompareMemberMemberBinding((MemberMemberBinding)a, (MemberMemberBinding)b);
                    default:
                        throw new InvalidOperationException("Unhandled member binding type: " + a.BindingType);
                }
            }

            private bool CompareInvocation(InvocationExpression a, InvocationExpression b)
            {
                return Compare(a.Expression, b.Expression)
                        &&
                        CompareExpressions(a.Arguments, b.Arguments);
            }

            private bool CompareNewArray(NewArrayExpression a, NewArrayExpression b)
            {
                return CompareExpressions(a.Expressions, b.Expressions);
            }

            private bool CompareNew(NewExpression a, NewExpression b)
            {
                #region compare Members
                if (a.Members == b.Members)
                    return true;

                if (a.Members == null || b.Members == null)
                    return false;

                if (a.Members.Count != b.Members.Count)
                    return false;

                for (var i = 0; i < a.Members.Count; i++)
                {
                    if (!Equals(a.Members[i], b.Members[i]))
                        return false;
                }
                #endregion

                return Equals(a.Constructor, b.Constructor)
                        &&
                        CompareExpressions(a.Arguments, b.Arguments)
                        ;
            }

            private readonly Dictionary<ParameterExpression, ParameterExpression> _lambdaParams = new Dictionary<ParameterExpression, ParameterExpression>();

            private bool CompareLambda(LambdaExpression a, LambdaExpression b)
            {
                if (a.Parameters.Count != b.Parameters.Count)
                    return false;

                for (var i = 0; i < a.Parameters.Count; i++)
                    if (
                            !(
                                a.Parameters[i].Type == b.Parameters[i].Type
                                && a.Parameters[i].Name == b.Parameters[i].Name
                            )
                        )
                        return false;

                for (var i = 0; i < a.Parameters.Count; i++)
                {
                    this._lambdaParams.Add(a.Parameters[i], b.Parameters[i]);
                }

                try
                {
                    return Compare(a.Body, b.Body);
                }
                finally
                {
                    for (var i = 0; i < a.Parameters.Count; i++)
                    {
                        this._lambdaParams.Remove(a.Parameters[i]);
                    }
                }
            }

            private bool CompareExpressions(IReadOnlyList<Expression> a, IReadOnlyList<Expression> b)
            {
                if (Equals(a, b))
                    return true;
                if (a == null || b == null)
                    return false;
                if (a.Count != b.Count)
                    return false;
                for (var i = 0; i < a.Count; i++)
                {
                    if (!this.Compare(a[i], b[i]))
                        return false;
                }
                return true;
            }

            private bool CompareMethodCall(MethodCallExpression a, MethodCallExpression b)
            {
                return Equals(a.Method, b.Method)
                        &&
                        CompareExpressions(a.Arguments, b.Arguments)
                        &&
                        Compare(a.Object, b.Object)
                        ;
            }

            private bool CompareMemberAccess(MemberExpression a, MemberExpression b)
            {
                return (
                            a.Expression is ConstantExpression && b.Expression is ConstantExpression
                            &&
                            (a.Member is FieldInfo || a.Member is PropertyInfo)
                            &&
                            (b.Member is FieldInfo || b.Member is PropertyInfo)
                            &&
                            a.Member.Name == b.Member.Name
                            &&
                            GetMemberInfoType(a.Member) == GetMemberInfoType(b.Member)
                         )
                         ||
                        (
                        Equals(a.Member, b.Member)
                        &&
                        Compare(a.Expression, b.Expression)
                        )
                        ;
            }

            private bool CompareParameter(ParameterExpression a, ParameterExpression b)
            {
                if (a == b)
                    return true;
                if (this._lambdaParams.TryGetValue(a, out var otherParam))
                {
                    if (otherParam == b)
                        return true;
                }
                if (a.Type == b.Type && a.Name == b.Name)
                    return true;
                return false;
            }

            private bool CompareConstant(ConstantExpression a, ConstantExpression b)
            {
                return Equals(a.Value, b.Value);
            }

            private bool CompareConditional(ConditionalExpression a, ConditionalExpression b)
            {
                return this.Compare(a.Test, b.Test)
                        &&
                        this.Compare(a.IfTrue, b.IfTrue)
                        &&
                        this.Compare(a.IfFalse, b.IfFalse);
            }

            private bool CompareTypeIs(TypeBinaryExpression a, TypeBinaryExpression b)
            {
                return a.TypeOperand == b.TypeOperand
                        &&
                        Compare(a.Expression, b.Expression);
            }

            private bool CompareBinary(BinaryExpression a, BinaryExpression b)
            {
                return Compare(a.Left, b.Left)
                        &&
                        Compare(a.Right, b.Right)
                        &&
                        Equals(a.Method, b.Method);
            }

            private bool CompareUnary(UnaryExpression a, UnaryExpression b)
            {
                return Compare(a.Operand, b.Operand)
                        &&
                        Equals(a.Method, b.Method)
                        ;
            }
        }
    }
}
