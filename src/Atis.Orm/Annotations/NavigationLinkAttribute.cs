using Atis.SqlExpressionEngine;
using System;
using System.Collections.Generic;
using System.Text;

namespace Atis.Orm.Annotations
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class NavigationLinkAttribute : Attribute
    {
        public IReadOnlyList<string> ParentKeys { get; }
        public IReadOnlyList<string> ForeignKeysInChild { get; }
        public NavigationType NavigationType { get; }

        public NavigationLinkAttribute(NavigationType navigationType, string parentKey, string foreignKeyInChild)
        {
            this.NavigationType = navigationType;
            this.ParentKeys = new[] { parentKey };
            this.ForeignKeysInChild = new[] { foreignKeyInChild };
        }

        public NavigationLinkAttribute(NavigationType navigationType, string[] parentKeys, string[] foreignKeysInChild)
        {
            this.ParentKeys = parentKeys;
            this.ForeignKeysInChild = foreignKeysInChild;
            this.NavigationType = navigationType;
        }
    }
}
