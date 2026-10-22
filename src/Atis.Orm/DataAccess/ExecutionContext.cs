using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;

using Atis.Orm.Abstractions;
using Atis.Orm.Querying;
namespace Atis.Orm.DataAccess
{
    public class ExecutionContext : IExecutionContext
    {
        public ExecutionContext(string sql, IReadOnlyList<DbParameter> dbParameters, bool isNonQuery, Func<IDataReader, object> elementFactory)
        {
            this.Sql = sql ?? throw new ArgumentNullException(nameof(sql));
            this.DbParameters = dbParameters;
            this.ElementFactory = elementFactory;
            this.IsNonQuery = isNonQuery;
        }

        public string Sql { get; }

        public IReadOnlyList<DbParameter> DbParameters { get; }

        public Func<IDataReader, object> ElementFactory { get; }

        public bool IsNonQuery { get; }
    }

    /// <summary>
    ///     <para>
    ///         The execution of a query that has child collections, see <see cref="IChildAwareExecutionContext"/>.
    ///     </para>
    /// </summary>
    public class ChildAwareExecutionContext : ExecutionContext, IChildAwareExecutionContext
    {
        /// <summary>Initializes a new instance of the <see cref="ChildAwareExecutionContext"/> class.</summary>
        public ChildAwareExecutionContext(IExecutionContext parent, IReadOnlyList<IExecutionContext> childQueries, Func<IDataReader, ChildCollections, object> childAwareElementFactory)
            : base(parent?.Sql, parent?.DbParameters, isNonQuery: false, elementFactory: null)
        {
            this.ChildQueries = childQueries ?? throw new ArgumentNullException(nameof(childQueries));
            this.ChildAwareElementFactory = childAwareElementFactory ?? throw new ArgumentNullException(nameof(childAwareElementFactory));
        }

        /// <inheritdoc />
        public IReadOnlyList<IExecutionContext> ChildQueries { get; }

        /// <inheritdoc />
        public Func<IDataReader, ChildCollections, object> ChildAwareElementFactory { get; }
    }
}
