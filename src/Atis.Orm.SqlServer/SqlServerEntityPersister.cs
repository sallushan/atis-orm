using Atis.Orm.Abstractions;
using Atis.Orm.DataAccess;
using Atis.Orm.DataManipulation;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Atis.Orm.SqlServer
{
    /// <summary>
    ///     <para>
    ///         The SQL Server <see cref="EntityPersister"/>. SQL Server can return the written row from
    ///         the statement that wrote it via the <c>OUTPUT</c> clause, so database generated values —
    ///         an <c>IDENTITY</c> key, a computed column, a <c>ROWVERSION</c> — are read back without a
    ///         second round trip, and the <c>*WithoutOutput</c> fallbacks are never reached.
    ///     </para>
    ///     <para>
    ///         It also appends to a <c>varbinary(max)</c> / <c>nvarchar(max)</c> / <c>varchar(max)</c>
    ///         column with <c>.WRITE</c>, which is what lets a large column be written in chunks.
    ///     </para>
    /// </summary>
    public class SqlServerEntityPersister : EntityPersister
    {
        private readonly IDbParameterFactory parameterFactory;
        private readonly IDbParameterNameGenerator parameterNameGenerator;
        private readonly ISqlNaming naming;

        /// <summary>Constructs the persister.</summary>
        /// <param name="parameterFactory">
        ///     Creates the chunk statements' parameters, from the same client as every other command, since
        ///     a command rejects a parameter from the other SQL Server client.
        /// </param>
        /// <param name="parameterNameGenerator">Names those parameters, as the query translator names its own.</param>
        /// <param name="naming">Spells the chunk statements' table and column names, as the query translator spells its own.</param>
        public SqlServerEntityPersister(
            IOrmReflectionService reflectionService,
            IOrmModel model,
            IEntityCrudMetadataFactory crudMetadataFactory,
            IAsyncQueryProvider queryProvider,
            IDbParameterFactory parameterFactory,
            IDbParameterNameGenerator parameterNameGenerator,
            ISqlNaming naming,
            IDbCommunication dbCommunication = null)
            : base(reflectionService, model, crudMetadataFactory, queryProvider, dbCommunication)
        {
            this.parameterFactory = parameterFactory ?? throw new ArgumentNullException(nameof(parameterFactory));
            this.parameterNameGenerator = parameterNameGenerator ?? throw new ArgumentNullException(nameof(parameterNameGenerator));
            this.naming = naming ?? throw new ArgumentNullException(nameof(naming));
        }

        /// <inheritdoc />
        protected override bool SupportsOutput => true;

        /// <inheritdoc />
        protected override bool SupportsStreamedColumnWrite => true;

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

        /// <inheritdoc />
        protected override int WriteBinaryChunk(IDbCommunication communication, ColumnChunkTarget target, byte[] chunk)
            => this.WriteChunk(communication, target, chunk);

        /// <inheritdoc />
        protected override Task<int> WriteBinaryChunkAsync(
            IDbCommunication communication, ColumnChunkTarget target, byte[] chunk, CancellationToken cancellationToken)
            => this.WriteChunkAsync(communication, target, chunk, cancellationToken);

        /// <inheritdoc />
        protected override int WriteTextChunk(IDbCommunication communication, ColumnChunkTarget target, string chunk)
            => this.WriteChunk(communication, target, chunk);

        /// <inheritdoc />
        protected override Task<int> WriteTextChunkAsync(
            IDbCommunication communication, ColumnChunkTarget target, string chunk, CancellationToken cancellationToken)
            => this.WriteChunkAsync(communication, target, chunk, cancellationToken);

        // .WRITE is spelled the same for varbinary(max) and nvarchar(max), so both chunk kinds share one
        // statement; the chunk goes in as a parameter value either way.
        private int WriteChunk(IDbCommunication communication, ColumnChunkTarget target, object chunk)
            => communication.ExecuteNonQueryCommand(this.BuildChunkSql(target, chunk, out var parameters), parameters, CommandType.Text);

        private Task<int> WriteChunkAsync(
            IDbCommunication communication, ColumnChunkTarget target, object chunk, CancellationToken cancellationToken)
            => communication.ExecuteNonQueryCommandAsync(
                this.BuildChunkSql(target, chunk, out var parameters), parameters, CommandType.Text, cancellationToken);

        /// <summary>
        ///     <para>
        ///         <c>UPDATE t SET col.WRITE(@p0, NULL, NULL) WHERE key = @p1 ...</c> — a <c>NULL</c> offset
        ///         appends, and SQL Server logs only the appended part instead of rewriting the value.
        ///     </para>
        ///     <para>
        ///         Names are spelled through <see cref="ISqlNaming"/>, as the query translator spells them, so
        ///         the chunk statement reaches the same table and column the insert or update did.
        ///     </para>
        /// </summary>
        private string BuildChunkSql(ColumnChunkTarget target, object chunk, out IReadOnlyList<DbParameter> parameters)
        {
            var parameterList = new List<DbParameter>(1 + target.KeyColumns.Count);
            var sql = new StringBuilder();
            sql.Append("UPDATE ").Append(this.naming.GetQualifiedTableName(target.Table))
               .Append(" SET ").Append(this.naming.GetColumnName(target.ColumnName))
               .Append(".WRITE(").Append(this.AddParameter(parameterList, chunk)).Append(", NULL, NULL)");

            for (var i = 0; i < target.KeyColumns.Count; i++)
            {
                sql.Append(i == 0 ? " WHERE " : " AND ")
                   .Append(this.naming.GetColumnName(target.KeyColumns[i].Key))
                   .Append(" = ")
                   .Append(this.AddParameter(parameterList, target.KeyColumns[i].Value));
            }

            parameters = parameterList;
            return sql.ToString();
        }

        private string AddParameter(List<DbParameter> parameters, object value)
        {
            var parameter = this.parameterFactory.CreateDbParameter(parameters.Count, queryParameter: null, value);
            // The factory may name it itself; it is the name the statement has to use either way.
            if (string.IsNullOrEmpty(parameter.ParameterName))
                parameter.ParameterName = this.parameterNameGenerator.GenerateParameterName(parameters.Count);
            parameters.Add(parameter);
            return parameter.ParameterName;
        }
    }
}
