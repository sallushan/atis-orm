using Atis.Orm.Abstractions;
using Atis.Orm.DataManipulation;

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
            IAsyncQueryProvider queryProvider)
            : base(reflectionService, model, crudMetadataFactory, queryProvider)
        {
        }

        /// <inheritdoc />
        protected override bool SupportsOutput => true;
    }
}
