using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using Atis.Orm.Abstractions;
namespace Atis.Orm.Services
{
    public class OrmReflectionService : ReflectionService, IOrmReflectionService
    {
        public bool IsAsyncEnumerableType(Type type)
        {
            if (type == typeof(string)) return false;
            return type.GetInterfaces()
                       .Append(type)
                       .Any(x => x.IsGenericType &&
                                 x.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));
        }

        public Type GetAsyncElementType(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
                return type.GetGenericArguments()[0];
            return type.GetInterfaces()
                       .Where(x => x.IsGenericType &&
                                   x.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
                       .Select(x => x.GetGenericArguments()[0])
                       .FirstOrDefault();
        }

        /// <inheritdoc />
        public void SetPropertyOrFieldValue(object instance, MemberInfo propertyOrField, object value)
        {
            if (instance is null)
                throw new ArgumentNullException(nameof(instance));
            if (propertyOrField is null)
                throw new ArgumentNullException(nameof(propertyOrField));

            switch (propertyOrField)
            {
                case PropertyInfo property:
                    property.SetValue(instance, value);
                    break;
                case FieldInfo field:
                    field.SetValue(instance, value);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"'{propertyOrField.DeclaringType?.Name}.{propertyOrField.Name}' is neither a property nor a field, so it cannot be assigned.");
            }
        }

        /// <inheritdoc />
        public bool IsWriteableMember(MemberInfo propertyOrField)
        {
            if (propertyOrField is null)
                throw new ArgumentNullException(nameof(propertyOrField));

            switch (propertyOrField)
            {
                case PropertyInfo property:
                    return property.CanWrite;
                case FieldInfo field:
                    return !field.IsInitOnly;
                default:
                    return false;
            }
        }
    }
}
