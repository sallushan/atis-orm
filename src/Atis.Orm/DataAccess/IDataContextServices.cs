using Atis.Orm.Abstractions;
using System;

namespace Atis.Orm.DataAccess
{
    /// <summary>
    ///     <para>
    ///         Carries the <see cref="DataContextConfiguration"/> of the <see cref="DataContext"/> that owns
    ///         the current service scope. Registered as a scoped service, so there is exactly one of these
    ///         per <see cref="DataContext"/>.
    ///     </para>
    ///     <para>
    ///         Root service providers are cached process-wide by configuration type + extension types, so the
    ///         very same provider is handed to contexts that differ only in instance-level options — a
    ///         connection string, a command timeout, an externally supplied connection. A service
    ///         registration must therefore never close over an extension instance: whichever context happened
    ///         to build the provider first would silently supply its options to every context after it.
    ///     </para>
    ///     <para>
    ///         Instead <see cref="DataContext"/> initializes this service with its own live configuration
    ///         immediately after creating the scope, and registrations read their options from here at
    ///         resolution time. See docs/ServiceProviderCachingAndModelLifetime.md.
    ///     </para>
    /// </summary>
    public interface IDataContextServices
    {
        /// <summary>
        ///     The configuration of the <see cref="DataContext"/> that owns this scope.
        /// </summary>
        /// <exception cref="InvalidOperationException">The scope has not been initialized.</exception>
        DataContextConfiguration Configuration { get; }

        /// <summary>
        ///     The <see cref="DataContext"/> that owns this scope.
        /// </summary>
        /// <exception cref="InvalidOperationException">The scope has not been initialized.</exception>
        DataContext Context { get; }

        /// <summary>
        ///     <para>
        ///         The finalized model for this scope's context, built on first access by running the
        ///         context's <c>OnModelCreating</c> through <see cref="IOrmModelSource"/>.
        ///     </para>
        ///     <para>
        ///         <see cref="Abstractions.IOrmModel"/> is registered as a scoped service that resolves to
        ///         this property, so every consumer of the model reaches it through here and none of them
        ///         has to remember to configure it first.
        ///     </para>
        /// </summary>
        /// <exception cref="InvalidOperationException">The scope has not been initialized.</exception>
        IOrmModel Model { get; }

        /// <summary>
        ///     Whether <see cref="Initialize"/> has already run for this scope.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        ///     Binds this scope to the context that created it. Called once, by <see cref="DataContext"/>,
        ///     before any other service is resolved from the scope.
        /// </summary>
        void Initialize(DataContext context, DataContextConfiguration configuration);
    }

    /// <inheritdoc />
    public class DataContextServices : IDataContextServices
    {
        private readonly IOrmModelSource _modelSource;
        private DataContext _context;
        private DataContextConfiguration _configuration;
        private IOrmModel _model;
        private bool _buildingModel;

        /// <summary>Constructs the scope's services.</summary>
        public DataContextServices(IOrmModelSource modelSource)
        {
            this._modelSource = modelSource ?? throw new ArgumentNullException(nameof(modelSource));
        }

        /// <inheritdoc />
        public IOrmModel Model
        {
            get
            {
                if (this._model is null)
                {
                    // OnModelCreating runs inside this call. A context whose OnModelCreating reaches back
                    // for the model — by creating a queryable, say — would otherwise recurse until the
                    // stack ran out, so it is reported as what it is instead.
                    if (this._buildingModel)
                    {
                        throw new InvalidOperationException(
                            $"'{this.Context.GetType().Name}.OnModelCreating' asked for the model it is in the middle " +
                            "of building. Configure entities on the ModelBuilder it was handed rather than querying " +
                            "the context from inside it.");
                    }

                    this._buildingModel = true;
                    try
                    {
                        this._model = this._modelSource.GetModel(this.Context);
                    }
                    finally
                    {
                        this._buildingModel = false;
                    }
                }
                return this._model;
            }
        }

        /// <inheritdoc />
        public DataContextConfiguration Configuration
        {
            get
            {
                this.ThrowIfNotInitialized();
                return this._configuration;
            }
        }

        /// <inheritdoc />
        public DataContext Context
        {
            get
            {
                this.ThrowIfNotInitialized();
                return this._context;
            }
        }

        /// <inheritdoc />
        public bool IsInitialized => this._configuration != null;

        /// <inheritdoc />
        public void Initialize(DataContext context, DataContextConfiguration configuration)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));
            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));
            if (this.IsInitialized)
                throw new InvalidOperationException(
                    $"This {nameof(IDataContextServices)} has already been initialized. A service scope belongs " +
                    $"to exactly one {nameof(DataContext)} and cannot be re-bound.");

            this._context = context;
            this._configuration = configuration;
        }

        private void ThrowIfNotInitialized()
        {
            if (!this.IsInitialized)
                throw new InvalidOperationException(
                    $"The current service scope has not been initialized with a {nameof(DataContext)}. Services " +
                    $"that read their options from {nameof(IDataContextServices)} can only be resolved from a " +
                    $"scope created by a {nameof(DataContext)}, not from the root service provider.");
        }
    }
}
