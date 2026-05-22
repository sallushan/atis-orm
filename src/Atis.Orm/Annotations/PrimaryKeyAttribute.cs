using System;
using System.Collections.Generic;
using System.Text;

namespace Atis.Orm.Annotations
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class PrimaryKeyAttribute : Attribute
    {
    }
}
