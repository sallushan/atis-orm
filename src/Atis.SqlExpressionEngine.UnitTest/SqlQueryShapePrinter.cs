using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Atis.SqlExpressionEngine.UnitTest
{
    public class SqlQueryShapePrinter : SqlExpressionVisitor
    {
        private readonly StringBuilder sb = new();
        private int currentIdentLevel = 0;

        private void AppendLine(string text)
        {
            sb.AppendLine($"{new string('\t', currentIdentLevel)}{text}");
        }

        private IReadOnlyList<SelectColumn> projectedColumns;

        public string PrintShape(SqlExpression shapeExpression, IReadOnlyList<SelectColumn> projectedColumns)
        {
            sb.Clear();
            currentIdentLevel = 0;
            this.projectedColumns = projectedColumns ?? throw new ArgumentNullException(nameof(projectedColumns));
            Visit(shapeExpression);
            return sb.ToString();
        }

        protected override SqlExpression VisitSqlMemberInit(SqlMemberInitExpression node)
        {
            AppendLine($"SqlMemberInitExpression: (Bindings:{node.Bindings.Count}, IsScalar:{node.IsScalar}, NodeType:{node.NodeType})");
            currentIdentLevel++;
            for(var i = 0; i < node.Bindings.Count; i++)
            {
                var binding = node.Bindings[i];
                AppendLine($"Bindings[{i}]: {binding.GetType().Name}: (MemberName:{binding.MemberName}, Projectable:{binding.Projectable}, SqlExpression:{binding.SqlExpression.GetType().Name})");
                currentIdentLevel++;
                Visit(binding.SqlExpression);
                currentIdentLevel--;
            }
            currentIdentLevel--;
            return node;
        }

        protected override SqlExpression VisitSqlDataSourceColumn(SqlDataSourceColumnExpression node)
        {
            var matchedProjection = projectedColumns.FirstOrDefault(c => c.ColumnExpression == node);
            AppendLine($"SqlDataSourceColumnExpression: (DataSourceAlias:{node.DataSourceAlias}, ColumnName:{node.ColumnName}, ProjectedAlias:{matchedProjection?.Alias})");
            return node;
        }

        protected override SqlExpression VisitDataSourceQueryShape(SqlDataSourceQueryShapeExpression node)
        {
            AppendLine($"SqlDataSourceQueryShapeExpression: (DataSource:{node.DataSourceAlias}, IsScalar:{node.IsScalar}, NodeType:{node.NodeType}, ShapeExpression:{node.ShapeExpression.GetType().Name})");
            currentIdentLevel++;
            Visit(node.ShapeExpression);
            currentIdentLevel--;
            return node;
        }
    }
}
