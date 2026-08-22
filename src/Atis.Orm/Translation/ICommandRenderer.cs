using System;
using System.Collections.Generic;
using System.Data.Common;

using Atis.Orm.Abstractions;

namespace Atis.Orm.Translation
{
    /// <summary>
    ///     <para>
    ///         The materialized command produced from a fragment list: the command text and the
    ///         <see cref="DbParameter"/>s in the order they appear in it.
    ///     </para>
    /// </summary>
    public sealed class RenderedCommand
    {
        internal RenderedCommand(string sql, IReadOnlyList<DbParameter> dbParameters)
        {
            this.Sql = sql ?? throw new ArgumentNullException(nameof(sql));
            this.DbParameters = dbParameters ?? throw new ArgumentNullException(nameof(dbParameters));
        }

        /// <summary>The complete SQL statement.</summary>
        public string Sql { get; }

        /// <summary>The placeholders in the order they appear in <see cref="Sql"/>.</summary>
        public IReadOnlyList<DbParameter> DbParameters { get; }
    }

    /// <summary>
    ///     <para>
    ///         Walks a translated <see cref="ICommandFragment"/> list together with the current parameter
    ///         values and produces the command text plus the matching <see cref="DbParameter"/>s.
    ///     </para>
    /// </summary>
    public interface ICommandRenderer
    {
        /// <summary>
        ///     Renders <paramref name="fragments"/>, using <paramref name="resolveValue"/> to obtain each
        ///     parameter's current value (initial value for display / cache-miss, or a rebound value).
        /// </summary>
        RenderedCommand Render(IReadOnlyList<ICommandFragment> fragments, Func<IQueryParameter, object> resolveValue);
    }
}
