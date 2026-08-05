using Atis.SqlExpressionEngine.Abstractions;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Atis.Orm.Abstractions
{
    public interface IOrmReflectionService : IReflectionService
    {
        bool IsAsyncEnumerableType(Type type);
        Type GetAsyncElementType(Type type);

        /// <summary>
        ///     Writes <paramref name="value"/> into <paramref name="propertyOrField"/> of
        ///     <paramref name="instance"/>. Used to push database generated values back into an entity
        ///     after a write; the expression engine never reads or writes entity instances, which is why
        ///     this lives here and not on <see cref="IReflectionService"/>.
        /// </summary>
        void SetPropertyOrFieldValue(object instance, MemberInfo propertyOrField, object value);

        /// <summary>
        ///     Whether <paramref name="propertyOrField"/> can be assigned — a property with a setter or a
        ///     field that is not <c>readonly</c>.
        /// </summary>
        bool IsWriteableMember(MemberInfo propertyOrField);
    }
}
