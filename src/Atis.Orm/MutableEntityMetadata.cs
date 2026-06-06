using Atis.SqlExpressionEngine;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Atis.Orm
{
    internal class MutableEntityMetadata
    {
        public Type ClrType { get; }
        public SqlTable Table { get; set; }
        public List<TableColumn> SqlColumns { get; set; }
        public Dictionary<string, NavigationInfo> Navigations { get; set; }
        public Dictionary<string, LambdaExpression> CalculatedProperties { get; set; }

        public MutableEntityMetadata(EntityMetadata source)
        {
            this.ClrType = source.ClrType;
            this.Table = source.Table;
            this.SqlColumns = new List<TableColumn>(source.SqlColumns);
            this.Navigations = new Dictionary<string, NavigationInfo>(source.Navigations.ToDictionary(kv => kv.Key, kv => kv.Value));
            this.CalculatedProperties = new Dictionary<string, LambdaExpression>(source.CalculatedProperties.ToDictionary(kv => kv.Key, kv => kv.Value));
        }

        public EntityMetadata Build()
        {
            return new EntityMetadata(this.ClrType, this.Table, this.SqlColumns, this.Navigations, this.CalculatedProperties);
        }
    }
}
