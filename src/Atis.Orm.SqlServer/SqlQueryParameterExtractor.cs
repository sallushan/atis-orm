using Atis.SqlExpressionEngine;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Atis.Orm.SqlServer
{
    public class SqlQueryParameterExtractor : SqlExpressionVisitor, IExpressionVariableValuesExtractor
    {
        private readonly List<SqlQueryParameter> queryParameters = new List<SqlQueryParameter>();
        public IReadOnlyList<IQueryParameter> ExtractQueryParameters(SqlExpression sqlExpression)
        {
            this.queryParameters.Clear();
            this.Visit(sqlExpression);
            return this.queryParameters.AsReadOnly();
        }

        protected override SqlExpression VisitSqlParameter(SqlParameterExpression node)
        {
            var sqlQueryParameter = this.SqlQueryParameter(node.Value, node, isLiteral: false);
            this.queryParameters.Add(sqlQueryParameter);
            return base.VisitSqlParameter(node);
        }

        protected override SqlExpression VisitSqlLiteral(SqlLiteralExpression node)
        {
            var sqlQueryParameter = this.SqlQueryParameter(node.LiteralValue, node, isLiteral: true);
            this.queryParameters.Add(sqlQueryParameter);
            return base.VisitSqlLiteral(node);
        }

        private SqlQueryParameter SqlQueryParameter(object value, SqlExpression node, bool isLiteral)
        {
            return new SqlQueryParameter(value, isLiteral, node, $"@q_param{this.queryParameters.Count + 1}");
        }
    }
}
