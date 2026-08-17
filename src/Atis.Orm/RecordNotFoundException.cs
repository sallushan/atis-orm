using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Atis.Orm
{
    /// <summary>
    ///     <para>
    ///         Thrown when a key-based read matched no row:
    ///         <see cref="DataContext.GetRequiredEntity{T}(object)"/> and
    ///         <see cref="DataContext.GetRequiredEntityAsync{T}(object, System.Threading.CancellationToken)"/>
    ///         raise it where their <c>GetEntity</c> counterparts simply return <c>null</c>.
    ///     </para>
    ///     <para>
    ///         <see cref="EntityType"/> and <see cref="Key"/> carry what the message says in a form callers
    ///         can act on, so nothing has to parse the text to find out what was missing.
    ///     </para>
    /// </summary>
    public class RecordNotFoundException : Exception
    {
        /// <summary>Constructs the exception.</summary>
        public RecordNotFoundException(string message) : base(message)
        {
        }

        /// <summary>Constructs the exception with an inner exception.</summary>
        public RecordNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        ///     Constructs the exception for a read on <paramref name="entityType"/> that matched no row.
        /// </summary>
        /// <param name="entityType">The entity type that was read.</param>
        /// <param name="entityName">
        ///     The entity's display name, as
        ///     <see cref="Abstractions.IOrmReflectionService.GetTypeDescription(Type)"/> reports it.
        /// </param>
        /// <param name="key">
        ///     The primary-key columns that matched no row, paired with the values looked for. Keyed by name
        ///     rather than by position, because that is how <see cref="DataContext.GetEntity{T}(object)"/>
        ///     binds them.
        /// </param>
        public RecordNotFoundException(Type entityType, string entityName, IReadOnlyList<KeyValuePair<string, object>> key)
            : base($"Record was not found for '{entityName}' with key '{FormatKey(key)}'.")
        {
            this.EntityType = entityType;
            this.Key = key == null
                        ? null
                        : new ReadOnlyDictionary<string, object>(key.ToDictionary(x => x.Key, x => x.Value));
        }

        /// <summary>The entity type whose read matched no row, when known.</summary>
        public Type EntityType { get; }

        /// <summary>
        ///     The key that matched no row, as column name to value — for example
        ///     <c>["SkillId"] = 5, ["EmployeeId"] = 12</c>. <c>null</c> when the exception was constructed
        ///     from a message alone.
        /// </summary>
        public IReadOnlyDictionary<string, object> Key { get; }

        private static string FormatKey(IReadOnlyList<KeyValuePair<string, object>> key)
            => key == null
                ? "[]"
                : $"[{string.Join(", ", key.Select(x => $"{x.Key} = {x.Value?.ToString() ?? "null"}"))}]";
    }
}
