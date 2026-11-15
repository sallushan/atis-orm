using System.Reflection;

namespace Atis.Orm
{
    /// <summary>
    ///     <para>
    ///         Says what <see cref="DataContext.SaveEntity{T}(T)"/> should do with an entity. The
    ///         <see cref="Record"/> base class implements it; implement it yourself on anything that cannot
    ///         derive from a class — most usefully a C# <c>record</c>, which can only inherit from another
    ///         record.
    ///     </para>
    ///     <para>
    ///         The property is bookkeeping, not a column: the metadata builder leaves it out of every table
    ///         mapping, so no <c>[DbNotMapped]</c> is needed on the implementation.
    ///     </para>
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         On a C# <c>record</c>, remember that value-based <c>Equals</c> and <c>GetHashCode</c> include
    ///         every property, <see cref="RecordState"/> too. Changing the state of a record that sits in a
    ///         <see cref="System.Collections.Generic.HashSet{T}"/> or is a dictionary key changes its hash.
    ///         Set the state before adding such a record to a collection.
    ///     </para>
    ///     <para>
    ///         The interface only asks for a getter, because the context reads the state and never writes it.
    ///         An immutable record can declare <c>{ get; init; }</c> and use <c>with</c> to change it.
    ///     </para>
    /// </remarks>
    public interface IRecord
    {
        /// <summary>
        ///     What <see cref="DataContext.SaveEntity{T}(T)"/> should do with this entity. The consumer owns
        ///     this value entirely — the context reads it and never writes it.
        /// </summary>
        RecordState RecordState { get; }
    }

    internal static class RecordStateMapping
    {
        /// <summary>
        ///     Tests whether <paramref name="property"/> is the <see cref="IRecord.RecordState"/> of its type,
        ///     which is never a column.
        /// </summary>
        public static bool IsRecordState(PropertyInfo property)
            => property.Name == nameof(IRecord.RecordState) &&
               property.PropertyType == typeof(RecordState) &&
               typeof(IRecord).IsAssignableFrom(property.DeclaringType);
    }
}
