using Atis.Orm.Annotations;

namespace Atis.Orm
{
    /// <summary>
    ///     <para>
    ///         The base class for entities that can be written through
    ///         <see cref="DataContext.SaveEntity{T}(T)"/>. It carries the one thing that call needs and
    ///         cannot work out for itself: what the consumer intends to happen to this record.
    ///     </para>
    ///     <para>
    ///         Deriving from this is only required for <c>SaveEntity</c>. Querying, and the fluent
    ///         <c>InsertEntity</c> / <c>UpdateEntity</c> / <c>DeleteEntity</c> APIs, work with any type.
    ///     </para>
    /// </summary>
    public abstract class Record
    {
        /// <summary>
        ///     Constructs a record in the <see cref="RecordState.Unchanged"/> state.
        /// </summary>
        /// <remarks>
        ///     Derived entities must keep a parameterless constructor: writes are built as
        ///     <c>new T { Member = value }</c>, which needs one.
        /// </remarks>
        protected Record()
        {
        }

        /// <summary>
        ///     <para>
        ///         What <see cref="DataContext.SaveEntity{T}(T)"/> should do with this record. The
        ///         consumer owns this value entirely — the context reads it and never writes it, not even
        ///         after a successful save.
        ///     </para>
        /// </summary>
        /// <remarks>
        ///     <see cref="DbNotMappedAttribute"/> is not optional here. Column discovery uses
        ///     <c>Type.GetProperties()</c>, which returns inherited public properties, so without it
        ///     every derived entity would gain a bogus <c>RecordState</c> column in every statement.
        /// </remarks>
        [DbNotMapped]
        public RecordState RecordState { get; set; }
    }
}
