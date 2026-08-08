using Atis.Orm;
using Atis.Orm.Abstractions;
using Atis.Orm.Annotations;
using Atis.Orm.DataAccess;
using Atis.Orm.DataManipulation;
using Atis.Orm.Metadata;
using Atis.Orm.Services;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         Covers <see cref="EntityPersister"/>: the statements it builds from an entity's mapping
    ///         metadata, and what it does with the row image the database returns.
    ///     </para>
    ///     <para>
    ///         Nothing here needs a database. The persister submits a standard QueryExtensions method-call
    ///         expression to the query provider, so a provider that only records what it was handed is
    ///         enough to render the SQL and assert on it.
    ///     </para>
    /// </summary>
    [TestClass]
    public class EntityPersisterTests : TestBase
    {
        #region entities

        /// <summary>
        ///     One property of every <see cref="ColumnKind"/>, so a single entity exercises the whole
        ///     column-participation table.
        /// </summary>
        [DbTable]
        public class Product : Record
        {
            [PrimaryKey]
            [DbIdentityColumn]
            public int Id { get; set; }

            [RequiredFieldValidation("Product Name")]
            public string Name { get; set; }

            public string Category { get; set; }

            [DbInsertOnly]
            public string CreatedBy { get; set; }

            [DbUpdateOnly]
            public string ModifiedBy { get; set; }

            [DbReadOnlyColumn]
            public int Rank { get; set; }

            [DbRowVersion]
            public int VersionNo { get; set; }
        }

        /// <summary>A composite key, which is where losing all but the last key predicate does real damage.</summary>
        [DbTable]
        public class OrderLine : Record
        {
            [PrimaryKey]
            public string OrderId { get; set; }

            [PrimaryKey]
            public int LineNo { get; set; }

            public string Note { get; set; }
        }

        /// <summary>No generated column, so the write never needs to read anything back.</summary>
        [DbTable]
        public class Tag : Record
        {
            [PrimaryKey]
            public string Code { get; set; }

            public string Label { get; set; }
        }

        /// <summary>A generated column with no setter — the value could never be assigned back.</summary>
        [DbTable]
        public class UnsettableGenerated : Record
        {
            [PrimaryKey]
            public int Id { get; set; }

            [DbIdentityColumn]
            public int Serial { get; }
        }

        /// <summary>Two column kinds on one property, which the kinds being exclusive makes meaningless.</summary>
        [DbTable]
        public class Contradictory : Record
        {
            [PrimaryKey]
            public int Id { get; set; }

            [DbInsertOnly]
            [DbUpdateOnly]
            public string Confused { get; set; }
        }

        #endregion

        #region harness

        /// <summary>Records the expression the persister submits, and serves canned OUTPUT rows.</summary>
        private sealed class CapturingQueryProvider : IAsyncQueryProvider
        {
            public Expression CapturedExpression { get; private set; }

            public int AffectedRows { get; set; } = 1;

            public List<Dictionary<string, object>> OutputRows { get; } = new List<Dictionary<string, object>>();

            public IQueryable CreateQuery(Expression expression)
                => throw new NotSupportedException();

            public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
            {
                this.CapturedExpression = expression;
                if (typeof(TElement) == typeof(Dictionary<string, object>))
                    return (IQueryable<TElement>)this.OutputRows.AsQueryable();
                return Enumerable.Empty<TElement>().AsQueryable();
            }

            public object Execute(Expression expression)
            {
                this.CapturedExpression = expression;
                return null;
            }

            public TResult Execute<TResult>(Expression expression)
            {
                this.CapturedExpression = expression;
                if (typeof(TResult) == typeof(int))
                    return (TResult)(object)this.AffectedRows;
                return default;
            }

            // TResult is the Task or the IAsyncEnumerable itself, not something wrapping it.
            public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
            {
                this.CapturedExpression = expression;
                if (typeof(TResult) == typeof(Task<int>))
                    return (TResult)(object)Task.FromResult(this.AffectedRows);
                if (typeof(TResult) == typeof(IAsyncEnumerable<Dictionary<string, object>>))
                    return (TResult)(object)this.YieldOutputRows();
                return default;
            }

            private async IAsyncEnumerable<Dictionary<string, object>> YieldOutputRows()
            {
                foreach (var row in this.OutputRows)
                {
                    await Task.Yield();
                    yield return row;
                }
            }
        }

        /// <summary>Stands in for a provider whose database has an OUTPUT clause.</summary>
        private sealed class OutputCapablePersister : EntityPersister
        {
            public OutputCapablePersister(IOrmReflectionService reflectionService, IOrmModel model, IEntityCrudMetadataFactory crudMetadataFactory, IAsyncQueryProvider queryProvider)
                : base(reflectionService, model, crudMetadataFactory, queryProvider)
            {
            }

            protected override bool SupportsOutput => true;
        }

        private static readonly OrmReflectionService ReflectionService = new OrmReflectionService();

        /// <summary>
        ///     A persister over a fresh model, so one test's metadata cache cannot decide another's
        ///     outcome.
        /// </summary>
        private static EntityPersister CreatePersister(IAsyncQueryProvider provider, bool supportsOutput = false)
        {
            var metadataBuilder = new EntityMetadataBuilder(ReflectionService);
            var model = new OrmModel(metadataBuilder);
            var crudMetadataFactory = new EntityCrudMetadataFactory(metadataBuilder);
            return supportsOutput
                ? new OutputCapablePersister(ReflectionService, model, crudMetadataFactory, provider)
                : new EntityPersister(ReflectionService, model, crudMetadataFactory, provider);
        }

        #endregion

        #region insert

        /// <summary>Every insertable column must reach the statement, not just the last one built.</summary>
        [TestMethod]
        public void Insert_writes_every_insertable_column()
        {
            var provider = new CapturingQueryProvider();
            var tag = new Tag { Code = "T1", Label = "First" };

            CreatePersister(provider).Insert(tag);

            string expectedResult = @"
insert into Tag (Code, Label)
values ('T1', 'First')
";
            Test("Persister Insert All Columns Test", provider.CapturedExpression, expectedResult);
        }

        /// <summary>
        ///     Identity, read-only and row version columns are the database's to fill in, and an
        ///     update-only column has no meaning on a row that is being created.
        /// </summary>
        [TestMethod]
        public void Insert_skips_generated_and_update_only_columns()
        {
            var provider = new CapturingQueryProvider();
            provider.OutputRows.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["Id"] = 7,
                ["Rank"] = 3,
                ["VersionNo"] = 1,
            });
            var product = new Product { Name = "Widget", Category = "Tools", CreatedBy = "sallu", ModifiedBy = "ignored" };

            CreatePersister(provider, supportsOutput: true).Insert(product);

            string expectedResult = @"
insert into Product (Name, Category, CreatedBy)
output inserted.Id as Id, inserted.Rank as Rank, inserted.VersionNo as VersionNo
values ('Widget', 'Tools', 'sallu')
";
            Test("Persister Insert Column Kinds Test", provider.CapturedExpression, expectedResult);
        }

        /// <summary>The point of the OUTPUT clause: the entity ends up holding what the database chose.</summary>
        [TestMethod]
        public void Insert_assigns_generated_values_back_onto_the_entity()
        {
            var provider = new CapturingQueryProvider();
            provider.OutputRows.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["Id"] = 42,
                ["Rank"] = 9,
                ["VersionNo"] = 5,
            });
            var product = new Product { Name = "Widget" };

            var rowsAffected = CreatePersister(provider, supportsOutput: true).Insert(product);

            Assert.AreEqual(1, rowsAffected);
            Assert.AreEqual(42, product.Id, "The identity value must be read back onto the entity.");
            Assert.AreEqual(9, product.Rank);
            Assert.AreEqual(5, product.VersionNo);
        }

        /// <summary>An entity with nothing to read back must not pay for an OUTPUT clause.</summary>
        [TestMethod]
        public void Insert_without_generated_columns_does_not_ask_for_output()
        {
            var provider = new CapturingQueryProvider();

            CreatePersister(provider, supportsOutput: true).Insert(new Tag { Code = "T1", Label = "First" });

            var insertCall = (MethodCallExpression)provider.CapturedExpression;
            Assert.AreEqual(1, insertCall.Arguments.Count - 1,
                "An insert with nothing to read back takes only the destination and the values.");
        }

        /// <summary>
        ///     A database that cannot return the written row leaves the entity's generated values
        ///     unknowable, so it must say so rather than hand back a half-populated entity.
        /// </summary>
        [TestMethod]
        public void Insert_without_output_support_reports_the_columns_it_cannot_read_back()
        {
            var provider = new CapturingQueryProvider();

            var thrown = Assert.ThrowsException<NotSupportedException>(
                () => CreatePersister(provider).Insert(new Product { Name = "Widget" }));

            StringAssert.Contains(thrown.Message, nameof(Product.Id));
        }

        #endregion

        #region update

        /// <summary>
        ///     The primary key identifies the row and is never assigned; the row version identifies the
        ///     <em>version</em> of the row and is the database's to bump, so it belongs in the WHERE
        ///     clause and nowhere else.
        /// </summary>
        [TestMethod]
        public void Update_sets_updatable_columns_and_keys_on_pk_plus_row_version()
        {
            var provider = new CapturingQueryProvider();
            provider.OutputRows.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["Rank"] = 1,
                ["VersionNo"] = 2,
            });
            var product = new Product
            {
                Id = 7,
                Name = "Widget",
                Category = "Tools",
                CreatedBy = "ignored",
                ModifiedBy = "sallu",
                VersionNo = 1,
            };

            CreatePersister(provider, supportsOutput: true).Update(product, optimisticConcurrency: true);

            string expectedResult = @"
update a_1
	set Name = 'Widget',
		Category = 'Tools',
		ModifiedBy = 'sallu'
output inserted.Rank as Rank, inserted.VersionNo as VersionNo
from	Product as a_1
where	((a_1.Id = 7) and (a_1.VersionNo = 1))
";
            Test("Persister Update Column Kinds Test", provider.CapturedExpression, expectedResult);
        }

        /// <summary>Without optimistic concurrency the row version drops out of the predicate entirely.</summary>
        [TestMethod]
        public void Update_without_optimistic_concurrency_keys_on_the_primary_key_alone()
        {
            var provider = new CapturingQueryProvider();
            provider.OutputRows.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["Rank"] = 1,
                ["VersionNo"] = 2,
            });
            var product = new Product { Id = 7, Name = "Widget", Category = "Tools", ModifiedBy = "sallu", VersionNo = 1 };

            CreatePersister(provider, supportsOutput: true).Update(product, optimisticConcurrency: false);

            string expectedResult = @"
update a_1
	set Name = 'Widget',
		Category = 'Tools',
		ModifiedBy = 'sallu'
output inserted.Rank as Rank, inserted.VersionNo as VersionNo
from	Product as a_1
where	(a_1.Id = 7)
";
            Test("Persister Update Without Concurrency Test", provider.CapturedExpression, expectedResult);
        }

        /// <summary>An identity value never changes, so re-reading it after an update is pure cost.</summary>
        [TestMethod]
        public void Update_does_not_read_the_identity_column_back()
        {
            var provider = new CapturingQueryProvider();
            provider.OutputRows.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["Rank"] = 1,
                ["VersionNo"] = 2,
            });

            CreatePersister(provider, supportsOutput: true)
                .Update(new Product { Id = 7, Name = "Widget", VersionNo = 1 }, optimisticConcurrency: true);

            var updateCall = (MethodCallExpression)provider.CapturedExpression;
            var outputLambda = (Expression<Func<Product, object[]>>)((UnaryExpression)updateCall.Arguments[3]).Operand;
            var outputs = (NewArrayExpression)outputLambda.Body;

            Assert.AreEqual(2, outputs.Expressions.Count, "Only the read-only and row version columns are read back.");
        }

        /// <summary>
        ///     No row came back, so the row the entity describes is not there in the version it describes.
        ///     The persister reports zero and leaves the interpretation to the caller.
        /// </summary>
        [TestMethod]
        public void Update_returning_no_row_reports_zero_rows_affected()
        {
            var provider = new CapturingQueryProvider();
            var product = new Product { Id = 7, Name = "Widget", VersionNo = 1 };

            var rowsAffected = CreatePersister(provider, supportsOutput: true)
                .Update(product, optimisticConcurrency: true);

            Assert.AreEqual(0, rowsAffected);
        }

        /// <summary>More than one row means the key does not identify one row, which is a mapping error.</summary>
        [TestMethod]
        public void Update_returning_several_rows_rejects_the_key_as_non_unique()
        {
            var provider = new CapturingQueryProvider();
            provider.OutputRows.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["Rank"] = 1, ["VersionNo"] = 2 });
            provider.OutputRows.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["Rank"] = 1, ["VersionNo"] = 2 });

            var thrown = Assert.ThrowsException<InvalidOperationException>(
                () => CreatePersister(provider, supportsOutput: true)
                        .Update(new Product { Id = 7, Name = "Widget" }, optimisticConcurrency: false));

            StringAssert.Contains(thrown.Message, "primary key");
        }

        #endregion

        #region delete

        /// <summary>
        ///     The one that bites hardest: dropping a key column would widen the delete from one line to
        ///     every line that happens to share its number.
        /// </summary>
        [TestMethod]
        public void Delete_keys_on_every_primary_key_column()
        {
            var provider = new CapturingQueryProvider();
            var line = new OrderLine { OrderId = "SO-1", LineNo = 3, Note = "irrelevant" };

            CreatePersister(provider).Delete(line, optimisticConcurrency: false);

            string expectedResult = @"
delete a_1
from	OrderLine as a_1
where	((a_1.OrderId = 'SO-1') and (a_1.LineNo = 3))
";
            Test("Persister Composite Key Delete Test", provider.CapturedExpression, expectedResult);
        }

        /// <summary>The row version narrows the delete the same way it narrows an update.</summary>
        [TestMethod]
        public void Delete_with_optimistic_concurrency_adds_the_row_version()
        {
            var provider = new CapturingQueryProvider();
            var product = new Product { Id = 7, VersionNo = 4 };

            CreatePersister(provider).Delete(product, optimisticConcurrency: true);

            string expectedResult = @"
delete a_1
from	Product as a_1
where	((a_1.Id = 7) and (a_1.VersionNo = 4))
";
            Test("Persister Delete With Concurrency Test", provider.CapturedExpression, expectedResult);
        }

        #endregion

        #region mapping diagnostics

        /// <summary>The metadata carries a required flag and a title for it; both must actually be used.</summary>
        [TestMethod]
        public void A_missing_required_value_is_reported_under_its_configured_title()
        {
            var provider = new CapturingQueryProvider();

            var thrown = Assert.ThrowsException<InvalidOperationException>(
                () => CreatePersister(provider, supportsOutput: true).Insert(new Product { Name = "   " }));

            StringAssert.Contains(thrown.Message, "Product Name");
            Assert.IsNull(provider.CapturedExpression, "Validation must run before the database is touched.");
        }

        /// <summary>
        ///     A generated value is delivered by assignment, so a generated column with no setter cannot
        ///     work — and silently skipping it would leave the caller holding a stale value.
        /// </summary>
        [TestMethod]
        public void A_generated_column_without_a_setter_is_rejected()
        {
            var provider = new CapturingQueryProvider();

            var thrown = Assert.ThrowsException<InvalidOperationException>(
                () => CreatePersister(provider, supportsOutput: true).Insert(new UnsettableGenerated()));

            StringAssert.Contains(thrown.Message, nameof(UnsettableGenerated.Serial));
        }

        /// <summary>
        ///     EntityCrudMetadataFactory documents that combined column-kind annotations are diagnosed on
        ///     first use for a write. This is that first use.
        /// </summary>
        [TestMethod]
        public void Combined_column_kind_annotations_are_rejected_on_first_write()
        {
            var provider = new CapturingQueryProvider();

            var thrown = Assert.ThrowsException<InvalidOperationException>(
                () => CreatePersister(provider).Insert(new Contradictory { Confused = "x" }));

            StringAssert.Contains(thrown.Message, nameof(DbInsertOnlyAttribute));
            StringAssert.Contains(thrown.Message, nameof(DbUpdateOnlyAttribute));
        }

        #endregion

        #region record state

        /// <summary>
        ///     <see cref="Record.RecordState"/> is bookkeeping the consumer owns, not a column. Column
        ///     discovery walks inherited public properties, so without the annotation every entity
        ///     deriving from <see cref="Record"/> would carry it into every statement.
        /// </summary>
        [TestMethod]
        public void RecordState_is_not_a_mapped_column()
        {
            var metadata = new EntityMetadataBuilder(ReflectionService).Build(typeof(Product));

            Assert.IsFalse(
                metadata.SqlColumns.Any(x => x.ModelPropertyName == nameof(Record.RecordState)),
                $"{nameof(Record.RecordState)} must not be mapped.");
            Assert.IsTrue(metadata.SqlColumns.Any(x => x.ModelPropertyName == nameof(Product.Name)));
        }

        /// <summary>An unchanged record is not a write, so it must not reach the database at all.</summary>
        [TestMethod]
        public void SaveEntity_does_nothing_for_an_unchanged_record()
        {
            // No service provider is ever built: an unchanged record is decided before anything is
            // resolved, which is also what keeps this test free of a database.
            using var context = new Atis.Orm.DataContext(new DataContextConfiguration());

            var rowsAffected = context.SaveEntity(new Tag { Code = "T1", RecordState = RecordState.Unchanged });

            Assert.AreEqual(0, rowsAffected);
        }

        /// <summary>
        ///     Nothing tracks changes here, so an entity that cannot say what it wants leaves SaveEntity
        ///     with nothing to go on — and that has to be said plainly rather than guessed at.
        /// </summary>
        [TestMethod]
        public void SaveEntity_rejects_an_entity_that_does_not_derive_from_Record()
        {
            using var context = new Atis.Orm.DataContext(new DataContextConfiguration());

            var thrown = Assert.ThrowsException<InvalidOperationException>(
                () => context.SaveEntity(new Asset { ItemId = "123" }));

            StringAssert.Contains(thrown.Message, nameof(Record));
        }

        #endregion
    }
}
