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
        /// <inheritdoc />
        /// <remarks>
        ///     Derives the mapping every time rather than looking one up, which is consistent with
        ///     <see cref="CanBeEntity(Type)"/> accepting everything: there is no type this model would
        ///     call an entity and then fail to map.
        /// </remarks>
        public virtual EntityMetadata GetRequiredEntity(Type type)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));
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

        /// <inheritdoc />
        /// <remarks>
        ///     This model maps by convention alone, so every type qualifies. Override together with
        ///     <see cref="GetRequiredEntity(Type)"/> — deciding what counts as an entity and deriving its
        ///     mapping have to agree on the same rule.
        /// </remarks>
        public virtual bool CanBeEntity(Type type) => true;
    }
}
