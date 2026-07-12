using System;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine
{
    public enum NavigationType
    {
        ToParent,
        ToParentOptional,
        ToChildren,
        ToSingleChild,
    }
    public class NavigationInfo
    {
        public NavigationType NavigationType { get; }
        /// <summary>
        ///     <para>
        ///         Gets or sets the Join Condition for the navigation property.
        ///     </para>
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         This property must be set like this,
        ///     </para>
        ///     <code>
        ///         (parentEntity, childEntity) => parentEntity.PK == childEntity.FK
        ///     </code>
        /// </remarks>
        public LambdaExpression JoinCondition { get; }
        /// <summary>
        ///     <para>
        ///         Gets the data source of the related entity, a lambda of the form
        ///         <c>(thisEntity) =&gt; IQueryable&lt;TRelated&gt;</c>. Never <c>null</c>: for key-based
        ///         navigations the body is a plain query root over the related type, while custom
        ///         relations may supply a correlated query.
        ///     </para>
        /// </summary>
        public LambdaExpression JoinedSource { get; }
        public string PropertyName { get; }
        /// <param name="navigationType">The kind of navigation.</param>
        /// <param name="joinCondition">The <c>(parent, child)</c> join predicate; may be <c>null</c> for
        /// correlated sub-query navigations (OUTER APPLY).</param>
        /// <param name="joinedSource">The related data source lambda; required.</param>
        /// <param name="propertyName">The navigation property's name; required.</param>
        public NavigationInfo(NavigationType navigationType, LambdaExpression joinCondition, LambdaExpression joinedSource, string propertyName)
        {
            NavigationType = navigationType;
            this.JoinCondition = joinCondition;
            this.JoinedSource = joinedSource ?? throw new ArgumentNullException(nameof(joinedSource));
            this.PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
        }
    }
}