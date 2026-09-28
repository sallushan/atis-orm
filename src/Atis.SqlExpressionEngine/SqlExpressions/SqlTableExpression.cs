using System;
using System.Collections.Generic;
using System.Linq;

using Atis.SqlExpressionEngine.Visitors;
namespace Atis.SqlExpressionEngine.SqlExpressions
{
    /// <summary>
    /// Represents a table in the SQL query.
    /// </summary>
    public class SqlTableExpression : SqlQuerySourceExpression
    {
        private readonly Dictionary<string, string> propertyMap;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sqlTable"></param>
        /// <param name="tableColumns"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public SqlTableExpression(SqlTable sqlTable, IReadOnlyList<TableColumn> tableColumns)
        {
            this.SqlTable = sqlTable ?? throw new ArgumentNullException(nameof(sqlTable));
            this.TableColumns = tableColumns ?? throw new ArgumentNullException(nameof(tableColumns));
            this.propertyMap = tableColumns.ToDictionary(x => x.ModelPropertyName, x => x.DatabaseColumnName);
        }

        /// <summary>
        /// 
        /// </summary>
        public override SqlExpressionType NodeType => SqlExpressionType.Table;

        /// <summary>
        /// 
        /// </summary>
        public SqlTable SqlTable { get; }

        /// <summary>
        /// 
        /// </summary>
        public IReadOnlyList<TableColumn> TableColumns { get; }

        /// <inheritdoc />
        public override SqlDataSourceQueryShapeExpression CreateQueryShape(Guid dataSourceAlias)
        {
            var memberInit = new SqlMemberInitExpression(this.CreateBindings(dataSourceAlias));
            return new SqlDataSourceQueryShapeExpression(memberInit, dataSourceAlias);
        }

        /// <summary>
        ///     <para>
        ///         Builds one <see cref="SqlMemberAssignment"/> per plain column, plus one per
        ///         value-object-typed property — a value object's <see cref="TableColumn.ModelPropertyName"/>
        ///         is a dotted path (<c>"{PropertyName}.{ValueObjectMemberName}"</c>), grouped here by the
        ///         segment before the first <c>.</c> into a single nested <see cref="SqlMemberInitExpression"/>
        ///         binding, so <c>x.ValueObjectProperty.Member</c> resolves through the same two ordinary
        ///         <c>MemberExpressionConverter</c> lookups that any other nested member-init shape does —
        ///         no other part of the engine needs to know value objects exist.
        ///     </para>
        /// </summary>
        private IReadOnlyList<SqlMemberAssignment> CreateBindings(Guid dataSourceAlias)
        {
            var bindings = new List<SqlMemberAssignment>(this.TableColumns.Count);
            var valueObjectGroups = new Dictionary<string, List<SqlMemberAssignment>>();
            var valueObjectOrder = new List<string>();

            foreach (var tableColumn in this.TableColumns)
            {
                var dotIndex = tableColumn.ModelPropertyName.IndexOf('.');
                if (dotIndex < 0)
                {
                    bindings.Add(new SqlMemberAssignment(tableColumn.ModelPropertyName, new SqlDataSourceColumnExpression(dataSourceAlias, tableColumn.DatabaseColumnName)));
                    continue;
                }

                var outerPropertyName = tableColumn.ModelPropertyName.Substring(0, dotIndex);
                var voMemberName = tableColumn.ModelPropertyName.Substring(dotIndex + 1);
                if (!valueObjectGroups.TryGetValue(outerPropertyName, out var voBindings))
                {
                    voBindings = new List<SqlMemberAssignment>();
                    valueObjectGroups.Add(outerPropertyName, voBindings);
                    valueObjectOrder.Add(outerPropertyName);
                }
                voBindings.Add(new SqlMemberAssignment(voMemberName, new SqlDataSourceColumnExpression(dataSourceAlias, tableColumn.DatabaseColumnName)));
            }

            foreach (var outerPropertyName in valueObjectOrder)
            {
                bindings.Add(new SqlMemberAssignment(outerPropertyName, new SqlMemberInitExpression(valueObjectGroups[outerPropertyName])));
            }

            return bindings;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public string GetByPropertyName(string propertyName)
        {
            if (this.propertyMap.TryGetValue(propertyName, out var columnName))
                return columnName;
            throw new InvalidOperationException($"Property '{propertyName}' not found in table '{this.SqlTable}'.");
        }

        /// <inheritdoc />
        protected internal override SqlExpression Accept(SqlExpressionVisitor sqlExpressionVisitor)
        {
            return sqlExpressionVisitor.VisitSqlTable(this);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return this.SqlTable.TableName;
        }
    }
}
