using Atis.Expressions;
using Atis.SqlExpressionEngine;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.ExpressionConverters;
using Atis.SqlExpressionEngine.Preprocessors;
using Atis.SqlExpressionEngine.Services;
using Atis.SqlExpressionEngine.SqlExpressions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Atis.Orm.Abstractions;
using Atis.Orm.DataAccess;
using Atis.Orm.Metadata;
using Atis.Orm.Querying;
using Atis.Orm.Translation;
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
        protected virtual void OnModelCreating(ModelBuilder mb) { }


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
        ///         Gets the <see cref="IOrmModel"/> for this context, building it via
        ///         <see cref="OnModelCreating(ModelBuilder)"/> on first access.
        ///     </para>
        ///     <para>
        ///         <see cref="IOrmModel"/> is a singleton within its root <see cref="IServiceProvider"/>,
        ///         and that provider is cached per configuration type + extension set (not per
        ///         <see cref="DataContext"/> subclass). As a result, contexts that share the same
        ///         configuration type and extensions share one model, and only the first context to
        ///         access this property runs its <see cref="OnModelCreating(ModelBuilder)"/>. To isolate
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
                    this._ormModel.EnsureModelInitialized(() =>
                    {
                        var metadataBuilder = this.ServiceProvider.GetRequiredService<IEntityMetadataBuilder>();
                        var crudMetadataFactory = this.ServiceProvider.GetRequiredService<IEntityCrudMetadataFactory>();
                        var mb = new ModelBuilder(metadataBuilder, crudMetadataFactory, this._ormModel);
                        this.OnModelCreating(mb);
                        mb.Build();
                    });
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
            // Touching the model first guarantees OnModelCreating has run, so fluent configuration is
            // never lost to an on-demand build from annotations alone.
            var model = this.Model;
            return model.GetOrAddCrud(entityType, t => this.CrudMetadataFactory.Build(t));
        }

        /// <summary>
        ///
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public virtual IQueryable<T> CreateQuery<T>()
        {
            // Accessing Model first guarantees OnModelCreating has run before the factory
            // auto-builds metadata for T, so fluent configuration is not lost.
            _ = this.Model;
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
            // important to call the CreateQuery<T>() first, so that the model is built and the metadata
            // for T is available to the UpdateEntity<T>() call
            this.CreateQuery<T>();
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
            // important to call the CreateQuery<T>() first, so that the model is built and the metadata
            // for T is available to the InsertEntity<T>() call
            this.CreateQuery<T>();
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
            // important to call the CreateQuery<T>() first, so that the model is built and the metadata
            // for T is available to the DeleteEntity<T>() call
            this.CreateQuery<T>();
            return this.QueryProvider.DeleteEntity<T>();
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
