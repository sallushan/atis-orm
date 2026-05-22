using System;
using System.Collections.Generic;
using System.Text;

namespace Atis.Orm.Annotations
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class DbColumnAttribute : Attribute
    {
        public DbColumnAttribute(string columnName)
        {
            this.ColumnName = columnName;
        }

        public string ColumnName { get; }
    }
}
