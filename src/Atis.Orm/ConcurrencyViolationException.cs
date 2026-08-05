using System;

namespace Atis.Orm
{
    /// <summary>
    ///     <para>
    ///         Thrown when an entity level write matched no row: the row was changed or removed by someone
    ///         else since it was read, or the key it carries is not in the table.
    ///     </para>
    ///     <para>
    ///         Those two cases are not distinguished. Telling them apart would take a read before every
    ///         write to see whether the row exists, which costs a round trip on every save to produce a
    ///         better message for a case that should be rare — and the read could still race. Callers that
    ///         need the distinction can query for the key after catching this.
    ///     </para>
    /// </summary>
    public class ConcurrencyViolationException : Exception
    {
        /// <summary>Constructs the exception.</summary>
        public ConcurrencyViolationException(string message) : base(message)
        {
        }

        /// <summary>Constructs the exception with an inner exception.</summary>
        public ConcurrencyViolationException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        ///     Constructs the exception for a write on <paramref name="entityType"/> that affected
        ///     <paramref name="rowsAffected"/> rows instead of one.
        /// </summary>
        public ConcurrencyViolationException(Type entityType, string operationPastTense, int rowsAffected)
            : base($"Expected exactly 1 row of '{entityType?.Name}' to be {operationPastTense}, but {rowsAffected} were. " +
                   "The row was changed or removed by someone else, or its key is not in the table.")
        {
            this.EntityType = entityType;
            this.RowsAffected = rowsAffected;
        }

        /// <summary>The entity type whose write matched no row, when known.</summary>
        public Type EntityType { get; }

        /// <summary>How many rows the statement actually affected.</summary>
        public int RowsAffected { get; }
    }
}
