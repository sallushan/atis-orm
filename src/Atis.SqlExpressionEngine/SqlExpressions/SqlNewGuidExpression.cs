using System;
using System.Collections.Generic;
using System.Text;

using Atis.SqlExpressionEngine.Visitors;
namespace Atis.SqlExpressionEngine.SqlExpressions
{
    public class SqlNewGuidExpression : SqlExpression
    {
        /// <inheritdoc />
        public override SqlExpressionType NodeType => SqlExpressionType.NewGuid;

        protected internal override SqlExpression Accept(SqlExpressionVisitor visitor)
        {
            return visitor.VisitSqlNewGuid(this);
        }

        public override string ToString()
        {
            return "newId()";
        }
    }
}
