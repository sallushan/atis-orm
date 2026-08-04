using Atis.SqlExpressionEngine.Visitors;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Atis.SqlExpressionEngine.SqlExpressions
{
    /// <summary>A single-row INSERT whose values are carried directly by the statement.</summary>
    public class SqlInsertExpression : SqlExpression
    {
        public SqlInsertExpression(
            SqlTable table,
            IReadOnlyList<string> columns,
            IReadOnlyList<SqlExpression> values,
            IReadOnlyList<SelectColumn> outputs)
        {
            this.Table = table ?? throw new ArgumentNullException(nameof(table));
            this.Columns = columns ?? throw new ArgumentNullException(nameof(columns));
            this.Values = values ?? throw new ArgumentNullException(nameof(values));
            this.Outputs = outputs ?? Array.Empty<SelectColumn>();

            if (columns.Count == 0)
                throw new ArgumentException("At least one column must be specified.", nameof(columns));
            if (columns.Count != values.Count)
                throw new ArgumentException("The number of columns must match the number of values.", nameof(columns));
        }

        public SqlTable Table { get; }
        public IReadOnlyList<string> Columns { get; }
        public IReadOnlyList<SqlExpression> Values { get; }
        public IReadOnlyList<SelectColumn> Outputs { get; }

        public override SqlExpressionType NodeType => SqlExpressionType.Insert;

        protected internal override SqlExpression Accept(SqlExpressionVisitor visitor)
        {
            return visitor.VisitSqlInsert(this);
        }

        public SqlInsertExpression Update(
            IReadOnlyList<SqlExpression> values,
            IReadOnlyList<SelectColumn> outputs)
        {
            if (values is null)
                throw new ArgumentNullException(nameof(values));
            // Matches the constructor: no outputs and an empty output list are the same statement.
            outputs = outputs ?? Array.Empty<SelectColumn>();

            if ((this.Values == values || this.Values.SequenceEqual(values))
                && (this.Outputs == outputs || this.Outputs.SequenceEqual(outputs)))
            {
                return this;
            }

            return new SqlInsertExpression(this.Table, this.Columns, values, outputs);
        }

        public override string ToString()
        {
            var output = this.Outputs.Count == 0
                ? string.Empty
                : $" output {string.Join(", ", this.Outputs)}";
            return $"insert into {this.Table.TableName} ({string.Join(", ", this.Columns)}){output} values ({string.Join(", ", this.Values)})";
        }
    }
}
