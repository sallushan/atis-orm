using System;

namespace Atis.Orm.Annotations
{
    /// <summary>
    ///     <para>
    ///         Marks a method (or a delegate-typed static field) as a query macro: when it is called inside a
    ///         query, the call is replaced by the body of an expression-tree lambda, with the call's
    ///         arguments substituted for the lambda's parameters.
    ///     </para>
    ///     <para>
    ///         The lambda is held in a <c>static</c> field or property of the same class, named by
    ///         <see cref="ExpressionMember"/>. The marked member can still be called as normal C# outside
    ///         a query, so one definition can serve both the in-memory and the SQL side.
    ///     </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class ExpressionMacroAttribute : Attribute
    {
        /// <summary>
        ///     Creates the attribute.
        /// </summary>
        /// <param name="expressionMember">
        ///     Name of the static field or property, on the same class, that holds the
        ///     <see cref="System.Linq.Expressions.LambdaExpression"/> to inline.
        /// </param>
        public ExpressionMacroAttribute(string expressionMember)
        {
            this.ExpressionMember = expressionMember;
        }

        /// <summary>
        ///     Name of the static field or property that holds the lambda to inline.
        /// </summary>
        public string ExpressionMember { get; }
    }
}
