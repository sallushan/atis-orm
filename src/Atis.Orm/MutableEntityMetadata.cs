using Atis.SqlExpressionEngine;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Atis.Orm
{
    internal class MutableEntityMetadata
    {
        public Type ClrType { get; }
        public string TableName { get; set; }
        public string Schema { get; set; }
        public string Database { get; set; }
        public string Server { get; set; }
        public List<MutableTableColumn> SqlColumns { get; }
        public Dictionary<string, MutableNavigationInfo> Navigations { get; }
        public Dictionary<string, LambdaExpression> CalculatedProperties { get; }

        public MutableEntityMetadata(EntityMetadata source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            this.ClrType = source.ClrType;
            this.TableName = source.Table.TableName;
            this.Schema = source.Table.Schema;
            this.Database = source.Table.Database;
            this.Server = source.Table.Server;
            this.SqlColumns = new List<MutableTableColumn>(source.SqlColumns.Select(x => new MutableTableColumn(x)));
            this.Navigations = new Dictionary<string, MutableNavigationInfo>(source.Navigations.ToDictionary(kv => kv.Key, kv => new MutableNavigationInfo(kv.Value)));
            this.CalculatedProperties = new Dictionary<string, LambdaExpression>(source.CalculatedProperties.ToDictionary(kv => kv.Key, kv => kv.Value));
        }

        public MutableTableColumn GetOrAddColumn(string propertyName)
        {
            var column = this.SqlColumns.FirstOrDefault(x => x.ModelPropertyName == propertyName);
            if (column == null)
            {
                column = new MutableTableColumn(propertyName, propertyName, isPrimaryKey: false);
                this.SqlColumns.Add(column);
            }
            return column;
        }

        // Navigation and calculated properties are not table columns. When seeding from an
        // un-annotated CLR type every property is initially treated as a column, so configuring
        // such a property fluently must drop it from the column collection.
        public void RemoveColumn(string propertyName)
        {
            this.SqlColumns.RemoveAll(x => x.ModelPropertyName == propertyName);
        }

        public EntityMetadata Build()
        {
            return new EntityMetadata(
                this.ClrType,
                new SqlTable(this.TableName, this.Schema, this.Database, this.Server),
                this.SqlColumns.Select(x => new TableColumn(x.DatabaseColumnName, x.ModelPropertyName, x.IsPrimaryKey)).ToArray(),
                this.Navigations.Values.Select(x => new NavigationInfo(x.NavigationType, x.JoinCondition, x.JoinedSource, x.PropertyName)).ToDictionary(x => x.PropertyName),
                this.CalculatedProperties
            );
        }
    }
}