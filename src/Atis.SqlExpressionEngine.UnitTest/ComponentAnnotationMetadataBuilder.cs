using Atis.Orm;
using Atis.Orm.Annotations;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Atis.Orm.Metadata;
namespace Atis.SqlExpressionEngine.UnitTest
{
    public class ComponentAnnotationMetadataBuilder : EntityMetadataBuilder
    {
        public ComponentAnnotationMetadataBuilder(IReflectionService reflectionService) : base(reflectionService)
        {
        }

        protected override string GetColumnName(PropertyInfo propertyInfo)
        {
            var columnAttribute = propertyInfo.GetCustomAttribute<ColumnAttribute>();
            return columnAttribute?.Name ?? propertyInfo.Name;
        }

        protected override SqlTable GetSqlTable(Type type)
        {
            var tableAttribute = type.GetCustomAttribute<TableAttribute>();
            return new SqlTable(tableAttribute?.Name ?? type.Name, tableAttribute?.Schema, null, null);
        }
    }
}
