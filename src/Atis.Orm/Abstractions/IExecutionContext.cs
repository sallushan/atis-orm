using Atis.Orm.Querying;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace Atis.Orm.Abstractions
{
    public interface IExecutionContext
    {
        string Sql { get; }
        IReadOnlyList<DbParameter> DbParameters { get; }
        Func<IDataReader, object> ElementFactory { get; }
        bool IsNonQuery { get; }
    }

    /// <summary>
    ///     <para>
    ///         The execution of a query that has child collections: the child queries to run first, and the factory that
    ///         reads a row of the main query once their rows are in hand. <see cref="IExecutionContext.ElementFactory"/>
    ///         is <c>null</c> on such a context because a row cannot be read without the child rows.
    ///     </para>
    /// </summary>
    public interface IChildAwareExecutionContext : IExecutionContext
    {
        /// <summary>
        ///     The child queries, each with its own <see cref="IExecutionContext.Sql"/> and parameters, and a
        ///     <see cref="IExecutionContext.ElementFactory"/> that reads a <see cref="ChildRow"/>. Position in the list is the
        ///     child index that <see cref="ChildCollections"/> is filled by; a <c>null</c> entry is a child query that is not run.
        /// </summary>
        IReadOnlyList<IExecutionContext> ChildQueries { get; }

        /// <summary>Reads one row of the main query from the reader, taking the collections from <paramref name="childCollections"/>.</summary>
        Func<IDataReader, ChildCollections, object> ChildAwareElementFactory { get; }
    }
}
