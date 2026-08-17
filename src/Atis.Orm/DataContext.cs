using Atis.Orm.Abstractions;
using Atis.Orm.DataAccess;
using Atis.Orm.DataManipulation;
using Atis.Orm.Metadata;
using Atis.Orm.Translation;
using Atis.SqlExpressionEngine;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.ExpressionExtensions;
using Atis.SqlExpressionEngine.SqlExpressions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
namespace Atis.Orm
{
    /// <summary>
    /// 
    /// </summary>
    public class DataContext : IDisposable
    {
        private readonly DataContextConfiguration _config;
        private IServiceScope _serviceScope;
        private IServiceProvider _serviceProvider;

        protected DataContext() : this(new DataContextConfiguration()) { }

        public DataContext(DataContextConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        ///     <para>
        ///         The scoped <see cref="IServiceProvider"/> backing this context, built on first access.
        ///     </para>
        ///     <para>
        ///         The <em>root</em> provider it is scoped from is cached process-wide by configuration type +
        ///         extension types, so it is shared with every other context that hashes the same way —
        ///         including contexts pointed at a different database. That is why the scope is initialized
        ///         with this context's own configuration before anything is resolved from it: services read
        ///         their instance-level options from <see cref="IDataContextServices"/>, never from an
        ///         extension instance captured at registration time.
        ///         See docs/ServiceProviderCachingAndModelLifetime.md.
        ///     </para>
        /// </summary>
        protected IServiceProvider ServiceProvider
        {
            get
            {
                if (_serviceProvider == null)
                {
                    OnConfiguring(_config);
                    _serviceScope = OrmServiceManager.Instance
                        .GetOrAdd(_config)
                        .GetRequiredService<IServiceScopeFactory>()
                        .CreateScope();
                    _serviceScope.ServiceProvider
                        .GetRequiredService<IDataContextServices>()
                        .Initialize(this, _config);
                    _serviceProvider = _serviceScope.ServiceProvider;
                }
                return _serviceProvider;
            }
        }

        protected virtual void OnConfiguring(DataContextConfiguration config) { }

        /// <summary>
        ///     <para>
        ///         Configures the entity mappings. Called once per model, by <see cref="IOrmModelSource"/>,
        ///         the first time anything resolves an <see cref="IOrmModel"/> from this context's scope.
        ///     </para>
        ///     <para>
        ///         Configure entities on <paramref name="mb"/>. Do not query the context from in here — the
        ///         model it would need is the one being built, and that is reported as an error rather than
        ///         left to recurse.
        ///     </para>
        /// </summary>
        /// <remarks>
        ///     <c>internal</c> as well as <c>protected</c> so the model source can invoke it, the same way
        ///     EF Core's <c>ModelSource</c> invokes <c>DbContext.OnModelCreating</c>.
        /// </remarks>
        protected internal virtual void OnModelCreating(ModelBuilder mb) { }


        private IEntityMetadataBuilder _metadataBuilder;
        /// <summary>
        /// 
        /// </summary>
        protected IEntityMetadataBuilder MetadataBuilder
        {
            get
            {
                if (this._metadataBuilder is null)
                {
                    this._metadataBuilder = this.ServiceProvider.GetRequiredService<IEntityMetadataBuilder>();
                }
                return this._metadataBuilder;
            }
        }

        private IEntityCrudMetadataFactory _crudMetadataFactory;
        /// <summary>
        ///     Builds the persistence side of an entity's mapping — column kinds and required field
        ///     information — for entities that were never configured in
        ///     <see cref="OnModelCreating(ModelBuilder)"/>.
        /// </summary>
        protected IEntityCrudMetadataFactory CrudMetadataFactory
        {
            get
            {
                if (this._crudMetadataFactory is null)
                {
                    this._crudMetadataFactory = this.ServiceProvider.GetRequiredService<IEntityCrudMetadataFactory>();
                }
                return this._crudMetadataFactory;
            }
        }

        private IOrmModel _ormModel;
        /// <summary>
        ///     <para>
        ///         Gets the <see cref="IOrmModel"/> for this context. Resolving it is what builds it:
        ///         <see cref="IOrmModel"/> is a scoped service that comes from
        ///         <see cref="IDataContextServices.Model"/>, which runs
        ///         <see cref="OnModelCreating(ModelBuilder)"/> through <see cref="IOrmModelSource"/> on
        ///         first access. There is no way to obtain a model that skipped that step, so no caller
        ///         has to touch this property before doing anything else.
        ///     </para>
        ///     <para>
        ///         <see cref="IOrmModelSource"/> is a singleton within its root
        ///         <see cref="IServiceProvider"/>, and that provider is cached per configuration type +
        ///         extension set (not per <see cref="DataContext"/> subclass). As a result, contexts that
        ///         share the same configuration type and extensions share one model, and only the first
        ///         one to reach for it runs its <see cref="OnModelCreating(ModelBuilder)"/>. To isolate
        ///         the model per context, use a distinct <see cref="DataContextConfiguration"/> subclass.
        ///         See docs/ServiceProviderCachingAndModelLifetime.md.
        ///     </para>
        /// </summary>
        protected IOrmModel Model
        {
            get
            {
                if (this._ormModel is null)
                {
                    this._ormModel = this.ServiceProvider.GetRequiredService<IOrmModel>();
                }
                return this._ormModel;
            }
        }


        private IAsyncQueryProvider _queryProvider;
        /// <summary>
        ///
        /// </summary>
        protected IAsyncQueryProvider QueryProvider
        {
            get
            {
                if (this._queryProvider is null)
                {
                    this._queryProvider = this.ServiceProvider.GetRequiredService<IAsyncQueryProvider>();
                }
                return this._queryProvider;
            }
        }


        private INavigationInitializer _navigationInitializer;
        /// <summary>
        ///     Gets the <see cref="INavigationInitializer"/> for this context. It populates the lazy
        ///     navigation properties of materialized entities during query execution.
        /// </summary>
        protected INavigationInitializer NavigationInitializer
        {
            get
            {
                if (this._navigationInitializer is null)
                {
                    this._navigationInitializer = this.ServiceProvider.GetRequiredService<INavigationInitializer>();
                }
                return this._navigationInitializer;
            }
        }

        private IQueryableFactory _queryableFactory;
        /// <summary>
        ///     Gets the <see cref="IQueryableFactory"/> for this context. It creates <see cref="IQueryable{T}"/> 
        ///     instances for a given expression.
        /// </summary>
        protected IQueryableFactory QueryableFactory
        {
            get
            {
                if (this._queryableFactory is null)
                {
                    this._queryableFactory = this.ServiceProvider.GetRequiredService<IQueryableFactory>();
                }
                return this._queryableFactory;
            }
        }

        /// <summary>
        ///     <para>
        ///         Returns the persistence side of <paramref name="entityType"/>'s mapping, building it
        ///         from annotations when the entity was never configured in
        ///         <see cref="OnModelCreating(ModelBuilder)"/>.
        ///     </para>
        /// </summary>
        protected EntityCrudMetadata GetCrudMetadata(Type entityType)
        {
            if (entityType is null)
                throw new ArgumentNullException(nameof(entityType));
            return this.Model.GetOrAddCrud(entityType, t => this.CrudMetadataFactory.Build(t));
        }

        /// <summary>
        ///
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public virtual IQueryable<T> CreateQuery<T>()
        {
            return this.QueryableFactory.CreateQueryable<T>();
        }

        /// <summary>
        ///     <para>
        ///         Starts a key-based update of <typeparamref name="T"/>: pick the columns to write with
        ///         <c>Set</c>, the rows with <c>Key</c>, then <c>Execute</c> — or <c>Output</c> and
        ///         <c>ExecuteDictionary</c> to read columns back from the rows that were written.
        ///     </para>
        /// </summary>
        public virtual UpdateSetStage<T> UpdateEntity<T>()
        {
            return this.QueryProvider.UpdateEntity<T>();
        }

        /// <summary>
        ///     <para>
        ///         Starts a single-row insert of <typeparamref name="T"/>: pick the columns to write with
        ///         <c>Value</c>, then <c>Execute</c> — or <c>Output</c> and <c>ExecuteDictionary</c> to
        ///         read columns back from the row that was written.
        ///     </para>
        /// </summary>
        public virtual InsertValueStage<T> InsertEntity<T>()
        {
            return this.QueryProvider.InsertEntity<T>();
        }

        /// <summary>
        ///     <para>
        ///         Starts a key-based delete of <typeparamref name="T"/>: pick the rows with
        ///         <c>Key</c>, then <c>Execute</c>. There is no <c>Output</c> — a deleted row leaves
        ///         no image to read columns back from.
        ///     </para>
        /// </summary>
        public virtual DeleteKeyStage<T> DeleteEntity<T>()
        {
            return this.QueryProvider.DeleteEntity<T>();
        }

        private IEntityPersister _entityPersister;
        /// <summary>
        ///     Gets the <see cref="IEntityPersister"/> for this context, which turns a single entity into
        ///     an Insert, Update or Delete driven by its mapping metadata.
        /// </summary>
        protected IEntityPersister EntityPersister
        {
            get
            {
                if (this._entityPersister is null)
                {
                    this._entityPersister = this.ServiceProvider.GetRequiredService<IEntityPersister>();
                }
                return this._entityPersister;
            }
        }

        /// <summary>
        ///     <para>
        ///         Writes <paramref name="entity"/> according to its <see cref="Record.RecordState"/>:
        ///         <see cref="RecordState.Added"/> inserts it, <see cref="RecordState.Updated"/> updates
        ///         it, <see cref="RecordState.Deleted"/> deletes it, and
        ///         <see cref="RecordState.Unchanged"/> does nothing. Returns the number of rows affected.
        ///     </para>
        ///     <para>
        ///         The state is the consumer's to set and the consumer's to clear — this method does not
        ///         reset it after a successful save. Calling it twice on an entity still marked
        ///         <see cref="RecordState.Added"/> therefore inserts two rows.
        ///     </para>
        ///     <para>
        ///         Update and Delete use optimistic concurrency: an entity with a row version column will
        ///         not overwrite a row that changed since it was read. An entity without one is
        ///         last-writer-wins.
        ///     </para>
        /// </summary>
        /// <exception cref="ConcurrencyViolationException">The write matched no row.</exception>
        public virtual int SaveEntity<T>(T entity)
        {
            var state = GetRecordState(entity);
            switch (state)
            {
                case RecordState.Unchanged:
                    return 0;
                case RecordState.Added:
                    return Verify<T>(this.EntityPersister.Insert(entity), "inserted");
                case RecordState.Updated:
                    return Verify<T>(this.EntityPersister.Update(entity, optimisticConcurrency: true), "updated");
                case RecordState.Deleted:
                    return Verify<T>(this.EntityPersister.Delete(entity, optimisticConcurrency: true), "deleted");
                default:
                    throw new InvalidOperationException($"'{state}' is not a record state {nameof(SaveEntity)} knows how to act on.");
            }
        }

        /// <summary>The asynchronous <see cref="SaveEntity{T}(T)"/>.</summary>
        /// <exception cref="ConcurrencyViolationException">The write matched no row.</exception>
        public virtual async Task<int> SaveEntityAsync<T>(T entity, CancellationToken cancellationToken = default)
        {
            var state = GetRecordState(entity);
            switch (state)
            {
                case RecordState.Unchanged:
                    return 0;
                case RecordState.Added:
                    return Verify<T>(
                        await this.EntityPersister.InsertAsync(entity, cancellationToken).ConfigureAwait(false),
                        "inserted");
                case RecordState.Updated:
                    return Verify<T>(
                        await this.EntityPersister.UpdateAsync(entity, optimisticConcurrency: true, cancellationToken).ConfigureAwait(false),
                        "updated");
                case RecordState.Deleted:
                    return Verify<T>(
                        await this.EntityPersister.DeleteAsync(entity, optimisticConcurrency: true, cancellationToken).ConfigureAwait(false),
                        "deleted");
                default:
                    throw new InvalidOperationException($"'{state}' is not a record state {nameof(SaveEntityAsync)} knows how to act on.");
            }
        }

        /// <summary>
        ///     <para>
        ///         Returns the entity whose primary-key columns match <paramref name="key"/>, or <c>null</c>
        ///         when no matching row exists.
        ///     </para>
        ///     <para>
        ///         Key values are matched to columns <em>by name</em>, never by position — pass an object
        ///         carrying them, usually an anonymous one:
        ///         <c>GetEntity&lt;OrderLine&gt;(new { OrderId = 7, LineNo = 2 })</c>. The order the properties
        ///         are written in is irrelevant. An entity keyed on a single column also accepts the bare
        ///         value: <c>GetEntity&lt;Order&gt;(7)</c>.
        ///     </para>
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">
        ///     <paramref name="key"/> does not supply every primary-key column, or a value's type cannot bind
        ///     to its column.
        /// </exception>
        /// <exception cref="InvalidOperationException"><typeparamref name="T"/> has no primary key.</exception>
        public virtual T GetEntity<T>(object key) where T : class
        {
            var predicateLambda = GetWherePredicateByKey<T>(key, this.Model.GetRequiredEntity(typeof(T)));
            return this.CreateQuery<T>().Where(predicateLambda).FirstOrDefault();
        }

        /// <summary>
        ///     The asynchronous <see cref="GetEntity{T}(object)"/>. Returns the entity whose primary-key
        ///     columns match <paramref name="key"/>, or <c>null</c> when no matching row exists.
        /// </summary>
        /// <typeparam name="T">The entity type to read.</typeparam>
        /// <param name="key">The key, matched to columns by name — see <see cref="GetEntity{T}(object)"/>.</param>
        /// <param name="cancellationToken">Cancels the read.</param>
        /// <returns>The matching entity, or <c>null</c> when no row matched.</returns>
        public virtual async Task<T> GetEntityAsync<T>(object key, CancellationToken cancellationToken = default) where T : class
        {
            var predicateLambda = GetWherePredicateByKey<T>(key, this.Model.GetRequiredEntity(typeof(T)));
            return await this.CreateQuery<T>().Where(predicateLambda)
                             .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        ///     Returns the entity whose primary-key columns match <paramref name="key"/>, throwing
        ///     <see cref="RecordNotFoundException"/> when no matching row exists. The key is matched by name —
        ///     see <see cref="GetEntity{T}(object)"/>.
        /// </summary>
        /// <exception cref="RecordNotFoundException">No row matched the key.</exception>
        public virtual T GetRequiredEntity<T>(object key) where T : class
        {
            var entity = GetEntity<T>(key);
            if (entity is null)
                throw this.RecordNotFound<T>(key);
            return entity;
        }

        /// <summary>
        ///     The asynchronous <see cref="GetRequiredEntity{T}(object)"/>. Returns the entity whose
        ///     primary-key columns match <paramref name="key"/>, throwing
        ///     <see cref="RecordNotFoundException"/> when no matching row exists.
        /// </summary>
        /// <typeparam name="T">The entity type to read.</typeparam>
        /// <param name="key">The key, matched to columns by name — see <see cref="GetEntity{T}(object)"/>.</param>
        /// <param name="cancellationToken">Cancels the read.</param>
        /// <returns>The matching entity, never <c>null</c>.</returns>
        /// <exception cref="RecordNotFoundException">No row matched the key.</exception>
        public virtual async Task<T> GetRequiredEntityAsync<T>(object key, CancellationToken cancellationToken = default) where T : class
        {
            var entity = await GetEntityAsync<T>(key, cancellationToken).ConfigureAwait(false);
            if (entity is null)
                throw this.RecordNotFound<T>(key);
            return entity;
        }

        /// <summary>
        ///     <para>
        ///         The primary-key columns of <typeparamref name="T"/>, in a fixed order.
        ///     </para>
        ///     <para>
        ///         Sorted by property name rather than left in <see cref="EntityMetadata.SqlColumns"/> order,
        ///         because that order comes from <c>Type.GetProperties()</c>, which the CLR does not specify.
        ///         Key values are bound by name, so this order never decides <em>which</em> value goes to which
        ///         column — it only fixes the shape of the generated predicate, which keeps one compiled-query
        ///         cache entry serving every caller regardless of how they wrote the key object.
        ///     </para>
        /// </summary>
        private static TableColumn[] GetPrimaryKeyColumns<T>(EntityMetadata metadata)
        {
            var primaryKeyColumns = metadata.SqlColumns
                                            .Where(x => x.IsPrimaryKey)
                                            .OrderBy(x => x.ModelPropertyName, StringComparer.Ordinal)
                                            .ToArray();
            if (primaryKeyColumns.Length == 0)
                throw new InvalidOperationException($"Entity '{typeof(T).Name}' does not have a primary key defined.");
            return primaryKeyColumns;
        }

        /// <summary>
        ///     <para>
        ///         Reads one value per primary-key column out of <paramref name="key"/>, in the order of
        ///         <paramref name="primaryKeyColumns"/>.
        ///     </para>
        ///     <para>
        ///         <paramref name="key"/> is any object carrying the key columns as public readable
        ///         properties. An anonymous object is the usual form; a whole entity works too, so a row can
        ///         be re-read by handing back what it produced. Properties beyond the key columns are ignored,
        ///         which is what makes that work — and a misspelt key property is still caught, because the
        ///         column it was meant to supply then has nothing supplying it.
        ///     </para>
        ///     <para>
        ///         An entity keyed on a single column also accepts the bare value, since with one column there
        ///         is no order to get wrong. The value is taken as that column's own unless it carries a
        ///         property named after the column, so <c>GetEntity&lt;Employee&gt;(5)</c> and
        ///         <c>GetEntity&lt;Employee&gt;(new { EmployeeId = 5 })</c> mean the same thing.
        ///     </para>
        /// </summary>
        private static object[] ResolveKeyValues<T>(object key, TableColumn[] primaryKeyColumns)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            var keyType = key.GetType();
            if (primaryKeyColumns.Length == 1)
            {
                var carrier = FindReadableProperty(keyType, primaryKeyColumns[0].ModelPropertyName);
                return new[] { carrier == null ? key : carrier.GetValue(key) };
            }

            var values = new object[primaryKeyColumns.Length];
            List<string> missing = null;
            for (var i = 0; i < primaryKeyColumns.Length; i++)
            {
                var property = FindReadableProperty(keyType, primaryKeyColumns[i].ModelPropertyName);
                if (property == null)
                    (missing ?? (missing = new List<string>())).Add(primaryKeyColumns[i].ModelPropertyName);
                else
                    values[i] = property.GetValue(key);
            }

            if (missing != null)
                throw new ArgumentException(
                    $"Entity '{typeof(T).Name}' is keyed on ({FormatColumnList(primaryKeyColumns)}), but the key " +
                    $"supplies no {string.Join(" or ", missing)}. Pass the key by name, e.g. " +
                    $"new {{ {string.Join(", ", primaryKeyColumns.Select(x => x.ModelPropertyName + " = ..."))} }}.",
                    nameof(key));

            return values;
        }

        private static PropertyInfo FindReadableProperty(Type type, string name)
        {
            var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            return property != null && property.CanRead ? property : null;
        }

        private static string FormatColumnList(TableColumn[] columns)
            => string.Join(", ", columns.Select(x => x.ModelPropertyName));

        /// <summary>
        ///     Rejects a key value that cannot bind to its column before the tree reaches the driver, which
        ///     reports the same mistake only as "No mapping exists from object type ..." — and only after a
        ///     round trip.
        /// </summary>
        private static void EnsureKeyValueBinds<T>(string propertyName, Type memberType, object value)
        {
            var underlying = Nullable.GetUnderlyingType(memberType);
            var expected = underlying ?? memberType;

            if (value == null)
            {
                if (underlying == null && memberType.IsValueType)
                    throw new ArgumentException(
                        $"Key property '{typeof(T).Name}.{propertyName}' is {expected.Name}, which has no null value to match on.",
                        "key");
                return;
            }

            if (!expected.IsInstanceOfType(value))
                throw new ArgumentException(
                    $"Key property '{typeof(T).Name}.{propertyName}' is {expected.Name}, but the value supplied " +
                    $"for it is {value.GetType().Name}.",
                    "key");
        }

        /// <summary>
        ///     Builds the <see cref="RecordNotFoundException"/> for a key-based read that matched no row. The
        ///     key is re-read here rather than carried down from the caller, so a read that does find its row
        ///     pays for neither the reflection nor the service resolve that only the message needs.
        /// </summary>
        private RecordNotFoundException RecordNotFound<T>(object key) where T : class
        {
            var primaryKeyColumns = GetPrimaryKeyColumns<T>(this.Model.GetRequiredEntity(typeof(T)));
            var keyValues = ResolveKeyValues<T>(key, primaryKeyColumns);
            var namedKey = primaryKeyColumns
                            .Select((c, i) => new KeyValuePair<string, object>(c.ModelPropertyName, keyValues[i]))
                            .ToArray();

            var entityName = this.ServiceProvider.GetRequiredService<IOrmReflectionService>()
                                 .GetTypeDescription(typeof(T));
            return new RecordNotFoundException(typeof(T), entityName, namedKey);
        }

        private static Expression<Func<T, bool>> GetWherePredicateByKey<T>(object key, EntityMetadata metadata) where T : class
        {
            var primaryKeyColumns = GetPrimaryKeyColumns<T>(metadata);
            var keyValues = ResolveKeyValues<T>(key, primaryKeyColumns);

            var entityParameter = Expression.Parameter(typeof(T), "e");
            Expression predicate = null;
            for (var i = 0; i < primaryKeyColumns.Length; i++)
            {
                var primaryKeyColumn = primaryKeyColumns[i];
                var member = Expression.PropertyOrField(entityParameter, primaryKeyColumn.ModelPropertyName);
                EnsureKeyValueBinds<T>(primaryKeyColumn.ModelPropertyName, member.Type, keyValues[i]);
                var keyParameter = new NamedParameterExpression(
                    $"entity_{primaryKeyColumn.ModelPropertyName}",
                    keyValues[i],
                    member.Type);
                var comparison = Expression.Equal(member, keyParameter);
                predicate = predicate == null ? comparison : Expression.AndAlso(predicate, comparison);
            }

            return Expression.Lambda<Func<T, bool>>(predicate, entityParameter);
        }

        /// <summary>
        ///     The state <see cref="SaveEntity{T}(T)"/> acts on. This ORM does not track changes, so the
        ///     entity has to say what it wants — which is what <see cref="Record"/> is for.
        /// </summary>
        private static RecordState GetRecordState<T>(T entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            if (entity is Record record)
                return record.RecordState;

            throw new InvalidOperationException(
                $"'{typeof(T).Name}' does not derive from '{nameof(Record)}', so {nameof(SaveEntity)} has no way to tell " +
                $"whether it should be inserted, updated or deleted. Derive from '{nameof(Record)}' and set its " +
                $"{nameof(Record.RecordState)}, or use the {nameof(InsertEntity)} / {nameof(UpdateEntity)} / " +
                $"{nameof(DeleteEntity)} fluent APIs, which say so explicitly.");
        }

        private static int Verify<T>(int rowsAffected, string operationPastTense)
        {
            if (rowsAffected != 1)
                throw new ConcurrencyViolationException(typeof(T), operationPastTense, rowsAffected);
            return rowsAffected;
        }

        private IDbCommunication _dbCommunication;
        /// <summary>
        ///     Gets the <see cref="IDbCommunication"/> for this context — connection lifetime, command
        ///     execution and transactions.
        /// </summary>
        protected IDbCommunication DbCommunication
        {
            get
            {
                if (this._dbCommunication is null)
                {
                    this._dbCommunication = this.ServiceProvider.GetRequiredService<IDbCommunication>();
                }
                return this._dbCommunication;
            }
        }

        private IDatabaseAdapter _databaseAdapter;
        /// <summary>
        ///     Gets the <see cref="IDatabaseAdapter"/> for this context, which turns SQL plus parameters
        ///     into materialized results.
        /// </summary>
        protected IDatabaseAdapter DatabaseAdapter
        {
            get
            {
                if (this._databaseAdapter is null)
                {
                    this._databaseAdapter = this.ServiceProvider.GetRequiredService<IDatabaseAdapter>();
                }
                return this._databaseAdapter;
            }
        }

        // NOTE: the four members below are a provisional surface so the DB layer can be exercised end to
        // end. The shape will be revisited once that layer is finalized.

        /// <summary>
        ///     <para>
        ///         Runs <paramref name="work"/> inside a database transaction, committing when it returns
        ///         and rolling back when it throws.
        ///     </para>
        ///     <para>
        ///         Nesting is allowed: an inner call joins the transaction already in progress and does not
        ///         commit or roll back on its own — only the outermost call does. Because of that, do not
        ///         swallow an exception thrown out of a nested call; the work it did cannot be undone
        ///         separately and the outer call would commit it. Use
        ///         <see cref="TransactionWithSavepoint(Action)"/> when you need to catch and carry on.
        ///     </para>
        /// </summary>
        public virtual void Transaction(Action work) => this.DbCommunication.Transaction(work);

        /// <summary>
        ///     Runs <paramref name="work"/> inside a savepoint of the surrounding transaction. If it
        ///     throws, only the work done since the savepoint is undone and the exception is rethrown,
        ///     leaving the surrounding transaction usable — so catching around this call is safe.
        /// </summary>
        /// <exception cref="InvalidOperationException">There is no surrounding transaction.</exception>
        public virtual void TransactionWithSavepoint(Action work)
            => this.DbCommunication.TransactionWithSavepoint(work);

        /// <summary>
        ///     The asynchronous <see cref="Transaction(Action)"/>. Nests with the synchronous version in
        ///     either direction. One transaction per context at a time — do not run two concurrently on the
        ///     same context.
        /// </summary>
        public virtual Task TransactionAsync(Func<Task> work, CancellationToken cancellationToken = default)
            => this.DbCommunication.TransactionAsync(work, cancellationToken);

        /// <summary>The asynchronous <see cref="TransactionWithSavepoint(Action)"/>.</summary>
        /// <exception cref="InvalidOperationException">There is no surrounding transaction.</exception>
        public virtual Task TransactionWithSavepointAsync(Func<Task> work, CancellationToken cancellationToken = default)
            => this.DbCommunication.TransactionWithSavepointAsync(work, cancellationToken);

        /// <summary>The asynchronous <see cref="ExecuteNonQuery(string, IEnumerable{DbParameter})"/>.</summary>
        public virtual Task<int> ExecuteNonQueryAsync(string sql, IEnumerable<DbParameter> dbParameters = null, CancellationToken cancellationToken = default)
            => this.DatabaseAdapter.ExecuteNonQueryAsync(sql, dbParameters, cancellationToken);

        /// <summary>
        ///     The asynchronous <see cref="ExecuteQuery{T}(string, Func{IDataReader, T}, IEnumerable{DbParameter})"/>.
        ///     Enumeration is lazy — the reader stays open until the sequence is fully enumerated or disposed.
        /// </summary>
        public virtual IAsyncEnumerable<T> ExecuteQueryAsync<T>(string sql, Func<IDataReader, T> elementFactory, IEnumerable<DbParameter> dbParameters = null)
        {
            if (elementFactory is null)
                throw new ArgumentNullException(nameof(elementFactory));
            return this.DatabaseAdapter.ExecuteEnumerableAsync<T>(
                sql, dbParameters ?? Array.Empty<DbParameter>(), r => elementFactory(r));
        }

        /// <summary>
        ///     Executes <paramref name="sql"/> and returns the number of rows affected. Runs inside the
        ///     current transaction when there is one.
        /// </summary>
        public virtual int ExecuteNonQuery(string sql, IEnumerable<DbParameter> dbParameters = null)
            => this.DatabaseAdapter.ExecuteNonQuery(sql, dbParameters);

        /// <summary>
        ///     Executes <paramref name="sql"/> and materializes each row with
        ///     <paramref name="elementFactory"/>. Runs inside the current transaction when there is one.
        ///     Enumeration is lazy — the reader stays open until the sequence is fully enumerated or
        ///     disposed.
        /// </summary>
        public virtual IEnumerable<T> ExecuteQuery<T>(string sql, Func<IDataReader, T> elementFactory, IEnumerable<DbParameter> dbParameters = null)
        {
            if (elementFactory is null)
                throw new ArgumentNullException(nameof(elementFactory));
            // DbEnumerable rejects a null parameter list where ExecuteNonQuery tolerates one; normalize
            // here so both entry points behave the same from a caller's point of view.
            return this.DatabaseAdapter.ExecuteEnumerable<T>(
                sql, dbParameters ?? Array.Empty<DbParameter>(), r => elementFactory(r));
        }

        /// <summary>
        ///     Translates <paramref name="query"/> and renders it into a command (SQL + parameters), using
        ///     each parameter's translation-time value. Collection parameters are expanded for that value.
        /// </summary>
        public RenderedCommand Translate<T>(IQueryable<T> query)
        {
            if (query is null)
                throw new ArgumentNullException(nameof(query));
            var queryTranslator = this.ServiceProvider.GetRequiredService<IQueryTranslator>();
            var commandRenderer = this.ServiceProvider.GetRequiredService<ISqlCommandRenderer>();
            var queryTranslationResult = queryTranslator.Translate(query.Expression);
            return commandRenderer.Render(queryTranslationResult.SqlTranslation.Fragments, p => p.InitialValue);
        }

        public string TranslateToSql<T>(IQueryable<T> query)
        {
            return this.Translate(query).Sql;
        }

        /// <inheritdoc />
        public void Dispose() => _serviceScope?.Dispose();
    }
}
