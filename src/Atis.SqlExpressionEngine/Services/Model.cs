using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Atis.SqlExpressionEngine.Services
{
    /// <summary>
    ///     <para>
    ///         Default implementation of <see cref="IModel"/>.
    ///     </para>
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This class simply assumes that all the properties in given type as columns.
    ///     </para>
    ///     <para>
    ///         Similarly, it assumes that the table name is the same as the type name.
    ///     </para>
    /// </remarks>
    public class Model : IModel
    {
        public virtual EntityMetadata GetEntity(Type type)
        {
            var properties = type.GetProperties();
            return new EntityMetadata(
                clrType: type,
                table: new SqlTable(type.Name),
                sqlColumns: properties.Select(p => new TableColumn(p.Name, p.Name)).ToArray(),
                navigations: new Dictionary<string, NavigationInfo>(), // No navigations by default
                calculatedProperties: new Dictionary<string, LambdaExpression>() // No calculated properties by default
            );
        }

        public virtual MemberInfo GetMember(EntityMetadata entity, TableColumn column)
        {
            return entity.ClrType.GetProperty(column.ModelPropertyName);
        }
    }
}
