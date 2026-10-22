using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Atis.SqlExpressionEngine.Visitors;
namespace Atis.SqlExpressionEngine.SqlExpressions
{
    public class SqlSelectListExpression : SqlExpression
    {
        public SqlSelectListExpression(IReadOnlyList<SelectColumn> selectColumns)
        {
            if (!(selectColumns?.Count > 0))
                throw new ArgumentNullException(nameof(selectColumns));
            if (selectColumns.Any(x => x.ColumnExpression is SqlSelectListExpression))
                throw new ArgumentException("Select Columns cannot contain select list expressions.", nameof(selectColumns));
            if (selectColumns.GroupBy(x=>x.Alias).Any(x=>x.Count() > 1))
                throw new ArgumentException("Duplicate column names in select columns", nameof(selectColumns));
            this.SelectColumns = selectColumns;
            this.ScalarSelectColumn = FindScalarColumn(selectColumns);
        }
        /// <inheritdoc />
        public override SqlExpressionType NodeType => SqlExpressionType.SelectColumnCollection;
        public IReadOnlyList<SelectColumn> SelectColumns { get; }

        /// <summary>
        ///     The column of a scalar select list, <c>null</c> if the select list is not a scalar one, see <see cref="FindScalarColumn"/>.
        /// </summary>
        public SelectColumn ScalarSelectColumn { get; }

        /// <summary>
        ///     The select list returns a single value per row.
        /// </summary>
        public bool IsScalar => this.ScalarSelectColumn != null;

        /// <summary>
        ///     The one rule that decides whether a select list is a scalar one: it has exactly one column that is part of
        ///     the projection and that column is scalar. Helper columns are not part of the projection, so they may sit
        ///     next to it.
        /// </summary>
        public static SelectColumn FindScalarColumn(IReadOnlyList<SelectColumn> selectColumns)
        {
            SelectColumn found = null;
            foreach (var column in selectColumns)
            {
                if (column.HelperColumn)
                    continue;
                if (found != null)
                    return null;
                found = column;
            }
            return found != null && found.ScalarColumn ? found : null;
        }


        /// <inheritdoc />
        protected internal override SqlExpression Accept(SqlExpressionVisitor visitor)
        {
            return visitor.VisitSqlSelectList(this);
        }

        public SqlSelectListExpression Update(SelectColumn[] selectColumns)
        {
            if (this.SelectColumns.AllEqual(selectColumns))
                return this;
            return new SqlSelectListExpression(selectColumns);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return string.Join(", ", this.SelectColumns.Select(x => x.ToString()));
        }
    }
}
