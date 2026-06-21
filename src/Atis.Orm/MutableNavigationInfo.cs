using Atis.SqlExpressionEngine;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Atis.Orm
{
    internal class MutableNavigationInfo
    {
        public NavigationType NavigationType { get; set; }
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
        public LambdaExpression JoinCondition { get; set; }
        public LambdaExpression JoinedSource { get; set; }
        public string PropertyName { get; }

        public MutableNavigationInfo(NavigationInfo navigationInfo)
        {
            if (navigationInfo == null) throw new ArgumentNullException(nameof(navigationInfo));
            this.NavigationType = navigationInfo.NavigationType;
            this.JoinCondition = navigationInfo.JoinCondition;
            this.JoinedSource = navigationInfo.JoinedSource;
            this.PropertyName = navigationInfo.PropertyName ?? throw new ArgumentNullException(nameof(navigationInfo.PropertyName));
        }

        public MutableNavigationInfo(NavigationType navigationType, LambdaExpression joinCondition, LambdaExpression joinedSource, string propertyName)
        {
            NavigationType = navigationType;
            this.JoinCondition = joinCondition;
            this.JoinedSource = joinedSource;
            this.PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
        }
    }
}
