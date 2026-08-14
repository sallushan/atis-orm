using Atis.Orm;
using Atis.Orm.Abstractions;
using Atis.Orm.Annotations;
using Atis.Orm.DataAccess;
using Atis.Orm.DataManipulation;
using Atis.Orm.Metadata;
using Atis.Orm.Services;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;

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

        /// <summary>
        ///     An identity primary key and nothing else generated — the commonest shape, and the one that
        ///     needs no read-back select at all once the generated key has been asked for.
        /// </summary>
        [DbTable]
        public class Ticket : Record
        {
            [PrimaryKey]
            [DbIdentityColumn]
            public int Id { get; set; }

            public string Subject { get; set; }
        }

        /// <summary>
        ///     An identity column that is <em>not</em> the key. The key is the caller's, so the row can be
        ///     found without asking the database for anything — the identity comes back with the select.
        /// </summary>
        [DbTable]
        public class Invoice : Record
        {
            [PrimaryKey]
            public string Number { get; set; }

            [DbIdentityColumn]
            public int Seq { get; set; }

            public string Notes { get; set; }
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

        /// <summary>Records the expressions the persister submits, and serves canned OUTPUT rows.</summary>
        private sealed class CapturingQueryProvider : IAsyncQueryProvider
        {
            /// <summary>
            ///     Every expression submitted, in order. The read-back path submits more than one — the
            ///     write, then the select that reads it back — and which is which is the point of most of
            ///     those tests.
            /// </summary>
            public List<Expression> CapturedExpressions { get; } = new List<Expression>();

            public Expression CapturedExpression => this.CapturedExpressions.LastOrDefault();

            public int AffectedRows { get; set; } = 1;

            public List<Dictionary<string, object>> OutputRows { get; } = new List<Dictionary<string, object>>();

            /// <summary>The rows a read-back select finds, as entities rather than as a row image.</summary>
            public List<object> EntityRows { get; } = new List<object>();

            public IQueryable CreateQuery(Expression expression)
                => throw new NotSupportedException();

            public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
            {
                this.CapturedExpressions.Add(expression);
                if (typeof(TElement) == typeof(Dictionary<string, object>))
                    return (IQueryable<TElement>)this.OutputRows.AsQueryable();
                return this.EntityRows.Cast<TElement>().AsQueryable();
            }

            public object Execute(Expression expression)
            {
                this.CapturedExpressions.Add(expression);
                return null;
            }

            public TResult Execute<TResult>(Expression expression)
            {
                this.CapturedExpressions.Add(expression);
                if (typeof(TResult) == typeof(int))
                    return (TResult)(object)this.AffectedRows;
                return default;
            }

            // TResult is the Task or the IAsyncEnumerable itself, not something wrapping it.
            public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
            {
                this.CapturedExpressions.Add(expression);
                if (typeof(TResult) == typeof(Task<int>))
                    return (TResult)(object)Task.FromResult(this.AffectedRows);
                if (typeof(TResult) == typeof(IAsyncEnumerable<Dictionary<string, object>>))
                    return (TResult)(object)this.YieldOutputRows();
                if (typeof(TResult).IsGenericType && typeof(TResult).GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
                    return (TResult)this.GetType()
                                        .GetMethod(nameof(YieldEntityRows), BindingFlags.NonPublic | BindingFlags.Instance)
                                        .MakeGenericMethod(typeof(TResult).GetGenericArguments()[0])
                                        .Invoke(this, null);
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

            private async IAsyncEnumerable<TElement> YieldEntityRows<TElement>()
            {
                foreach (var row in this.EntityRows)
                {
                    await Task.Yield();
                    yield return (TElement)row;
                }
            }
        }

        /// <summary>
        ///     <para>
        ///         Stands in for the connection the read-back path runs on. It serves the generated-key
        ///         scalar and, more importantly, asserts that every command reaching it arrived inside a
        ///         transaction — which is the whole reason the read-back path opens one.
        ///     </para>
        /// </summary>
        private sealed class FakeDbCommunication : IDbCommunication
        {
            public int TransactionsStarted { get; private set; }

            public bool InTransaction { get; private set; }

            /// <summary>What the generated-key statement returns. Decimal on purpose: SCOPE_IDENTITY is numeric(38,0).</summary>
            public object ScalarResult { get; set; } = 7m;

            public List<string> ScalarCommands { get; } = new List<string>();

            public void Transaction(Action work)
            {
                this.TransactionsStarted++;
                this.InTransaction = true;
                try { work(); }
                finally { this.InTransaction = false; }
            }

            public async Task TransactionAsync(Func<Task> work, CancellationToken cancellationToken = default)
            {
                this.TransactionsStarted++;
                this.InTransaction = true;
                try { await work().ConfigureAwait(false); }
                finally { this.InTransaction = false; }
            }

            public T ExecuteScalarCommand<T>(string sql, IEnumerable<DbParameter> dbParameters, CommandType commandType)
            {
                Assert.IsTrue(this.InTransaction,
                    "The generated-key statement is session scoped, so it must run inside the transaction that pins it to the insert's connection.");
                this.ScalarCommands.Add(sql);
                return (T)this.ScalarResult;
            }

            public Task<T> ExecuteScalarCommandAsync<T>(string sql, IEnumerable<DbParameter> dbParameters, CommandType commandType, CancellationToken cancellationToken)
                => Task.FromResult(this.ExecuteScalarCommand<T>(sql, dbParameters, commandType));

            public void OpenConnection() => throw new NotSupportedException();
            public Task OpenConnectionAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
            public void CloseConnection() => throw new NotSupportedException();
            public Task CloseConnectionAsync() => throw new NotSupportedException();
            public IReadOnlyList<IReadOnlyDictionary<string, object>> ExecuteDictionary(string sql, IEnumerable<DbParameter> dbParameters, CommandType commandType) => throw new NotSupportedException();
            public Task<IReadOnlyList<IReadOnlyDictionary<string, object>>> ExecuteDictionaryAsync(string sql, IEnumerable<DbParameter> dbParameters, CommandType commandType, CancellationToken cancellationToken) => throw new NotSupportedException();
            public int ExecuteNonQueryCommand(string sql, IEnumerable<DbParameter> dbParameters, CommandType commandType) => throw new NotSupportedException();
            public Task<int> ExecuteNonQueryCommandAsync(string sql, IEnumerable<DbParameter> dbParameters, CommandType commandType, CancellationToken cancellationToken) => throw new NotSupportedException();
            public void TransactionWithSavepoint(Action work) => throw new NotSupportedException();
            public Task TransactionWithSavepointAsync(Func<Task> work, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public DbReaderExecutionResult ExecuteReader(string sql, IEnumerable<DbParameter> dbParameters, CommandType commandType) => throw new NotSupportedException();
            public Task<DbReaderExecutionResult> ExecuteReaderAsync(string sql, IEnumerable<DbParameter> dbParameters, CommandType commandType, CancellationToken cancellationToken) => throw new NotSupportedException();
        }

        /// <summary>
        ///     Stands in for a provider whose database has no OUTPUT clause but can be asked for the key it
        ///     generated — SQLite, MySQL, SQL Server against a table with a trigger.
        /// </summary>
        private sealed class NoOutputPersister : EntityPersister
        {
            public NoOutputPersister(IOrmReflectionService reflectionService, IOrmModel model, IEntityCrudMetadataFactory crudMetadataFactory, IAsyncQueryProvider queryProvider, IDbCommunication dbCommunication)
                : base(reflectionService, model, crudMetadataFactory, queryProvider, dbCommunication)
            {
            }

            protected override string GetLastGeneratedKeySql(Type entityType, MemberInfo keyMember)
                => "select last_insert_rowid()";
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

        /// <summary>A persister for a database with no OUTPUT clause, over a fresh model.</summary>
        private static EntityPersister CreateNoOutputPersister(IAsyncQueryProvider provider, IDbCommunication communication)
        {
            var metadataBuilder = new EntityMetadataBuilder(ReflectionService);
            var model = new OrmModel(metadataBuilder);
            var crudMetadataFactory = new EntityCrudMetadataFactory(metadataBuilder);
            return new NoOutputPersister(ReflectionService, model, crudMetadataFactory, provider, communication);
        }

        /// <summary>The member names the read-back select's predicate compares against.</summary>
        private static IReadOnlyList<string> ReadBackPredicateMembers(Expression selectCall)
        {
            var where = (MethodCallExpression)selectCall;
            var predicate = (LambdaExpression)((UnaryExpression)where.Arguments[1]).Operand;

            var names = new List<string>();
            CollectComparedMembers(predicate.Body, predicate.Parameters[0], names);
            return names;
        }

        private static void CollectComparedMembers(Expression node, ParameterExpression parameter, List<string> names)
        {
            switch (node)
            {
                case BinaryExpression binary when binary.NodeType == ExpressionType.AndAlso:
                    CollectComparedMembers(binary.Left, parameter, names);
                    CollectComparedMembers(binary.Right, parameter, names);
                    break;
                case BinaryExpression binary when binary.NodeType == ExpressionType.Equal:
                    // Only the side written against the lambda parameter names a column; the other side
                    // is the value read off the entity.
                    if (binary.Left is MemberExpression left && left.Expression == parameter)
                        names.Add(left.Member.Name);
                    if (binary.Right is MemberExpression right && right.Expression == parameter)
                        names.Add(right.Member.Name);
                    break;
            }
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
        ///     Reading values back without an OUTPUT clause takes several commands on one connection, and
        ///     a persister built without a connection has no way to hold them together — so it says so
        ///     rather than issue them and hope they land on the same one.
        /// </summary>
        [TestMethod]
        public void Insert_without_output_support_and_without_a_connection_is_reported()
        {
            var provider = new CapturingQueryProvider();

            var thrown = Assert.ThrowsException<InvalidOperationException>(
                () => CreatePersister(provider).Insert(new Product { Name = "Widget" }));

            StringAssert.Contains(thrown.Message, nameof(IDbCommunication));
            Assert.AreEqual(0, provider.CapturedExpressions.Count, "Nothing may be written before the failure is reported.");
        }

        #endregion

        #region read-back without an OUTPUT clause

        /// <summary>
        ///     <para>
        ///         The commonest shape, and the cheapest: an identity primary key and nothing else
        ///         generated. Once the database has been asked for the key it chose, everything the entity
        ///         was missing is known, so there is nothing left to select and the second round trip never
        ///         happens.
        ///     </para>
        ///     <para>
        ///         The key also arrives as a <see cref="decimal"/> — SCOPE_IDENTITY is <c>numeric(38,0)</c>
        ///         whatever the column is — so assigning it onto an <c>int</c> member has to convert.
        ///     </para>
        /// </summary>
        [TestMethod]
        public void Insert_with_an_identity_key_asks_for_it_and_needs_no_select()
        {
            var provider = new CapturingQueryProvider();
            var communication = new FakeDbCommunication { ScalarResult = 7m };
            var ticket = new Ticket { Subject = "Printer jam" };

            var rowsAffected = CreateNoOutputPersister(provider, communication).Insert(ticket);

            Assert.AreEqual(1, rowsAffected);
            Assert.AreEqual(7, ticket.Id, "The generated key must be read back and converted onto the member's type.");
            CollectionAssert.AreEqual(new[] { "select last_insert_rowid()" }, communication.ScalarCommands.ToArray());
            Assert.AreEqual(1, provider.CapturedExpressions.Count,
                "Nothing is left to read back, so the insert is the only statement submitted.");
        }

        /// <summary>
        ///     Every command of the read-back must run inside one transaction. Not for atomicity — a
        ///     single-row insert is atomic already — but because the generated-key statement is session
        ///     scoped, so it only answers for the connection the insert ran on.
        /// </summary>
        [TestMethod]
        public void Read_back_runs_every_command_inside_one_transaction()
        {
            var provider = new CapturingQueryProvider();
            var communication = new FakeDbCommunication();

            // FakeDbCommunication asserts the transaction is open when the scalar reaches it; this asserts
            // that exactly one was started rather than one per command.
            CreateNoOutputPersister(provider, communication).Insert(new Ticket { Subject = "x" });

            Assert.AreEqual(1, communication.TransactionsStarted);
        }

        /// <summary>
        ///     With more than the key to read back, the generated key is asked for first — it is what makes
        ///     the row findable — and the rest arrive by selecting that row.
        /// </summary>
        [TestMethod]
        public void Insert_selects_the_remaining_generated_columns_back_by_key()
        {
            var provider = new CapturingQueryProvider();
            provider.EntityRows.Add(new Product { Id = 42, Rank = 9, VersionNo = 5 });
            var communication = new FakeDbCommunication { ScalarResult = 42m };
            var product = new Product { Name = "Widget" };

            var rowsAffected = CreateNoOutputPersister(provider, communication).Insert(product);

            Assert.AreEqual(1, rowsAffected);
            Assert.AreEqual(42, product.Id);
            Assert.AreEqual(9, product.Rank);
            Assert.AreEqual(5, product.VersionNo);
            Assert.AreEqual(2, provider.CapturedExpressions.Count, "The insert, then the select that reads the rest back.");
            CollectionAssert.AreEqual(
                new[] { nameof(Product.Id) },
                ReadBackPredicateMembers(provider.CapturedExpressions[1]).ToArray(),
                "The read-back is keyed on the primary key.");
        }

        /// <summary>
        ///     <para>
        ///         An identity column that is not the key needs no generated-key statement at all. The key
        ///         was supplied by the caller, so the row can be found straight away and the identity comes
        ///         back with the select like any other generated column.
        ///     </para>
        ///     <para>
        ///         Worth having its own test because it is the case that removes the session-scoped
        ///         statement — and with it the only part of this path that depends on which connection the
        ///         commands land on.
        ///     </para>
        /// </summary>
        [TestMethod]
        public void Insert_with_an_identity_outside_the_key_never_asks_for_a_generated_key()
        {
            var provider = new CapturingQueryProvider();
            provider.EntityRows.Add(new Invoice { Number = "INV-1", Seq = 31, Notes = "read back" });
            var communication = new FakeDbCommunication();
            var invoice = new Invoice { Number = "INV-1", Notes = "written" };

            CreateNoOutputPersister(provider, communication).Insert(invoice);

            Assert.AreEqual(0, communication.ScalarCommands.Count, "The key was already known, so nothing had to be asked for.");
            Assert.AreEqual(31, invoice.Seq, "The identity arrives with the select.");
            CollectionAssert.AreEqual(
                new[] { nameof(Invoice.Number) },
                ReadBackPredicateMembers(provider.CapturedExpressions[1]).ToArray());
        }

        /// <summary>
        ///     A store generated key that the provider has no way to ask for leaves the row unfindable, so
        ///     the mapping and the provider cannot both be right — and saying which one to change is more
        ///     use than a failure at the select.
        /// </summary>
        [TestMethod]
        public void Insert_without_a_way_to_ask_for_the_generated_key_is_reported()
        {
            var provider = new CapturingQueryProvider();
            var metadataBuilder = new EntityMetadataBuilder(ReflectionService);
            // The base persister: no OUTPUT clause and no GetLastGeneratedKeySql either.
            var persister = new EntityPersister(
                ReflectionService,
                new OrmModel(metadataBuilder),
                new EntityCrudMetadataFactory(metadataBuilder),
                provider,
                new FakeDbCommunication());

            var thrown = Assert.ThrowsException<NotSupportedException>(
                () => persister.Insert(new Ticket { Subject = "x" }));

            StringAssert.Contains(thrown.Message, "GetLastGeneratedKeySql");
            StringAssert.Contains(thrown.Message, nameof(Ticket.Id));
        }

        /// <summary>
        ///     The row version has just been changed by the update, so keying the read-back on it would
        ///     look for the version that no longer exists. The primary key alone identifies the row.
        /// </summary>
        [TestMethod]
        public void Update_reads_back_on_the_primary_key_alone()
        {
            var provider = new CapturingQueryProvider();
            provider.EntityRows.Add(new Product { Id = 7, Rank = 3, VersionNo = 2 });
            var product = new Product { Id = 7, Name = "Widget", ModifiedBy = "sallu", VersionNo = 1 };

            var rowsAffected = CreateNoOutputPersister(provider, new FakeDbCommunication())
                .Update(product, optimisticConcurrency: true);

            Assert.AreEqual(1, rowsAffected);
            Assert.AreEqual(2, product.VersionNo, "The bumped row version must be read back.");
            Assert.AreEqual(3, product.Rank);
            CollectionAssert.AreEqual(
                new[] { nameof(Product.Id) },
                ReadBackPredicateMembers(provider.CapturedExpressions[1]).ToArray(),
                "The row version identifies a version, not a row, and the update has just changed it.");
        }

        /// <summary>
        ///     An update that matched nothing lost an optimistic concurrency check. Reading back afterwards
        ///     would either find the row someone else wrote and report success, or find nothing and report
        ///     a mapping error — both of which hide what actually happened.
        /// </summary>
        [TestMethod]
        public void Update_that_affects_no_row_does_not_read_back()
        {
            var provider = new CapturingQueryProvider { AffectedRows = 0 };
            var product = new Product { Id = 7, Name = "Widget", ModifiedBy = "sallu", VersionNo = 1 };

            var rowsAffected = CreateNoOutputPersister(provider, new FakeDbCommunication())
                .Update(product, optimisticConcurrency: true);

            Assert.AreEqual(0, rowsAffected);
            Assert.AreEqual(1, provider.CapturedExpressions.Count, "Only the update itself was submitted.");
        }

        /// <summary>
        ///     The write reported that it affected a row, so the row is there. Failing to find it again
        ///     means the primary key does not describe how to reach it — a mapping error, not the zero-rows
        ///     answer that would be read as a concurrency failure.
        /// </summary>
        [TestMethod]
        public void Read_back_that_finds_no_row_is_reported_as_a_key_problem()
        {
            var provider = new CapturingQueryProvider();
            var product = new Product { Id = 7, Name = "Widget", ModifiedBy = "sallu" };

            var thrown = Assert.ThrowsException<InvalidOperationException>(
                () => CreateNoOutputPersister(provider, new FakeDbCommunication())
                        .Update(product, optimisticConcurrency: false));

            StringAssert.Contains(thrown.Message, "primary key");
        }

        /// <summary>The asynchronous path takes the same three steps, on the asynchronous transaction.</summary>
        [TestMethod]
        public async Task InsertAsync_reads_the_generated_values_back_the_same_way()
        {
            var provider = new CapturingQueryProvider();
            provider.EntityRows.Add(new Product { Id = 42, Rank = 9, VersionNo = 5 });
            var communication = new FakeDbCommunication { ScalarResult = 42m };
            var product = new Product { Name = "Widget" };

            var rowsAffected = await CreateNoOutputPersister(provider, communication).InsertAsync(product);

            Assert.AreEqual(1, rowsAffected);
            Assert.AreEqual(42, product.Id);
            Assert.AreEqual(9, product.Rank);
            Assert.AreEqual(5, product.VersionNo);
            Assert.AreEqual(1, communication.TransactionsStarted);
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
            // The row version comes back unchanged on purpose. The submitted expression reads its values
            // off the entity when it is translated -- that is what makes one compiled query serve every
            // row -- and this test translates after the persister has already assigned the returned row
            // image back. A bumped version here would therefore change the predicate being asserted, for
            // a reason that has nothing to do with what the persister built.
            provider.OutputRows.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["Rank"] = 1,
                ["VersionNo"] = 1,
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

        #region compiled query reuse

        /// <summary>
        ///     <para>
        ///         The reason the persister reads values off the entity instead of emitting them as
        ///         constants. Two saves of two different rows must produce the same cache key, so one
        ///         compiled query serves the whole table rather than one being compiled and kept per row
        ///         ever written.
        ///     </para>
        ///     <para>
        ///         A constant would not do this: <c>ExpressionEqualityComparer</c> folds a constant's value
        ///         into the hash, and it has to — a constant becomes a literal, whose value is frozen at
        ///         translation time and never rebound, so two rows sharing an entry would write the first
        ///         row's values twice.
        ///     </para>
        /// </summary>
        [TestMethod]
        public void Insert_of_two_different_rows_shares_one_cache_key()
        {
            var keyProvider = new ExpressionCacheKeyProvider();

            var first = new CapturingQueryProvider();
            CreatePersister(first).Insert(new Tag { Code = "T1", Label = "First" });

            var second = new CapturingQueryProvider();
            CreatePersister(second).Insert(new Tag { Code = "T2", Label = "Second" });

            Assert.AreEqual(
                keyProvider.GetCacheKey(first.CapturedExpression),
                keyProvider.GetCacheKey(second.CapturedExpression),
                "Two rows of one entity type must share a compiled query.");
        }

        /// <summary>
        ///     <para>
        ///         Sharing a compiled query is only safe if each value can be found again on a cache hit,
        ///         which is done by the parameter's identity. Using the entity as the container gives every
        ///         column its own identity for free.
        ///     </para>
        ///     <para>
        ///         A single shared value holder would not: every column would arrive as
        ///         <c>Holder.Value</c>, and two columns claiming one identity with different values is
        ///         rejected outright — which is what this asserts by extracting the values the way the
        ///         execution path does.
        ///     </para>
        /// </summary>
        [TestMethod]
        public void Insert_values_are_parameters_with_one_identity_per_column()
        {
            var provider = new CapturingQueryProvider();
            CreatePersister(provider).Insert(new Tag { Code = "T1", Label = "First" });

            var extractor = new ExpressionVariableValuesExtractor(
                new global::Atis.SqlExpressionEngine.Services.ExpressionEvaluator(),
                new global::Atis.SqlExpressionEngine.Services.VariableIdentityProvider());

            var valuesByIdentity = extractor.ExtractVariableValuesByIdentity(provider.CapturedExpression);

            Assert.AreEqual(2, valuesByIdentity.Count, "Each column must carry its own parameter identity.");
            Assert.AreEqual("T1", valuesByIdentity.Single(x => x.Key.EndsWith("." + nameof(Tag.Code))).Value);
            Assert.AreEqual("First", valuesByIdentity.Single(x => x.Key.EndsWith("." + nameof(Tag.Label))).Value);
        }

        /// <summary>
        ///     The values are read from the entity at translation time, so the entity the persister was
        ///     handed is the one that gets written — not whichever entity first compiled this statement.
        /// </summary>
        [TestMethod]
        public void Insert_binds_the_values_of_the_entity_it_was_given()
        {
            var provider = new CapturingQueryProvider();
            CreatePersister(provider).Insert(new Tag { Code = "T2", Label = "Second" });

            string expectedResult = @"
insert into Tag (Code, Label)
values ('T2', 'Second')
";
            Test("Persister Insert Second Row Test", provider.CapturedExpression, expectedResult);
        }

        #endregion
    }
}
