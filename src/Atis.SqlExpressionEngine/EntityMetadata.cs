using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Atis.SqlExpressionEngine
{
    public class EntityMetadata
    {
        public EntityMetadata(Type clrType, SqlTable table, IReadOnlyList<TableColumn> sqlColumns, IReadOnlyDictionary<string, NavigationInfo> navigations)
        {
            this.ClrType = clrType ?? throw new ArgumentNullException(nameof(clrType));
            this.Table = table ?? throw new ArgumentNullException(nameof(table));
            this.SqlColumns = sqlColumns ?? throw new ArgumentNullException(nameof(sqlColumns));
            this.Navigations = navigations ?? throw new ArgumentNullException(nameof(navigations));
        }

        public Type ClrType { get; }
        public SqlTable Table { get; }
        public IReadOnlyList<TableColumn> SqlColumns { get; }
        public IReadOnlyDictionary<string, NavigationInfo> Navigations { get; }
    }
}
