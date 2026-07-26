using Atis.SqlExpressionEngine;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Atis.Orm.Metadata
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
            : this(source, crudSource: null)
        {
        }

        /// <param name="source">The query side of the mapping, as built from annotations.</param>
        /// <param name="crudSource">
        ///     The persistence side of the mapping, as built from annotations. Its column kinds and
        ///     required flags seed the mutable state, so that a fluent call overrides an annotation
        ///     rather than the other way round. May be <c>null</c>, in which case every column starts
        ///     out as <see cref="ColumnKind.Regular"/>.
        /// </param>
        public MutableEntityMetadata(EntityMetadata source, EntityCrudMetadata crudSource)
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

            if (crudSource != null)
            {
                foreach (var column in this.SqlColumns)
                {
                    if (crudSource.TryGetColumn(column.ModelPropertyName, out var crudColumn))
                    {
                        column.Kind = crudColumn.Kind;
                        column.IsRequired = crudColumn.IsRequired;
                        column.RequiredFieldTitle = crudColumn.RequiredFieldTitle;
                    }
                }
            }
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

        /// <summary>
        ///     Builds the query side of the mapping. Column kinds are deliberately absent — they are a
        ///     persistence concept and belong to <see cref="BuildCrud"/>.
        /// </summary>
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

        /// <summary>
        ///     <para>
        ///         Builds the persistence side of the mapping, in the same column order as
        ///         <see cref="Build"/>.
        ///     </para>
        ///     <para>
        ///         A column whose property cannot be resolved on <see cref="ClrType"/> is omitted: it is
        ///         still a perfectly usable query column, and there is nothing for Insert or Update to
        ///         read or write. The omission surfaces as a mapping error when — and only when — the
        ///         entity is first used for entity level CRUD.
        ///     </para>
        /// </summary>
        public EntityCrudMetadata BuildCrud()
        {
            var columns = new List<CrudColumn>(this.SqlColumns.Count);
            foreach (var column in this.SqlColumns)
            {
                var property = this.ClrType.GetProperty(column.ModelPropertyName);
                if (property is null)
                    continue;
                columns.Add(new CrudColumn(property, column.Kind, column.IsRequired, column.RequiredFieldTitle));
            }
            return new EntityCrudMetadata(this.ClrType, columns);
        }
    }
}