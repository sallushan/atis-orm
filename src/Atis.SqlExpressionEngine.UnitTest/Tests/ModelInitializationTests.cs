using Atis.Orm;
using Atis.Orm.Abstractions;
using Atis.Orm.DataAccess;
using Atis.Orm.Metadata;
using Atis.Orm.SqlServer;
using Microsoft.Extensions.DependencyInjection;

// The test namespace has its own DataContext; this is the ORM's.
using OrmDataContext = Atis.Orm.DataContext;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         Covers the one guarantee everything else depends on: nothing can use a model that has not
    ///         run <c>OnModelCreating</c>.
    ///     </para>
    ///     <para>
    ///         It used to be a convention — every entry point that could reach the model had to touch
    ///         <c>DataContext.Model</c> first, and forgetting to would silently derive a mapping from
    ///         annotations and cache it, losing the fluent configuration for the life of the process.
    ///         Now <see cref="IOrmModel"/> is a scoped service produced by
    ///         <see cref="IDataContextServices.Model"/>, so obtaining one is what runs
    ///         <c>OnModelCreating</c>, and there is no ordering left to get wrong.
    ///     </para>
    ///     <para>
    ///         Each context here uses its own <see cref="DataContextConfiguration"/> subclass. Root
    ///         providers — and therefore models — are cached process-wide by configuration type, so a
    ///         shared type would let whichever test ran first build the model and leave the others
    ///         asserting nothing.
    ///     </para>
    /// </summary>
    [TestClass]
    public class ModelInitializationTests
    {
        private const string ConnectionString =
            "Server=.;Database=ModelInitDb;Integrated Security=true;Encrypt=True;TrustServerCertificate=True";

        private sealed class WriteFirstConfig : DataContextConfiguration { }

        /// <summary>Configures one entity fluently, in a way annotations alone could not produce.</summary>
        private sealed class WriteFirstContext : OrmDataContext
        {
            public WriteFirstContext() : base(new WriteFirstConfig()) { }

            protected override void OnConfiguring(DataContextConfiguration config)
            {
                config.UseSqlServer(ConnectionString);
                config.UseUnitTestCustomization();
            }

            protected override void OnModelCreating(ModelBuilder mb)
            {
                mb.Entity<CrudFluentEmployee>(e =>
                {
                    e.ToTable("CRUD_FLUENT_EMPLOYEE");
                    e.HasKey(x => x.EmployeeId);
                    e.Column(x => x.Name, "EMP_NAME");
                });
            }

            public EntityMetadata Mapping<T>()
            {
                if (!this.Model.CanBeEntity(typeof(T)))
                    return null;
                return this.Model.GetRequiredEntity(typeof(T));
            }

            public IServiceProvider Services => this.ServiceProvider;
        }

        /// <summary>
        ///     The regression that matters: a context whose very first operation is a write. Nothing on
        ///     this path creates a queryable, which is what used to be relied on to force the model to
        ///     build, so an unconfigured model would be derived from annotations and cached — and the
        ///     column would come out as <c>Name</c> rather than <c>EMP_NAME</c>.
        /// </summary>
        [TestMethod]
        public void A_write_is_enough_to_build_the_model()
        {
            using var context = new WriteFirstContext();

            context.UpdateEntity<CrudFluentEmployee>();

            var mapping = context.Mapping<CrudFluentEmployee>();
            Assert.IsNotNull(mapping, "The write entry point must have mapped the entity.");
            Assert.AreEqual("CRUD_FLUENT_EMPLOYEE", mapping.Table.TableName,
                "OnModelCreating must have run before the entity was mapped.");
            Assert.IsTrue(
                mapping.SqlColumns.Any(x => x.ModelPropertyName == nameof(CrudFluentEmployee.Name) &&
                                            x.DatabaseColumnName == "EMP_NAME"),
                "The fluent column name must have survived; deriving from annotations would give 'Name'.");
        }

        /// <summary>Resolving the model is what builds it — no caller has to ask for it first.</summary>
        [TestMethod]
        public void Resolving_the_model_service_runs_OnModelCreating()
        {
            using var context = new WriteFirstContext();

            var model = context.Services.GetRequiredService<IOrmModel>();

            // CrudFluentEmployee carries no [DbTable], so the only way the model can know it is through
            // fluent configuration — which makes this the same assertion as "OnModelCreating ran".
            Assert.IsTrue(model.CanBeEntity(typeof(CrudFluentEmployee)),
                "Resolving IOrmModel must have run OnModelCreating.");

            var mapping = model.GetRequiredEntity(typeof(CrudFluentEmployee));
            Assert.AreEqual("CRUD_FLUENT_EMPLOYEE", mapping.Table.TableName);
        }

        /// <summary>
        ///     The model belongs to a context. A scope nobody initialized has no context to build it from,
        ///     and saying so beats handing back an empty model that would quietly cache the wrong mappings.
        /// </summary>
        [TestMethod]
        public void The_model_cannot_be_resolved_from_an_uninitialized_scope()
        {
            var config = new WriteFirstConfig();
            config.UseSqlServer(ConnectionString);
            config.UseUnitTestCustomization();
            var rootProvider = OrmServiceManager.Instance.GetOrAdd(config);

            using var scope = rootProvider.GetRequiredService<IServiceScopeFactory>().CreateScope();

            var thrown = Assert.ThrowsException<InvalidOperationException>(
                () => scope.ServiceProvider.GetRequiredService<IOrmModel>());

            StringAssert.Contains(thrown.Message, "DataContext");
        }

        private sealed class SelfQueryingConfig : DataContextConfiguration { }

        /// <summary>A context that asks for the model from inside the call that is building it.</summary>
        private sealed class SelfQueryingContext : OrmDataContext
        {
            public SelfQueryingContext() : base(new SelfQueryingConfig()) { }

            protected override void OnConfiguring(DataContextConfiguration config)
            {
                config.UseSqlServer(ConnectionString);
                config.UseUnitTestCustomization();
            }

            protected override void OnModelCreating(ModelBuilder mb)
            {
                // Building the model needs the model — without a guard this recurses until the stack ends.
                this.CreateQuery<CrudFluentEmployee>();
            }
        }

        /// <summary>
        ///     Because the model is now built on the way in rather than by an explicit first call,
        ///     configuration that reaches back for it is a loop. It is reported for what it is instead of
        ///     overflowing the stack — the same thing EF Core's <c>DbContextServices.CreateModel</c> does.
        /// </summary>
        [TestMethod]
        public void OnModelCreating_asking_for_the_model_is_reported_not_recursed()
        {
            using var context = new SelfQueryingContext();

            var thrown = Assert.ThrowsException<InvalidOperationException>(
                () => context.CreateQuery<CrudFluentEmployee>());

            StringAssert.Contains(thrown.Message, "OnModelCreating");
        }

        /// <summary>No <c>[DbTable]</c> and never configured — neither of the two ways to be an entity.</summary>
        private sealed class NotAnEntity
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        /// <summary>
        ///     <para>
        ///         The rule the rest of the mapping rests on: a class is an entity because it is annotated
        ///         or because <c>OnModelCreating</c> configured it, and otherwise it is not one. Since
        ///         mappings are now derived on demand, nothing else stops a plain class from being handed
        ///         to the metadata builder and turned into a table named after itself.
        ///     </para>
        ///     <para>
        ///         Every other test drives the accepting side of that rule, so without this one the check
        ///         could be deleted and the suite would stay green — the mistake would surface as a
        ///         database error about a table that does not exist.
        ///     </para>
        /// </summary>
        [TestMethod]
        public void A_class_that_is_neither_annotated_nor_configured_is_refused()
        {
            using var context = new OrmDbContext();

            // Building the queryable is not the point of failure — nothing about T is inspected until the
            // expression is translated, which is where a root type has to resolve to a mapping.
            var query = context.CreateQuery<NotAnEntity>();

            var thrown = Assert.ThrowsException<InvalidOperationException>(
                () => context.GetService<IQueryTranslator>().Translate(query.Expression));

            StringAssert.Contains(thrown.Message, nameof(NotAnEntity),
                "The message must name the offending type — that is the only thing telling the caller " +
                "which of their classes is wrong.");
            StringAssert.Contains(thrown.Message, nameof(Atis.Orm.Annotations.DbTableAttribute),
                "and must name the annotation, which is one of the two ways out.");
            StringAssert.Contains(thrown.Message, "OnModelCreating",
                "and the other.");
        }
    }
}
