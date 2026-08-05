using Atis.Orm.Abstractions;
using Atis.Orm.Annotations;
using Atis.Orm.Metadata;
using Atis.Orm.Querying;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    ///         what <see cref="SupportsOutput"/> and the two <c>*WithoutOutput</c> methods are for: a
    ///         provider whose database has no <c>OUTPUT</c> clause overrides them to read the values back
    ///         its own way.
    ///     </para>
    /// </summary>
    public class EntityPersister : IEntityPersister
    {
        // TODO: values are emitted as Expression.Constant, which this engine renders as a SQL literal —
        // only a closure member access becomes a parameter, see VariableMemberExpressionConverter. Every
        // row therefore produces distinct SQL text: a new compiled-query cache entry per call, and string
        // values that are not parameterized. The fix is to box the value behind a member access so it
        // converts to a SqlParameterExpression instead.

        private readonly IOrmReflectionService reflectionService;
        private readonly IOrmModel model;
        private readonly IEntityCrudMetadataFactory crudMetadataFactory;
        private readonly IAsyncQueryProvider queryProvider;

        // The member sets never change for a type, but working them out costs a metadata lookup plus
        // several passes over the columns — too much to repeat on every single save.
        private readonly ConcurrentDictionary<Type, EntityWriteMap> writeMaps = new ConcurrentDictionary<Type, EntityWriteMap>();

        /// <summary>Constructs the persister.</summary>
        /// <param name="model">
        ///     The model. Resolving it is what runs <c>OnModelCreating</c>, so the metadata read here is
        ///     always the configured metadata.
        /// </param>
        public EntityPersister(
            IOrmReflectionService reflectionService,
            IOrmModel model,
            IEntityCrudMetadataFactory crudMetadataFactory,
            IAsyncQueryProvider queryProvider)
        {
            this.reflectionService = reflectionService ?? throw new ArgumentNullException(nameof(reflectionService));
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.crudMetadataFactory = crudMetadataFactory ?? throw new ArgumentNullException(nameof(crudMetadataFactory));
            this.queryProvider = queryProvider ?? throw new ArgumentNullException(nameof(queryProvider));
        }

        /// <summary>The provider statements are submitted to, for a derived persister that needs a second round trip.</summary>
        protected IAsyncQueryProvider QueryProvider => this.queryProvider;

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
                return this.InsertWithoutOutput(
                    entity, InsertEntityMethodCallFactory.CreateAffectedRowsCall<T>(values), generatedMembers);
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
                return this.UpdateWithoutOutput(
                    entity, UpdateEntityMethodCallFactory.CreateAffectedRowsCall<T>(setters, keys), generatedMembers);
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

        /// <summary>
        ///     <para>
        ///         Reads <paramref name="generatedMembers"/> back after an insert on a database with no
        ///         <c>OUTPUT</c> clause. Doing so needs a way to find the row that was just written —
        ///         a store generated key is not known to the caller until it is read — and how to ask for
        ///         it differs per database (<c>SCOPE_IDENTITY</c>, <c>last_insert_rowid</c>, a sequence
        ///         read before the insert, ...). A provider that needs this overrides it, executing
        ///         <paramref name="insertCall"/> through <see cref="QueryProvider"/> and then reading the
        ///         values back its own way.
        ///     </para>
        /// </summary>
        protected virtual int InsertWithoutOutput<T>(T entity, Expression insertCall, IReadOnlyList<MemberInfo> generatedMembers)
        {
            throw new NotSupportedException(
                $"'{this.GetType().Name}' reports that it cannot return the inserted row, and does not implement " +
                $"{nameof(InsertWithoutOutput)}. '{typeof(T).Name}' has database generated columns " +
                $"({string.Join(", ", generatedMembers.Select(x => x.Name))}) whose values cannot be read back.");
        }

        /// <summary>
        ///     The <see cref="InsertWithoutOutput{T}(T, Expression, IReadOnlyList{MemberInfo})"/> of update.
        ///     Simpler than the insert case — the key is already known — but still a second round trip, so
        ///     it is left to the provider.
        /// </summary>
        protected virtual int UpdateWithoutOutput<T>(T entity, Expression updateCall, IReadOnlyList<MemberInfo> generatedMembers)
        {
            throw new NotSupportedException(
                $"'{this.GetType().Name}' reports that it cannot return the updated row, and does not implement " +
                $"{nameof(UpdateWithoutOutput)}. '{typeof(T).Name}' has database generated columns " +
                $"({string.Join(", ", generatedMembers.Select(x => x.Name))}) whose values cannot be read back.");
        }

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
            return this.Assignments<T>(entity, map.InsertColumns.Select(x => (MemberInfo)x.Property));
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

            keys = this.Assignments<T>(entity, this.KeyAndConcurrencyMembers(map, optimisticConcurrency));
            generatedMembers = map.UpdateGeneratedMembers;
            return this.Assignments<T>(entity, map.UpdateColumns.Select(x => (MemberInfo)x.Property));
        }

        private IReadOnlyList<FieldValuePair> BuildDeleteKeys<T>(T entity, bool optimisticConcurrency)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            var map = this.GetWriteMap(typeof(T));
            EnsureHasKey<T>(map, "deleted");
            return this.Assignments<T>(entity, this.KeyAndConcurrencyMembers(map, optimisticConcurrency));
        }

        /// <summary>
        ///     The members that identify the row to act on. The row version columns are simply more key
        ///     members: <c>CreateKeyPredicate</c> ANDs the whole list into one predicate, so narrowing an
        ///     update to a particular version of a row is the same operation as narrowing it to a
        ///     particular row.
        /// </summary>
        private IEnumerable<MemberInfo> KeyAndConcurrencyMembers(EntityWriteMap map, bool optimisticConcurrency)
            => optimisticConcurrency
                ? map.KeyMembers.Concat(map.ConcurrencyMembers)
                : map.KeyMembers;

        /// <summary>Pairs each member with the entity's current value for it.</summary>
        private IReadOnlyList<FieldValuePair> Assignments<T>(T entity, IEnumerable<MemberInfo> members)
            => members.Select(member => new FieldValuePair(
                                    CreateFieldSelector<T>(member),
                                    this.CreateValueSelector(entity, member)))
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
        ///     The <c>() =&gt; value</c> selector holding the entity's current value. It stays a lambda
        ///     because that is what keeps a value visible in the tree for rebinding when a compiled query
        ///     is reused.
        /// </summary>
        private LambdaExpression CreateValueSelector(object entity, MemberInfo member)
        {
            var memberType = this.reflectionService.GetPropertyOrFieldType(member);
            var value = this.reflectionService.GetPropertyOrFieldValue(entity, member);
            return Expression.Lambda(Expression.Constant(value, memberType));
        }

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
            var entityMetadata = this.model.EnsureEntityMapped(entityType);

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

            return new EntityWriteMap(keyMembers, insertColumns, updateColumns, insertGenerated, updateGenerated, concurrencyMembers);
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
                IReadOnlyList<MemberInfo> concurrencyMembers)
            {
                this.KeyMembers = keyMembers;
                this.InsertColumns = insertColumns;
                this.UpdateColumns = updateColumns;
                this.InsertGeneratedMembers = insertGeneratedMembers;
                this.UpdateGeneratedMembers = updateGeneratedMembers;
                this.ConcurrencyMembers = concurrencyMembers;
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
        }
    }
}
