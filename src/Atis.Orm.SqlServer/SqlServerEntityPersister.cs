using Atis.Orm.Abstractions;
using Atis.Orm.DataAccess;
using Atis.Orm.DataManipulation;
using System;
using System.Reflection;

namespace Atis.Orm.SqlServer
{
    /// <summary>
    ///     <para>
    ///         The SQL Server <see cref="EntityPersister"/>. SQL Server can return the written row from
    ///         the statement that wrote it via the <c>OUTPUT</c> clause, so database generated values —
    ///         an <c>IDENTITY</c> key, a computed column, a <c>ROWVERSION</c> — are read back without a
    ///         second round trip, and the <c>*WithoutOutput</c> fallbacks are never reached.
    ///     </para>
    /// </summary>
    public class SqlServerEntityPersister : EntityPersister
    {
        /// <summary>Constructs the persister.</summary>
        public SqlServerEntityPersister(
            IOrmReflectionService reflectionService,
            IOrmModel model,
            IEntityCrudMetadataFactory crudMetadataFactory,
            IAsyncQueryProvider queryProvider,
            IDbCommunication dbCommunication = null)
            : base(reflectionService, model, crudMetadataFactory, queryProvider, dbCommunication)
        {
        }

        /// <inheritdoc />
        protected override bool SupportsOutput => true;

        /// <summary>
        ///     <para>
        ///         Supplied even though <see cref="SupportsOutput"/> makes the read-back path unreachable
        ///         today, because the thing that makes it reachable is not a different database — it is a
        ///         trigger. SQL Server refuses <c>OUTPUT</c> on a table with an enabled trigger (error
        ///         334), so a per-entity opt-out has to fall back to reading the value separately, and this
        ///         is what it would fall back to.
        ///     </para>
        ///     <para>
        ///         <c>SCOPE_IDENTITY()</c> rather than <c>@@IDENTITY</c>: the latter reports the last
        ///         identity generated on the connection by <em>anything</em>, so a trigger that inserts
        ///         into a table of its own hands back that table's key instead.
        ///     </para>
        /// </summary>
        protected override string GetLastGeneratedKeySql(Type entityType, MemberInfo keyMember)
            => "SELECT SCOPE_IDENTITY()";
    }
}
