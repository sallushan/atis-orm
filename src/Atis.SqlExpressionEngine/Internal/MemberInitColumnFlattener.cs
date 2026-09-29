using System;
using System.Collections.Generic;
using System.Linq;

using Atis.SqlExpressionEngine.SqlExpressions;

namespace Atis.SqlExpressionEngine.Internal
{
    /// <summary>
    ///     <para>
    ///         Turns the <c>new T { A = 1, Vo = new V { B = 2 } }</c> assignment list of an Insert or an
    ///         Update into flat (column, value) pairs.
    ///     </para>
    ///     <para>
    ///         A value object is stored as several columns of the entity's own table, keyed in the table by a
    ///         dotted property name (<c>Vo.B</c>), so a nested member-init is flattened by joining the member
    ///         names with a dot. A value object assigned <c>null</c> writes <c>NULL</c> to every column it
    ///         owns.
    ///     </para>
    /// </summary>
    internal static class MemberInitColumnFlattener
    {
        public static void Flatten(
            SqlTableExpression table,
            SqlMemberInitExpression memberInit,
            Func<object, SqlExpression> createLiteral,
            out string[] columns,
            out SqlExpression[] values)
        {
            var columnList = new List<string>();
            var valueList = new List<SqlExpression>();
            Flatten(table, memberInit, null, createLiteral, columnList, valueList);
            columns = columnList.ToArray();
            values = valueList.ToArray();
        }

        private static void Flatten(
            SqlTableExpression table,
            SqlMemberInitExpression memberInit,
            string prefix,
            Func<object, SqlExpression> createLiteral,
            List<string> columns,
            List<SqlExpression> values)
        {
            foreach (var binding in memberInit.Bindings)
            {
                var name = prefix is null ? binding.MemberName : prefix + "." + binding.MemberName;

                if (binding.SqlExpression is SqlMemberInitExpression nested)
                {
                    Flatten(table, nested, name, createLiteral, columns, values);
                    continue;
                }

                if (binding.SqlExpression is SqlLiteralExpression literal && literal.LiteralValue is null)
                {
                    var ownedColumns = table.TableColumns
                                            .Where(x => x.ModelPropertyName.StartsWith(name + ".", StringComparison.Ordinal))
                                            .ToArray();
                    if (ownedColumns.Length > 0)
                    {
                        foreach (var ownedColumn in ownedColumns)
                        {
                            columns.Add(ownedColumn.DatabaseColumnName);
                            values.Add(createLiteral(null));
                        }
                        continue;
                    }
                }

                columns.Add(table.GetByPropertyName(name));
                values.Add(binding.SqlExpression);
            }
        }
    }
}
