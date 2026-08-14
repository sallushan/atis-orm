using Atis.Orm.Abstractions;
using Atis.Orm.Annotations;
using Atis.Orm.DataAccess;
using Atis.Orm.Metadata;
using Atis.Orm.Querying;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Atis.Orm.DataManipulation
{
    /// <summary>
    ///     <para>
    ///         The default <see cref="IEntityPersister"/>. It reads an entity's mapping metadata, builds the
    ///         statement from it, executes it, and assigns any database generated value back onto the
    ///         entity.
    ///     </para>
    ///     <para>
    ///         It submits the same <c>QueryExtensions.Insert</c> / <c>Update</c> / <c>Delete</c> calls the
    ///         fluent stages submit, but builds them directly rather than driving the stages: a stage is
    ///         shaped for a caller naming one column at a time in source, and reaching it from a metadata
    ///         loop would mean closing a generic method over each column's type by reflection to say
    ///         something the stage would only take apart again.
    ///     </para>
    ///     <para>
    ///         Reading values back is done by mutating the entity. That does not sit well with an
    ///         immutable entity, and a mapping that makes a generated column read-only is rejected rather
    ///         than quietly skipped. The alternatives — returning a new instance, or a separate result
    ///         object — all change the shape of the API that the previous version of this ORM has in
    ///         production, and would make upgrading from it a migration rather than a reference swap.
    ///     </para>
    ///     <para>
    ///         Not every database can return the written row from the statement that wrote it. That is
    ///         what <see cref="SupportsOutput"/> and the two <c>*WithoutOutput</c> methods are for. Those
    ///         two carry the whole read-back algorithm, so a provider whose database has no <c>OUTPUT</c>
    ///         clause normally overrides nothing but <see cref="GetLastGeneratedKeySql"/> — the one piece
    ///         that genuinely differs per database (<c>SCOPE_IDENTITY()</c>, <c>last_insert_rowid()</c>,
    ///         <c>LAST_INSERT_ID()</c>, ...).
    ///     </para>
    /// </summary>
    public class EntityPersister : IEntityPersister
    {
        private readonly IOrmReflectionService reflectionService;
        private readonly IOrmModel model;
        private readonly IEntityCrudMetadataFactory crudMetadataFactory;
        private readonly IAsyncQueryProvider queryProvider;
        private readonly IDbCommunication dbCommunication;

        // The member sets never change for a type, but working them out costs a metadata lookup plus
        // several passes over the columns — too much to repeat on every single save.
        private readonly ConcurrentDictionary<Type, EntityWriteMap> writeMaps = new ConcurrentDictionary<Type, EntityWriteMap>();

        /// <summary>Constructs the persister.</summary>
        /// <param name="model">
        ///     The model. Resolving it is what runs <c>OnModelCreating</c>, so the metadata read here is
        ///     always the configured metadata.
        /// </param>
        /// <param name="dbCommunication">
        ///     <para>
        ///         Optional, and used only by the read-back path on a database with no <c>OUTPUT</c>
        ///         clause — which needs a transaction spanning several commands, and a scalar command for
        ///         the generated key. Every other path goes through <paramref name="queryProvider"/>
        ///         alone.
        ///     </para>
        ///     <para>
        ///         Optional rather than required so that constructing a persister for the ordinary paths
        ///         does not oblige the caller to supply a database connection it will never use. Reaching
        ///         the read-back path without one is reported there.
        ///     </para>
        /// </param>
        public EntityPersister(
            IOrmReflectionService reflectionService,
            IOrmModel model,
            IEntityCrudMetadataFactory crudMetadataFactory,
            IAsyncQueryProvider queryProvider,
            IDbCommunication dbCommunication = null)
        {
            this.reflectionService = reflectionService ?? throw new ArgumentNullException(nameof(reflectionService));
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.crudMetadataFactory = crudMetadataFactory ?? throw new ArgumentNullException(nameof(crudMetadataFactory));
            this.queryProvider = queryProvider ?? throw new ArgumentNullException(nameof(queryProvider));
            this.dbCommunication = dbCommunication;
        }

        /// <summary>The provider statements are submitted to, for a derived persister that needs a second round trip.</summary>
        protected IAsyncQueryProvider QueryProvider => this.queryProvider;

        /// <summary>
        ///     The connection the read-back path runs its transaction and its generated-key command on.
        ///     May be <c>null</c>; see the constructor.
        /// </summary>
        protected IDbCommunication DbCommunication => this.dbCommunication;

        /// <summary>
        ///     <para>
        ///         Whether this provider's database can return the written row from the statement that
        ///         wrote it, so that database generated values can be read back without a second query.
        ///     </para>
        ///     <para>
        ///         <c>false</c> here because it is not true everywhere; a provider whose database has an
        ///         <c>OUTPUT</c> clause (or equivalent) overrides this to <c>true</c>.
        ///     </para>
        /// </summary>
        protected virtual bool SupportsOutput => false;

        /// <inheritdoc />
        public int Insert<T>(T entity)
        {
            var values = this.BuildInsertValues(entity, out var generatedMembers);
            if (generatedMembers.Count == 0)
                return this.queryProvider.Execute<int>(InsertEntityMethodCallFactory.CreateAffectedRowsCall<T>(values));
            if (!this.SupportsOutput)
            {
                return this.InsertWithoutOutput(
                    entity, InsertEntityMethodCallFactory.CreateAffectedRowsCall<T>(values), generatedMembers);
            }

            var outputCall = InsertEntityMethodCallFactory.CreateOutputCall<T>(values, OutputSelectors<T>(generatedMembers));
            // The row count here is the number of rows the OUTPUT clause returned, which for a
            // single-row insert is the number of rows inserted.
            return this.ApplyGeneratedValues(entity, generatedMembers, this.ExecuteDictionary(outputCall), "insert");
        }

        /// <inheritdoc />
        public async Task<int> InsertAsync<T>(T entity, CancellationToken cancellationToken = default)
        {
            var values = this.BuildInsertValues(entity, out var generatedMembers);
            if (generatedMembers.Count == 0)
            {
                return await this.ExecuteAffectedRowsAsync(
                    InsertEntityMethodCallFactory.CreateAffectedRowsCall<T>(values), cancellationToken).ConfigureAwait(false);
            }
            if (!this.SupportsOutput)
            {
                return await this.InsertWithoutOutputAsync(
                        entity,
                        InsertEntityMethodCallFactory.CreateAffectedRowsCall<T>(values),
                        generatedMembers,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var outputCall = InsertEntityMethodCallFactory.CreateOutputCall<T>(values, OutputSelectors<T>(generatedMembers));
            var insertedRows = await this.ExecuteDictionaryAsync(outputCall, cancellationToken).ConfigureAwait(false);
            return this.ApplyGeneratedValues(entity, generatedMembers, insertedRows, "insert");
        }

        /// <inheritdoc />
        public int Update<T>(T entity, bool optimisticConcurrency)
        {
            var setters = this.BuildUpdateSetters(entity, optimisticConcurrency, out var keys, out var generatedMembers);
            if (generatedMembers.Count == 0)
                return this.queryProvider.Execute<int>(UpdateEntityMethodCallFactory.CreateAffectedRowsCall<T>(setters, keys));
            if (!this.SupportsOutput)
            {
                return this.UpdateWithoutOutput(
                    entity, UpdateEntityMethodCallFactory.CreateAffectedRowsCall<T>(setters, keys), generatedMembers);
            }

            var outputCall = UpdateEntityMethodCallFactory.CreateOutputCall<T>(setters, keys, OutputSelectors<T>(generatedMembers));
            return this.ApplyGeneratedValues(entity, generatedMembers, this.ExecuteDictionary(outputCall), "update");
        }

        /// <inheritdoc />
        public async Task<int> UpdateAsync<T>(T entity, bool optimisticConcurrency, CancellationToken cancellationToken = default)
        {
            var setters = this.BuildUpdateSetters(entity, optimisticConcurrency, out var keys, out var generatedMembers);
            if (generatedMembers.Count == 0)
            {
                return await this.ExecuteAffectedRowsAsync(
                    UpdateEntityMethodCallFactory.CreateAffectedRowsCall<T>(setters, keys), cancellationToken).ConfigureAwait(false);
            }
            if (!this.SupportsOutput)
            {
                return await this.UpdateWithoutOutputAsync(
                        entity,
                        UpdateEntityMethodCallFactory.CreateAffectedRowsCall<T>(setters, keys),
                        generatedMembers,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var outputCall = UpdateEntityMethodCallFactory.CreateOutputCall<T>(setters, keys, OutputSelectors<T>(generatedMembers));
            var updatedRows = await this.ExecuteDictionaryAsync(outputCall, cancellationToken).ConfigureAwait(false);
            return this.ApplyGeneratedValues(entity, generatedMembers, updatedRows, "update");
        }

        /// <inheritdoc />
        public int Delete<T>(T entity, bool optimisticConcurrency)
            => this.queryProvider.Execute<int>(
                DeleteEntityMethodCallFactory.CreateAffectedRowsCall<T>(this.BuildDeleteKeys(entity, optimisticConcurrency)));

        /// <inheritdoc />
        public Task<int> DeleteAsync<T>(T entity, bool optimisticConcurrency, CancellationToken cancellationToken = default)
            => this.ExecuteAffectedRowsAsync(
                DeleteEntityMethodCallFactory.CreateAffectedRowsCall<T>(this.BuildDeleteKeys(entity, optimisticConcurrency)),
                cancellationToken);

        // ---------------------------------------------------------------------------------------
        // Read-back without an OUTPUT clause
        // ---------------------------------------------------------------------------------------

        /// <summary>
        ///     <para>
        ///         Reads <paramref name="generatedMembers"/> back after an insert on a database with no
        ///         <c>OUTPUT</c> clause, in up to three steps: run the insert, ask the database for the
        ///         key it generated, then select the row back by its key.
        ///     </para>
        ///     <para>
        ///         All three run inside one <see cref="IDbCommunication.Transaction(Action)"/>, and that
        ///         is not for atomicity — a single-row insert is atomic on its own. It is because
        ///         <c>SCOPE_IDENTITY()</c> and its equivalents are <em>session</em> scoped: they only
        ///         answer for the connection that ran the insert. Outside a transaction each command
        ///         opens and closes its own connection, so the second one can land on a different pooled
        ///         connection and return someone else's value, or nothing, without any error. Holding a
        ///         transaction open is what pins all three commands to one connection. Nesting is free —
        ///         a <c>Transaction</c> inside a transaction the caller already started is a pass-through.
        ///     </para>
        ///     <para>
        ///         The whole algorithm lives here on purpose. A provider needs only
        ///         <see cref="GetLastGeneratedKeySql"/>, and only when a store generated column is also
        ///         the key.
        ///     </para>
        /// </summary>
        protected virtual int InsertWithoutOutput<T>(T entity, Expression insertCall, IReadOnlyList<MemberInfo> generatedMembers)
        {
            var plan = this.PlanReadBack<T>(generatedMembers, "inserted");
            var communication = this.RequireDbCommunication<T>("inserted");

            var rowsAffected = 0;
            communication.Transaction(() =>
            {
                rowsAffected = this.queryProvider.Execute<int>(insertCall);
                if (rowsAffected == 0)
                    return;

                if (plan.GeneratedKey != null)
                {
                    var keyValue = communication.ExecuteScalarCommand<object>(
                        this.RequireLastGeneratedKeySql<T>(plan.GeneratedKey), NoParameters, CommandType.Text);
                    this.AssignGeneratedKey(entity, plan.GeneratedKey, keyValue);
                }

                if (plan.SelectBackMembers.Count > 0)
                {
                    var rows = this.ExecuteDictionary(this.CreateReadBackCall(entity, plan));
                    this.AssignReadBackValues(entity, plan.SelectBackMembers, rows, "insert");
                }
            });
            return rowsAffected;
        }

        /// <summary>The asynchronous <see cref="InsertWithoutOutput{T}(T, Expression, IReadOnlyList{MemberInfo})"/>.</summary>
        protected virtual async Task<int> InsertWithoutOutputAsync<T>(
            T entity, Expression insertCall, IReadOnlyList<MemberInfo> generatedMembers, CancellationToken cancellationToken)
        {
            var plan = this.PlanReadBack<T>(generatedMembers, "inserted");
            var communication = this.RequireDbCommunication<T>("inserted");

            var rowsAffected = 0;
            await communication.TransactionAsync(async () =>
            {
                rowsAffected = await this.ExecuteAffectedRowsAsync(insertCall, cancellationToken).ConfigureAwait(false);
                if (rowsAffected == 0)
                    return;

                if (plan.GeneratedKey != null)
                {
                    var keyValue = await communication.ExecuteScalarCommandAsync<object>(
                            this.RequireLastGeneratedKeySql<T>(plan.GeneratedKey), NoParameters, CommandType.Text, cancellationToken)
                        .ConfigureAwait(false);
                    this.AssignGeneratedKey(entity, plan.GeneratedKey, keyValue);
                }

                if (plan.SelectBackMembers.Count > 0)
                {
                    var rows = await this.ExecuteDictionaryAsync(this.CreateReadBackCall(entity, plan), cancellationToken)
                                         .ConfigureAwait(false);
                    this.AssignReadBackValues(entity, plan.SelectBackMembers, rows, "insert");
                }
            }, cancellationToken).ConfigureAwait(false);
            return rowsAffected;
        }

        /// <summary>
        ///     <para>
        ///         The <see cref="InsertWithoutOutput{T}(T, Expression, IReadOnlyList{MemberInfo})"/> of
        ///         update, and simpler: the key is already known, so there is never a generated key to
        ///         fetch and the read-back is a single select.
        ///     </para>
        ///     <para>
        ///         Two details it does not share with the insert. The update's own affected-row count is
        ///         what this returns, and a count of zero means the statement matched nothing — an
        ///         optimistic concurrency failure — so the read-back is skipped rather than run against a
        ///         row that was never touched, which keeps "concurrency lost" distinguishable from "the
        ///         row is missing". And the read-back keys on the <em>primary key alone</em>, never on the
        ///         row version, because the update has just changed it.
        ///     </para>
        /// </summary>
        protected virtual int UpdateWithoutOutput<T>(T entity, Expression updateCall, IReadOnlyList<MemberInfo> generatedMembers)
        {
            var plan = this.PlanReadBack<T>(generatedMembers, "updated", isUpdate: true);
            var communication = this.RequireDbCommunication<T>("updated");

            var rowsAffected = 0;
            communication.Transaction(() =>
            {
                rowsAffected = this.queryProvider.Execute<int>(updateCall);
                if (rowsAffected == 0)
                    return;

                var rows = this.ExecuteDictionary(this.CreateReadBackCall(entity, plan));
                this.AssignReadBackValues(entity, plan.SelectBackMembers, rows, "update");
            });
            return rowsAffected;
        }

        /// <summary>The asynchronous <see cref="UpdateWithoutOutput{T}(T, Expression, IReadOnlyList{MemberInfo})"/>.</summary>
        protected virtual async Task<int> UpdateWithoutOutputAsync<T>(
            T entity, Expression updateCall, IReadOnlyList<MemberInfo> generatedMembers, CancellationToken cancellationToken)
        {
            var plan = this.PlanReadBack<T>(generatedMembers, "updated", isUpdate: true);
            var communication = this.RequireDbCommunication<T>("updated");

            var rowsAffected = 0;
            await communication.TransactionAsync(async () =>
            {
                rowsAffected = await this.ExecuteAffectedRowsAsync(updateCall, cancellationToken).ConfigureAwait(false);
                if (rowsAffected == 0)
                    return;

                var rows = await this.ExecuteDictionaryAsync(this.CreateReadBackCall(entity, plan), cancellationToken)
                                     .ConfigureAwait(false);
                this.AssignReadBackValues(entity, plan.SelectBackMembers, rows, "update");
            }, cancellationToken).ConfigureAwait(false);
            return rowsAffected;
        }

        /// <summary>
        ///     <para>
        ///         The statement that asks this database for the key value it generated for the row just
        ///         inserted — <c>SELECT SCOPE_IDENTITY()</c>, <c>SELECT last_insert_rowid()</c>,
        ///         <c>SELECT LAST_INSERT_ID()</c>, and so on. It must read the value for the current
        ///         session, since that is the only thing tying it to the insert this persister just ran.
        ///     </para>
        ///     <para>
        ///         The single piece of the read-back path that genuinely differs per database, so it is
        ///         the only thing a provider normally has to write. <c>null</c> — the default — means this
        ///         provider has no such statement, which is only a problem for an entity whose key the
        ///         database generates; every other entity reaches the row by a key it already has.
        ///     </para>
        /// </summary>
        /// <param name="entityType">The entity being inserted, for a provider that reads a per-table sequence.</param>
        /// <param name="keyMember">The store generated member that is also part of the primary key.</param>
        protected virtual string GetLastGeneratedKeySql(Type entityType, MemberInfo keyMember) => null;

        // ---------------------------------------------------------------------------------------
        // Statement building
        // ---------------------------------------------------------------------------------------

        private IReadOnlyList<FieldValuePair> BuildInsertValues<T>(T entity, out IReadOnlyList<MemberInfo> generatedMembers)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            var map = this.GetWriteMap(typeof(T));
            if (map.InsertColumns.Count == 0)
                throw new InvalidOperationException($"'{typeof(T).Name}' has no insertable column, so it cannot be inserted.");
            this.ValidateRequired(entity, map.InsertColumns);

            generatedMembers = map.InsertGeneratedMembers;
            return Assignments<T>(entity, map.InsertColumns.Select(x => (MemberInfo)x.Property));
        }

        private IReadOnlyList<FieldValuePair> BuildUpdateSetters<T>(
            T entity,
            bool optimisticConcurrency,
            out IReadOnlyList<FieldValuePair> keys,
            out IReadOnlyList<MemberInfo> generatedMembers)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            var map = this.GetWriteMap(typeof(T));
            EnsureHasKey<T>(map, "updated");
            if (map.UpdateColumns.Count == 0)
                throw new InvalidOperationException($"'{typeof(T).Name}' has no updatable column, so it cannot be updated.");
            this.ValidateRequired(entity, map.UpdateColumns);

            keys = Assignments<T>(entity, KeyAndConcurrencyMembers(map, optimisticConcurrency));
            generatedMembers = map.UpdateGeneratedMembers;
            return Assignments<T>(entity, map.UpdateColumns.Select(x => (MemberInfo)x.Property));
        }

        private IReadOnlyList<FieldValuePair> BuildDeleteKeys<T>(T entity, bool optimisticConcurrency)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            var map = this.GetWriteMap(typeof(T));
            EnsureHasKey<T>(map, "deleted");
            return Assignments<T>(entity, KeyAndConcurrencyMembers(map, optimisticConcurrency));
        }

        /// <summary>
        ///     The members that identify the row to act on. The row version columns are simply more key
        ///     members: <c>CreateKeyPredicate</c> ANDs the whole list into one predicate, so narrowing an
        ///     update to a particular version of a row is the same operation as narrowing it to a
        ///     particular row.
        /// </summary>
        private static IEnumerable<MemberInfo> KeyAndConcurrencyMembers(EntityWriteMap map, bool optimisticConcurrency)
            => optimisticConcurrency
                ? map.KeyMembers.Concat(map.ConcurrencyMembers)
                : map.KeyMembers;

        /// <summary>Pairs each member with the entity's current value for it.</summary>
        private static IReadOnlyList<FieldValuePair> Assignments<T>(T entity, IEnumerable<MemberInfo> members)
            => members.Select(member => new FieldValuePair(
                                    CreateFieldSelector<T>(member),
                                    CreateValueSelector(entity, member)))
                      .ToArray();

        private static IReadOnlyList<LambdaExpression> OutputSelectors<T>(IReadOnlyList<MemberInfo> members)
            => members.Select(CreateFieldSelector<T>).ToArray();

        /// <summary>The <c>e =&gt; e.Member</c> selector naming the column.</summary>
        private static LambdaExpression CreateFieldSelector<T>(MemberInfo member)
        {
            var parameter = Expression.Parameter(typeof(T), "e");
            return Expression.Lambda(Expression.MakeMemberAccess(parameter, member), parameter);
        }

        /// <summary>
        ///     <para>
        ///         The value selector for one column: <c>() =&gt; entity.Member</c>, where <c>entity</c> is
        ///         the instance being written, held in a <see cref="ConstantExpression"/>.
        ///     </para>
        ///     <para>
        ///         Reading the member off the entity, rather than emitting its <em>value</em> as a constant,
        ///         is what lets every saved row share one compiled query. A constant becomes a
        ///         <c>SqlLiteralExpression</c>, and a literal is frozen at translation time — it keeps its
        ///         first value on every cache hit — so its value has to be part of the cache key, which
        ///         means one compiled query per row saved. A member access over a constant is the shape a
        ///         captured local has, so it becomes a parameter instead: the key records the member and
        ///         not the value, and the value is re-read from whichever entity this call was given.
        ///     </para>
        ///     <para>
        ///         Using the entity itself as the parameter's container is also what keeps the parameter
        ///         identities apart — <c>MyEntity.Field1</c> against <c>MyEntity.Field2</c>. A shared value
        ///         holder would give every column of every entity the same identity, and two columns
        ///         claiming one identity is rejected outright when the values are re-extracted.
        ///     </para>
        /// </summary>
        private static LambdaExpression CreateValueSelector<T>(T entity, MemberInfo member)
            => Expression.Lambda(Expression.MakeMemberAccess(Expression.Constant(entity, typeof(T)), member));

        // ---------------------------------------------------------------------------------------
        // Execution
        // ---------------------------------------------------------------------------------------

        private IReadOnlyList<IReadOnlyDictionary<string, object>> ExecuteDictionary(Expression call)
            => this.queryProvider.CreateQuery<Dictionary<string, object>>(call).ToList();

        private Task<int> ExecuteAffectedRowsAsync(Expression call, CancellationToken cancellationToken)
            => this.queryProvider.ExecuteAsync<Task<int>>(call, cancellationToken);

        private async Task<IReadOnlyList<IReadOnlyDictionary<string, object>>> ExecuteDictionaryAsync(
            Expression call, CancellationToken cancellationToken)
        {
            // Asks the provider for the rows directly: the queryable the synchronous path builds exists
            // only to be enumerated on the spot, and its GetEnumerator hands this very expression straight
            // back to the provider.
            var rows = this.queryProvider.ExecuteAsync<IAsyncEnumerable<Dictionary<string, object>>>(call, cancellationToken);
            return await rows.DrainAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        ///     <para>
        ///         Assigns the returned row image onto <paramref name="entity"/> and returns the number of
        ///         rows the statement affected.
        ///     </para>
        ///     <para>
        ///         No row means the statement matched nothing — a concurrency failure or a key that is not
        ///         in the table — which the caller reports, since it knows what it was trying to do. More
        ///         than one row means the key was not unique, which is a mapping error and is reported
        ///         here.
        ///     </para>
        /// </summary>
        private int ApplyGeneratedValues<T>(
            T entity,
            IReadOnlyList<MemberInfo> generatedMembers,
            IReadOnlyList<IReadOnlyDictionary<string, object>> rows,
            string operation)
        {
            if (rows.Count == 0)
                return 0;
            if (rows.Count > 1)
            {
                throw new InvalidOperationException(
                    $"A single-entity {operation} of '{typeof(T).Name}' affected {rows.Count} rows. " +
                    "The columns marked as the primary key do not identify a single row.");
            }

            var row = rows[0];
            foreach (var member in generatedMembers)
            {
                if (!row.TryGetValue(member.Name, out var value))
                {
                    throw new InvalidOperationException(
                        $"The {operation} of '{typeof(T).Name}' did not return a value for the database generated column '{member.Name}'.");
                }
                this.reflectionService.SetPropertyOrFieldValue(entity, member, value);
            }
            return 1;
        }

        // ---------------------------------------------------------------------------------------
        // Read-back support
        // ---------------------------------------------------------------------------------------

        /// <summary>An empty parameter set, for the generated-key statement, which never takes one.</summary>
        private static readonly DbParameter[] NoParameters = new DbParameter[0];

        /// <summary>
        ///     <para>
        ///         Works out, before anything is executed, how this entity's generated values are going to
        ///         be found again: whether the database has to be asked for a key it generated, and which
        ///         members are left for the select to bring back.
        ///     </para>
        ///     <para>
        ///         The interesting case is the one that <em>does not</em> need a generated-key statement.
        ///         If the store generated column is not part of the primary key, then the key was supplied
        ///         by the caller and is already known, so the row can be found directly and the identity
        ///         comes back with the rest of the select. Only a key the database itself chose has to be
        ///         asked for separately — and that is also the only case needing a session-scoped
        ///         statement.
        ///     </para>
        /// </summary>
        private ReadBackPlan PlanReadBack<T>(IReadOnlyList<MemberInfo> generatedMembers, string operationPastTense, bool isUpdate = false)
        {
            var map = this.GetWriteMap(typeof(T));
            if (map.KeyMembers.Count == 0)
            {
                throw new InvalidOperationException(
                    $"'{typeof(T).Name}' has database generated columns " +
                    $"({string.Join(", ", generatedMembers.Select(x => x.Name))}) and no primary key, and this " +
                    "database cannot return the written row from the statement that wrote it. Without a key there " +
                    $"is no way to read the row back after it is {operationPastTense}. Mark its key with " +
                    $"{nameof(PrimaryKeyAttribute)} or configure it in OnModelCreating.");
            }

            // An update never fetches a generated key: it already has one, and an identity value does not
            // change, which is why it is not in the update's generated set to begin with.
            var generatedKey = isUpdate ? null : StoreGeneratedKeyMember<T>(map);

            var selectBack = generatedKey is null
                ? generatedMembers
                : generatedMembers.Where(x => x != generatedKey).ToArray();

            return new ReadBackPlan(generatedKey, map.KeyMembers, selectBack);
        }

        /// <summary>
        ///     The single identity column that is also part of the primary key, or <c>null</c>. More than
        ///     one is rejected rather than guessed at: no database generates two key values for one insert,
        ///     so the mapping says something that cannot be true.
        /// </summary>
        private static MemberInfo StoreGeneratedKeyMember<T>(EntityWriteMap map)
        {
            MemberInfo found = null;
            foreach (var identity in map.IdentityMembers)
            {
                if (!map.KeyMembers.Contains(identity))
                    continue;
                if (found != null)
                {
                    throw new InvalidOperationException(
                        $"'{typeof(T).Name}' marks both '{found.Name}' and '{identity.Name}' as a database generated " +
                        "identity column inside the primary key. Only one key value can be generated per insert.");
                }
                found = identity;
            }
            return found;
        }

        /// <summary>
        ///     <para>
        ///         The keyed select that brings the written row back, keyed on the primary key alone and
        ///         selecting only the columns being read back.
        ///     </para>
        ///     <para>
        ///         Narrowing the select list is why this returns a row image rather than an entity. The
        ///         values wanted are a handful of generated columns, and every other column would be
        ///         fetched only to be thrown away — on a table with a large column, or a row version next
        ///         to a blob, that is the expensive part of the whole write.
        ///     </para>
        /// </summary>
        private Expression CreateReadBackCall<T>(T entity, ReadBackPlan plan)
            => SelectEntityMethodCallFactory.CreateKeyedSelectCall<T>(
                Assignments<T>(entity, plan.KeyMembers),
                OutputSelectors<T>(plan.SelectBackMembers));

        private IDbCommunication RequireDbCommunication<T>(string operationPastTense)
            => this.dbCommunication ?? throw new InvalidOperationException(
                $"'{typeof(T).Name}' has database generated columns and '{this.GetType().Name}' reports that it " +
                $"cannot return the {operationPastTense} row, so the values have to be read back by further " +
                $"commands — but this persister was constructed without an {nameof(IDbCommunication)}, which is " +
                "what holds those commands on one connection.");

        private string RequireLastGeneratedKeySql<T>(MemberInfo keyMember)
        {
            var sql = this.GetLastGeneratedKeySql(typeof(T), keyMember);
            if (string.IsNullOrWhiteSpace(sql))
            {
                throw new NotSupportedException(
                    $"'{typeof(T).Name}.{keyMember.Name}' is a database generated column and part of the primary key, " +
                    $"so the value the database chose has to be asked for before the row can be found again. " +
                    $"'{this.GetType().Name}' reports no OUTPUT clause and does not override " +
                    $"{nameof(GetLastGeneratedKeySql)}, so there is no way to ask.");
            }
            return sql;
        }

        /// <summary>
        ///     Assigns the value the generated-key statement returned. It is converted first because these
        ///     statements are typically wider than the column: SQL Server's <c>SCOPE_IDENTITY()</c> is
        ///     <c>numeric(38,0)</c>, which arrives as a <see cref="decimal"/> whatever the column is.
        /// </summary>
        private void AssignGeneratedKey<T>(T entity, MemberInfo keyMember, object value)
        {
            if (value is null || value is DBNull)
            {
                throw new InvalidOperationException(
                    $"The insert of '{typeof(T).Name}' succeeded, but the database returned no value for the " +
                    $"generated key column '{keyMember.Name}'.");
            }

            var memberType = this.reflectionService.GetPropertyOrFieldType(keyMember);
            this.reflectionService.SetPropertyOrFieldValue(entity, keyMember, ConvertToMemberType(value, memberType));
        }

        /// <summary>
        ///     <para>
        ///         Copies the generated values off the row image the select brought back. The keys are the
        ///         member names, since that is what <c>SelectFields</c> aliases each column by.
        ///     </para>
        ///     <para>
        ///         Unlike the OUTPUT path, no row here is an error rather than a count of zero. The write
        ///         has already reported that it affected a row, so the row exists; failing to find it again
        ///         means the mapping does not describe how to reach it, and returning zero would report
        ///         that as a concurrency failure instead.
        ///     </para>
        /// </summary>
        private void AssignReadBackValues<T>(
            T entity,
            IReadOnlyList<MemberInfo> members,
            IReadOnlyList<IReadOnlyDictionary<string, object>> rows,
            string operation)
        {
            if (rows.Count == 0)
            {
                throw new InvalidOperationException(
                    $"The {operation} of '{typeof(T).Name}' affected a row, but reading it back by its primary key " +
                    "found no row. The columns marked as the primary key do not identify the row that was written.");
            }
            if (rows.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Reading back the {operation}d '{typeof(T).Name}' by its primary key matched {rows.Count} rows. " +
                    "The columns marked as the primary key do not identify a single row.");
            }

            var row = rows[0];
            foreach (var member in members)
            {
                // Only reachable if the select list and this loop disagreed about the member set, since
                // both are built from the same list -- but a silently skipped column would leave the
                // entity holding the value it was written with, which reads as "the database did not
                // change it".
                if (!row.TryGetValue(member.Name, out var value))
                {
                    throw new InvalidOperationException(
                        $"Reading back the {operation}d '{typeof(T).Name}' returned no value for the database " +
                        $"generated column '{member.Name}'.");
                }

                this.reflectionService.SetPropertyOrFieldValue(
                    entity, member, ConvertToMemberType(value, this.reflectionService.GetPropertyOrFieldType(member)));
            }
        }

        /// <summary>
        ///     Widens or narrows a scalar onto the member it is about to be assigned to. A row image comes
        ///     off the reader with whatever type the provider chose for the column, which is not always the
        ///     member's — the same reason the generated key needs converting.
        /// </summary>
        private static object ConvertToMemberType(object value, Type memberType)
        {
            // A null is already the member's own null, and ChangeType would reject it against a value type
            // rather than let the assignment report which member could not hold it.
            if (value is null || value is DBNull)
                return null;
            if (memberType.IsInstanceOfType(value))
                return value;

            // ChangeType cannot target Nullable<>, and the boxed underlying value assigns into one fine.
            var targetType = Nullable.GetUnderlyingType(memberType) ?? memberType;
            if (targetType.IsEnum)
            {
                return value is string enumName
                    ? Enum.Parse(targetType, enumName, ignoreCase: true)
                    : Enum.ToObject(targetType, value);
            }
            return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }

        /// <summary>How one entity's generated values will be found again once the write has run.</summary>
        private sealed class ReadBackPlan
        {
            public ReadBackPlan(MemberInfo generatedKey, IReadOnlyList<MemberInfo> keyMembers, IReadOnlyList<MemberInfo> selectBackMembers)
            {
                this.GeneratedKey = generatedKey;
                this.KeyMembers = keyMembers;
                this.SelectBackMembers = selectBackMembers;
            }

            /// <summary>The key column the database generates, which must be asked for before the row can be found. May be <c>null</c>.</summary>
            public MemberInfo GeneratedKey { get; }

            /// <summary>The primary key columns the read-back select filters on — never the row version.</summary>
            public IReadOnlyList<MemberInfo> KeyMembers { get; }

            /// <summary>
            ///     The generated columns left for the select. Empty when the generated key was the only one,
            ///     which is what lets the commonest case — an identity primary key and nothing else — skip
            ///     the select entirely.
            /// </summary>
            public IReadOnlyList<MemberInfo> SelectBackMembers { get; }
        }

        // ---------------------------------------------------------------------------------------
        // Metadata
        // ---------------------------------------------------------------------------------------

        /// <summary>
        ///     An entity with no primary key has nothing to restrict an Update or Delete to one row, and
        ///     a statement built without that restriction would touch the whole table.
        /// </summary>
        private static void EnsureHasKey<T>(EntityWriteMap map, string operationPastTense)
        {
            if (map.KeyMembers.Count == 0)
            {
                throw new InvalidOperationException(
                    $"'{typeof(T).Name}' has no primary key column, so a single entity cannot be {operationPastTense}. " +
                    $"Mark its key with {nameof(PrimaryKeyAttribute)} or configure it in OnModelCreating.");
            }
        }

        /// <summary>
        ///     Fails the write before it is built when a required value is missing, naming the field the
        ///     way the mapping asked for it to be named.
        /// </summary>
        private void ValidateRequired(object entity, IReadOnlyList<CrudColumn> columns)
        {
            for (var i = 0; i < columns.Count; i++)
            {
                var column = columns[i];
                if (!column.IsRequired)
                    continue;

                var value = this.reflectionService.GetPropertyOrFieldValue(entity, column.Property);
                var isMissing = value is null || (value is string text && string.IsNullOrWhiteSpace(text));
                if (isMissing)
                {
                    throw new InvalidOperationException(
                        $"'{column.RequiredFieldTitle ?? column.ModelPropertyName}' is required.");
                }
            }
        }

        private EntityWriteMap GetWriteMap(Type entityType)
            => this.writeMaps.GetOrAdd(entityType, this.BuildWriteMap);

        private EntityWriteMap BuildWriteMap(Type entityType)
        {
            var crudMetadata = this.model.GetOrAddCrud(entityType, t => this.crudMetadataFactory.Build(t));
            var entityMetadata = this.model.GetRequiredEntity(entityType);

            // EntityCrudMetadataFactory deliberately does not diagnose this — it runs for every entity,
            // including ones that are only ever queried, and collapses the annotations to a single kind.
            // First use for a write is where it said the check belongs.
            foreach (var column in crudMetadata.Columns)
            {
                EnsureSingleColumnKindAnnotation(entityType, column.Property);
            }

            var keyPropertyNames = new HashSet<string>(
                entityMetadata.SqlColumns.Where(x => x.IsPrimaryKey).Select(x => x.ModelPropertyName),
                StringComparer.Ordinal);

            var keyMembers = crudMetadata.Columns
                                         .Where(x => keyPropertyNames.Contains(x.ModelPropertyName))
                                         .Select(x => (MemberInfo)x.Property)
                                         .ToArray();

            var insertColumns = crudMetadata.Columns
                                            .Where(x => x.Kind == ColumnKind.Regular || x.Kind == ColumnKind.InsertOnly)
                                            .ToArray();

            var updateColumns = crudMetadata.Columns
                                            .Where(x => x.Kind == ColumnKind.Regular || x.Kind == ColumnKind.UpdateOnly)
                                            .Where(x => !keyPropertyNames.Contains(x.ModelPropertyName))
                                            .ToArray();

            // An identity value is assigned once and never changes, so an update has no reason to read it
            // back — which is why the two sets are not the same.
            var insertGenerated = crudMetadata.Columns
                                              .Where(x => x.Kind == ColumnKind.Identity ||
                                                          x.Kind == ColumnKind.ReadOnly ||
                                                          x.Kind == ColumnKind.RowVersion)
                                              .Select(x => (MemberInfo)x.Property)
                                              .ToArray();

            var updateGenerated = crudMetadata.Columns
                                              .Where(x => x.Kind == ColumnKind.ReadOnly || x.Kind == ColumnKind.RowVersion)
                                              .Select(x => (MemberInfo)x.Property)
                                              .ToArray();

            var concurrencyMembers = crudMetadata.Columns
                                                 .Where(x => x.Kind == ColumnKind.RowVersion)
                                                 .Select(x => (MemberInfo)x.Property)
                                                 .ToArray();

            // Kept apart from the merged generated set above because the read-back path on a database with
            // no OUTPUT clause has to tell an identity column from a computed one: only an identity value
            // can be asked for with SCOPE_IDENTITY and friends.
            var identityMembers = crudMetadata.Columns
                                              .Where(x => x.Kind == ColumnKind.Identity)
                                              .Select(x => (MemberInfo)x.Property)
                                              .ToArray();

            // Read-back is done by assignment, so a generated column that cannot be assigned would leave
            // the entity holding a stale value with nothing to say so. Reject the mapping instead.
            foreach (var member in insertGenerated)
            {
                if (!this.reflectionService.IsWriteableMember(member))
                {
                    throw new InvalidOperationException(
                        $"'{entityType.Name}.{member.Name}' is a database generated column, so its value is read back " +
                        "and assigned after a write, but it has no setter.");
                }
            }

            return new EntityWriteMap(keyMembers, insertColumns, updateColumns, insertGenerated, updateGenerated, concurrencyMembers, identityMembers);
        }

        /// <summary>
        ///     The column kinds are mutually exclusive, so more than one of their annotations on a
        ///     property is a mapping error rather than something to resolve by precedence.
        /// </summary>
        private static void EnsureSingleColumnKindAnnotation(Type entityType, PropertyInfo property)
        {
            var applied = new List<string>();
            if (property.GetCustomAttribute<DbIdentityColumnAttribute>() != null)
                applied.Add(nameof(DbIdentityColumnAttribute));
            if (property.GetCustomAttribute<DbRowVersionAttribute>() != null)
                applied.Add(nameof(DbRowVersionAttribute));
            if (property.GetCustomAttribute<DbReadOnlyColumnAttribute>() != null)
                applied.Add(nameof(DbReadOnlyColumnAttribute));
            if (property.GetCustomAttribute<DbInsertOnlyAttribute>() != null)
                applied.Add(nameof(DbInsertOnlyAttribute));
            if (property.GetCustomAttribute<DbUpdateOnlyAttribute>() != null)
                applied.Add(nameof(DbUpdateOnlyAttribute));

            if (applied.Count > 1)
            {
                throw new InvalidOperationException(
                    $"'{entityType.Name}.{property.Name}' carries {string.Join(" and ", applied)}. " +
                    "A column takes part in Insert and Update in exactly one way, so these cannot be combined.");
            }
        }

        /// <summary>How each of an entity's columns takes part in a write. Worked out once per type.</summary>
        private sealed class EntityWriteMap
        {
            public EntityWriteMap(
                IReadOnlyList<MemberInfo> keyMembers,
                IReadOnlyList<CrudColumn> insertColumns,
                IReadOnlyList<CrudColumn> updateColumns,
                IReadOnlyList<MemberInfo> insertGeneratedMembers,
                IReadOnlyList<MemberInfo> updateGeneratedMembers,
                IReadOnlyList<MemberInfo> concurrencyMembers,
                IReadOnlyList<MemberInfo> identityMembers)
            {
                this.KeyMembers = keyMembers;
                this.InsertColumns = insertColumns;
                this.UpdateColumns = updateColumns;
                this.InsertGeneratedMembers = insertGeneratedMembers;
                this.UpdateGeneratedMembers = updateGeneratedMembers;
                this.ConcurrencyMembers = concurrencyMembers;
                this.IdentityMembers = identityMembers;
            }

            /// <summary>The primary key columns, which identify the row an Update or Delete acts on.</summary>
            public IReadOnlyList<MemberInfo> KeyMembers { get; }

            /// <summary>The columns an Insert writes.</summary>
            public IReadOnlyList<CrudColumn> InsertColumns { get; }

            /// <summary>The columns an Update writes — never a primary key.</summary>
            public IReadOnlyList<CrudColumn> UpdateColumns { get; }

            /// <summary>The columns read back after an Insert.</summary>
            public IReadOnlyList<MemberInfo> InsertGeneratedMembers { get; }

            /// <summary>The columns read back after an Update.</summary>
            public IReadOnlyList<MemberInfo> UpdateGeneratedMembers { get; }

            /// <summary>The row version columns, which join the WHERE clause under optimistic concurrency.</summary>
            public IReadOnlyList<MemberInfo> ConcurrencyMembers { get; }

            /// <summary>
            ///     The identity columns on their own. A subset of <see cref="InsertGeneratedMembers"/>,
            ///     kept separately because only an identity value can be asked for after the fact.
            /// </summary>
            public IReadOnlyList<MemberInfo> IdentityMembers { get; }
        }
    }
}
