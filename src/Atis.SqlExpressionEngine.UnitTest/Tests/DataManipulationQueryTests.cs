using System.Linq.Expressions;
using Atis.Orm;
using Atis.Orm.Services;
using Atis.SqlExpressionEngine.SqlExpressions;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    [TestClass]
    public class DataManipulationQueryTests : TestBase
    {
        [TestMethod]
        public void Update_query_single_table()
        {
            var assets = new Queryable<Asset>(this.queryProvider);
            Expression<Func<int>> expr = () => assets.Update(x => new Asset { SerialNumber = "ABC", Description = "Check" }, x => x.SerialNumber == "123");
            var queryExpression = expr.Body;
            string expectedResult = @"
update a_1
	set SerialNumber = 'ABC',
		Description = 'Check'
from	Asset as a_1
where	(a_1.SerialNumber = '123')
";
            Test($"Update Query Single Table Test", queryExpression, expectedResult);
        }

        [TestMethod]
        public void Update_query_with_output_is_an_unexecuted_method_call_expression()
        {
            var assets = new Queryable<Asset>(this.queryProvider);
            Expression<Func<IReadOnlyList<IReadOnlyDictionary<string, object>>>> expr = () =>
                assets.Update(
                    x => new Asset { Description = "Check" },
                    x => x.ItemId == "123",
                    x => new object[] { x.SerialNumber, x.RowId });

            var updateCall = expr.Body as MethodCallExpression;
            Assert.IsNotNull(updateCall, "The output update must use the standard LINQ method-call AST shape.");
            Assert.AreEqual(nameof(QueryExtensions.Update), updateCall.Method.Name);
            Assert.AreEqual(typeof(QueryExtensions), updateCall.Method.DeclaringType);

            string expectedResult = @"
update a_1
	set Description = 'Check'
output inserted.SerialNumber as SerialNumber, inserted.RowId as RowId
from	Asset as a_1
where	(a_1.ItemId = '123')
";
            Test("Update Query With Output Test", updateCall, expectedResult);
        }

        [TestMethod]
        public void Update_query_navigation_selected_to_update()
        {
            var assets = new Queryable<Asset>(this.queryProvider);
            Expression<Func<int>> expr = () => assets.Update(
                                                        asset => asset.NavItem(),
                                                        asset => new ItemBase { ItemDescription = asset.NavItem().ItemDescription + asset.SerialNumber },
                                                        asset => asset.SerialNumber == "123");
            var queryExpression = expr.Body;
            string expectedResult = @"
update NavItem_2
	set ItemDescription = (NavItem_2.ItemDescription + a_1.SerialNumber)
from Asset as a_1
			inner join ItemBase as NavItem_2 on (NavItem_2.ItemId = a_1.ItemId)
	where (a_1.SerialNumber = '123')
";
            Test($"Update Query Multiple Table Navigation Test", queryExpression, expectedResult);
        }

        [TestMethod]
        public void Update_query_navigation_selected_to_update_with_output()
        {
            var assets = new Queryable<Asset>(this.queryProvider);
            Expression<Func<IReadOnlyList<IReadOnlyDictionary<string, object>>>> expr = () => assets.Update(
                asset => asset.NavItem(),
                asset => new ItemBase { ItemDescription = asset.NavItem().ItemDescription + asset.SerialNumber },
                asset => asset.SerialNumber == "123",
                asset => new object[] { asset.NavItem().ItemId, asset.NavItem().ItemDescription });

            string expectedResult = @"
update NavItem_2
	set ItemDescription = (NavItem_2.ItemDescription + a_1.SerialNumber)
output inserted.ItemId as ItemId, inserted.ItemDescription as ItemDescription
from Asset as a_1
			inner join ItemBase as NavItem_2 on (NavItem_2.ItemId = a_1.ItemId)
	where (a_1.SerialNumber = '123')
";
            Test("Update Query Multiple Table Navigation With Output Test", expr.Body, expectedResult);
        }


        [TestMethod]
        public void Update_query_with_query_syntax_multiple_tables_joined_with_one_table_selected_to_update()
        {
            var assets = new Queryable<Asset>(this.queryProvider);
            var items = new Queryable<ItemBase>(this.queryProvider);

            Expression<Func<int>> expr = () => (
                                                from asset in assets
                                                join item in items on asset.ItemId equals item.ItemId
                                                select new { asset, item }                  // joined 2 tables in 1 query
                                               )
                                               .Update(                                     // <- Update query
                                                    ms => ms.item,                          // <- which table to update
                                                    ms => new ItemBase                      // <- which fields to update
                                                    {
                                                        ItemDescription = ms.item.ItemDescription + ms.asset.SerialNumber
                                                    },
                                                    ms => ms.asset.SerialNumber == "123"    // <- where condition
                                                );

            var queryExpression = expr.Body;
            string expectedResult = @"
update a_2
	set ItemDescription = (a_2.ItemDescription + a_1.SerialNumber)
from Asset as a_1
			inner join ItemBase as a_2 on (a_1.ItemId = a_2.ItemId)
	where (a_1.SerialNumber = '123')
";
            Test($"Update Query Multiple Table Join Test", queryExpression, expectedResult);
        }

        [TestMethod]
        public void Update_query_using_From_query_extension_method_then_finally_navigation_property_selected_to_update()
        {
            var assets = new Queryable<Asset>(this.queryProvider);
            var items = new Queryable<ItemBase>(this.queryProvider);
            Expression<Func<int>> expr = () => queryProvider.From(() => new { asset = QueryExtensions.Table<Asset>(), item = QueryExtensions.Table<ItemBase>() })
                                                    .InnerJoin(x => x.item, x => x.asset.ItemId == x.item.ItemId)
                                                    .Select(x => x.item)
                                                    .Update(ms => ms.NavItemMoreInfo(), ms => new ItemMoreInfo { TrackingType = "TT" }, ms => ms.ItemDescription.Contains("123"));
            var queryExpression = expr.Body;
            string expectedResult = @"
update NavItemMoreInfo_4
	set TrackingType = 'TT'
from (
			select a_2.ItemId as ItemId, a_2.ItemDescription as ItemDescription
			from Asset as a_1
					inner join ItemBase as a_2 on (a_1.ItemId = a_2.ItemId)
		) as a_3
			left join ItemMoreInfo as NavItemMoreInfo_4 on (a_3.ItemId = NavItemMoreInfo_4.ItemId)
where (a_3.ItemDescription like '%' + '123' + '%')
";
            Test($"Update Query Multiple Table From Test", queryExpression, expectedResult);
        }

        [TestMethod]
        public void Update_query_using_dynamic_joins_to_add_multiple_data_sources_with_multiple_data_source_selected_in_projection_then_updating_one_data_source_from_that_projection()
        {
            var assets = new Queryable<Asset>(this.queryProvider);
            var items = new Queryable<ItemBase>(this.queryProvider);
            var moreInfo = new Queryable<ItemMoreInfo>(this.queryProvider);
            Expression<Func<int>> expr = () => assets
                                                    .InnerJoin(items, (assets, joinedTable) => new { a = assets, j1 = joinedTable }, newShape => newShape.a.ItemId == newShape.j1.ItemId)
                                                    .InnerJoin(moreInfo, (oldShape, joinedTable) => new { os1 = oldShape, j2 = joinedTable }, newShape => newShape.j2.ItemId == newShape.os1.j1.ItemId)
                                                    .Update(ms => ms.os1.j1, ms => new ItemBase { ItemDescription = ms.os1.a.SerialNumber + ms.os1.j1.ItemId }, ms => ms.os1.j1.ItemDescription.Contains("123"));
            var queryExpression = expr.Body;
            string expectedResult = @"
update a_2
	set ItemDescription = (a_1.SerialNumber + a_2.ItemId)
from Asset as a_1
			inner join ItemBase as a_2 on (a_1.ItemId = a_2.ItemId)
			inner join ItemMoreInfo as a_3 on (a_3.ItemId = a_2.ItemId)
	where (a_2.ItemDescription like '%' + '123' + '%')
";
            Test($"Update Query Multiple Table Complex Object For ModelPath Test", queryExpression, expectedResult);
        }

        [TestMethod]
        public void Delete_query_single_table()
        {
            var assets = new Queryable<Asset>(this.queryProvider);
            Expression<Func<int>> expr = () => assets.Delete(x => x.SerialNumber == "123");
            var queryExpression = expr.Body;
            string expectedResult = @"
delete a_1
from	Asset as a_1
where	(a_1.SerialNumber = '123')
";
            Test($"Delete Query Single Table Test", queryExpression, expectedResult);
        }

        [TestMethod]
        public void Delete_query_with_navigation_selected_to_delete()
        {
            var assets = new Queryable<Asset>(this.queryProvider);
            Expression<Func<int>> expr = () => assets.Delete(asset => asset.NavItem(),
                                                                asset => asset.SerialNumber == "123");
            var queryExpression = expr.Body;
            string expectedResult = @"
delete NavItem_2
from Asset as a_1
			inner join ItemBase as NavItem_2 on (NavItem_2.ItemId = a_1.ItemId)
	where (a_1.SerialNumber = '123')
";

            Test($"Delete Query Multiple Table Navigation Test", queryExpression, expectedResult);
        }


        [TestMethod]
        public void Bulk_insert()
        {
            var siteExtList = new Queryable<SiteExtension>(this.queryProvider);
            Expression<Func<int>> expr = () => siteExtList.Where(x => x.AttributeType == "T1" && x.AttributeValue == "V1")
                                                .Select(x => new SiteAuthorizationSetting { RowId = Guid.NewGuid(), ModuleName = "Module1", SiteId = x.SiteId, AuthorizationUserId = "User1" })
                                                .BulkInsert();
            var queryExpression = expr.Body;
            string expectedResult = @"
insert into SiteAuthorizationSetting(RowId, ModuleName, SiteId, AuthorizationUserId)
(
	select newId() as RowId, 'Module1' as ModuleName, a_1.SiteId as SiteId, 'User1' as AuthorizationUserId
	from SiteExtension as a_1
	where ((a_1.AttributeType = 'T1') and (a_1.AttributeValue = 'V1'))
)
";
            Test("Bulk Insert Test", queryExpression, expectedResult);
        }

        /// <summary>
        ///     <para>
        ///         The ORM fluent update without an <c>Output</c> clause. Its terminal method must
        ///         submit the same <c>QueryExtensions.Update</c> method-call AST as the engine API.
        ///     </para>
        /// </summary>
        [TestMethod]
        public void Update_fluent_api_without_output()
        {
            var provider = new ExpressionCapturingQueryProvider();

            provider.UpdateEntity<Asset>()
                    .Set(x => x.SerialNumber, () => "ABC")
                    .Set(x => x.Description, () => "Check")
                    .Key(x => x.ItemId, () => "123")
                    .Execute();

            var updateCall = provider.CapturedExpression as MethodCallExpression;
            Assert.IsNotNull(updateCall, "The ORM facade must submit a standard method-call expression.");
            Assert.AreEqual(nameof(QueryExtensions.Update), updateCall.Method.Name);
            Assert.AreEqual(typeof(QueryExtensions), updateCall.Method.DeclaringType);

            string expectedResult = @"
update a_1
	set SerialNumber = 'ABC',
		Description = 'Check'
from	Asset as a_1
where	(a_1.ItemId = '123')
";
            Test("Update Fluent API Without Output Test", provider.CapturedExpression, expectedResult);
        }

        /// <summary>
        ///     The field selector has an entity parameter, but the value selector passed to Set is a
        ///     parameterless lambda. Building the standard Update setter must not leave the field
        ///     selector's original parameter unbound in the generated member-init expression.
        /// </summary>
        [TestMethod]
        public void Update_fluent_api_setter_lambda_has_no_unbound_parameters()
        {
            var provider = new ExpressionCapturingQueryProvider();
            var description = "Captured value";

            provider.UpdateEntity<Asset>()
                    .Set(x => x.Description, () => description)
                    .Key(x => x.ItemId, () => "123")
                    .Execute();

            var updateCall = (MethodCallExpression)provider.CapturedExpression;
            var setter = (Expression<Func<Asset, Asset>>)((UnaryExpression)updateCall.Arguments[1]).Operand;

            Assert.AreEqual(1, setter.Parameters.Count,
                "The standard Update setter must have exactly its required entity parameter.");

            var updated = setter.Compile()(new Asset());
            Assert.AreEqual(description, updated.Description,
                "A captured Set value must remain valid after the setter lambda is rebuilt.");
        }

        /// <summary>
        ///     <para>
        ///         An <c>Execute()</c> update reports <see cref="int"/> — the affected-row count it
        ///         returns. It used to report <c>null</c>, which breaks the
        ///         <see cref="Expression"/> contract: every consumer is entitled to read
        ///         <see cref="Expression.Type"/>, and the ones that build on top of it (here,
        ///         <see cref="Expression.Lambda(Expression, ParameterExpression[])"/>) throw.
        ///     </para>
        /// </summary>
        [TestMethod]
        public void Update_fluent_api_without_output_reports_int_as_its_type()
        {
            var provider = new ExpressionCapturingQueryProvider();

            provider.UpdateEntity<Asset>()
                    .Set(x => x.Description, () => "Check")
                    .Key(x => x.ItemId, () => "123")
                    .Execute();

            Assert.AreEqual(typeof(int), provider.CapturedExpression.Type,
                "An update without an output clause returns the affected-row count.");

            var lambda = Expression.Lambda(provider.CapturedExpression);
            Assert.AreEqual(typeof(int), lambda.ReturnType,
                "The node must be usable anywhere the LINQ contract requires a Type.");
        }

        /// <summary>
        ///     <para>
        ///         Running the same fluent update twice must produce the same compiled-query cache
        ///         key, or the query is recompiled on every execution and the cache grows an entry
        ///         per call.
        ///     </para>
        /// </summary>
        [TestMethod]
        public void Update_fluent_api_produces_a_stable_cache_key()
        {
            var keyProvider = new ExpressionCacheKeyProvider();

            Assert.AreEqual(
                keyProvider.GetCacheKey(BuildUpdate()),
                keyProvider.GetCacheKey(BuildUpdate()),
                "Two identical fluent updates must share a cache key.");

            Expression BuildUpdate()
            {
                var provider = new ExpressionCapturingQueryProvider();
                provider.UpdateEntity<Asset>()
                        .Set(x => x.Description, () => "Check")
                        .Key(x => x.ItemId, () => "123")
                        .Execute();
                return provider.CapturedExpression;
            }
        }

        /// <summary>
        ///     The control for <c>Update_fluent_api_produces_a_stable_cache_key</c>: the same key
        ///     provider is stable for an ordinary LINQ query, so a difference is about how extension
        ///     nodes are hashed and not about the mechanism as a whole.
        /// </summary>
        [TestMethod]
        public void Ordinary_query_produces_a_stable_cache_key()
        {
            var keyProvider = new ExpressionCacheKeyProvider();

            Assert.AreEqual(
                keyProvider.GetCacheKey(BuildQuery()),
                keyProvider.GetCacheKey(BuildQuery()),
                "Two identical LINQ queries must share a cache key.");

            Expression BuildQuery()
                => new Queryable<Asset>(new QueryProvider())
                        .Where(x => x.ItemId == "123")
                        .Expression;
        }

        /// <summary>
        ///     <para>
        ///         Two chained <c>Output</c> calls, the second one naming a value-type column. The
        ///         second call binds to a different overload than the first, and that overload used
        ///         to take <c>Expression&lt;Func&lt;T, object&gt;&gt;</c> — which boxes a value type
        ///         behind a <c>Convert</c> node and defeated the converter's alias extraction.
        ///     </para>
        ///     <para>
        ///         Asserts the node as well as the SQL: the aliases are what the fix is about, and
        ///         reading them off <see cref="SqlUpdateExpression.Outputs"/> says so directly.
        ///     </para>
        /// </summary>
        [TestMethod]
        public void Update_fluent_api_with_multiple_outputs_including_value_type()
        {
            var provider = new ExpressionCapturingQueryProvider();

            provider.UpdateEntity<Asset>()
                    .Set(x => x.Description, () => "Check")
                    .Key(x => x.ItemId, () => "123")
                    .Output(x => x.SerialNumber)       // string  — first Output overload
                    .Output(x => x.RowId)              // Guid    — chained Output overload
                    .ExecuteDictionary();

            var sqlExpression = ConvertExpressionToSqlExpression(provider.CapturedExpression, out _);

            var update = sqlExpression as SqlUpdateExpression;
            Assert.IsNotNull(update, "The fluent update must convert to a SqlUpdateExpression.");
            Assert.AreEqual(2, update.Outputs.Count, "Both selected output columns must survive conversion.");

            Assert.AreEqual("SerialNumber", update.Outputs[0].Alias);
            Assert.AreEqual("RowId", update.Outputs[1].Alias,
                "The value-type column selected by the chained Output overload must keep its member name as the alias.");

            var rowIdColumn = update.Outputs[1].ColumnExpression as SqlOutputColumnExpression;
            Assert.IsNotNull(rowIdColumn, "An output column carries its row image, not a data-source alias.");
            Assert.AreEqual("RowId", rowIdColumn.ColumnName);
            Assert.AreEqual(SqlOutputSource.Inserted, rowIdColumn.Source,
                "An update reports the row as it now stands.");

            string expectedResult = @"
update a_1
	set Description = 'Check'
output inserted.SerialNumber as SerialNumber, inserted.RowId as RowId
from	Asset as a_1
where	(a_1.ItemId = '123')
";
            Test("Update Fluent API Multiple Outputs Test", provider.CapturedExpression, expectedResult);
        }

        /// <summary>
        ///     <para>
        ///         An UPDATE's output columns are part of the statement, so a visitor has to reach them
        ///         and its rewrites have to be carried into the new node. <c>VisitSqlUpdate</c> used to
        ///         visit only the source and the SET values, leaving the OUTPUT clause pointing at
        ///         whatever the visitor had just replaced everywhere else.
        ///     </para>
        /// </summary>
        [TestMethod]
        public void Visiting_an_update_reaches_and_rewrites_its_output_columns()
        {
            var provider = new ExpressionCapturingQueryProvider();
            provider.UpdateEntity<Asset>()
                    .Set(x => x.Description, () => "Check")
                    .Key(x => x.ItemId, () => "123")
                    .Output(x => x.SerialNumber)
                    .Output(x => x.RowId)
                    .ExecuteDictionary();

            var update = (SqlUpdateExpression)ConvertExpressionToSqlExpression(provider.CapturedExpression, out _);
            Assert.AreEqual(2, update.Outputs.Count, "Guard: two output columns to visit.");

            var rewritten = (SqlUpdateExpression)new OutputColumnSuffixingVisitor().Visit(update);

            CollectionAssert.AreEqual(
                new[] { "SerialNumber_visited", "RowId_visited" },
                rewritten.Outputs.Select(x => ((SqlOutputColumnExpression)x.ColumnExpression).ColumnName).ToArray(),
                "Every output column must be visited, and the replacement carried into the new statement.");
            CollectionAssert.AreEqual(
                new[] { "SerialNumber", "RowId" },
                rewritten.Outputs.Select(x => x.Alias).ToArray(),
                "Rewriting the column must not disturb its alias.");
        }

        private sealed class OutputColumnSuffixingVisitor : Atis.SqlExpressionEngine.Visitors.SqlExpressionVisitor
        {
            protected override SqlExpression VisitSqlOutputColumn(SqlOutputColumnExpression node)
                => new SqlOutputColumnExpression(node.Source, node.ColumnName + "_visited");
        }

        /// <summary>
        ///     <para>
        ///         Two outputs with the same name cannot both survive into a dictionary row. The element
        ///         factory used to let the second silently overwrite the first, while
        ///         <c>ExecuteDictionary</c> on the same shape of data threw — the two now share one rule,
        ///         and this one catches it at plan time rather than on every row.
        ///     </para>
        /// </summary>
        [TestMethod]
        public void An_update_with_duplicate_output_names_is_rejected()
        {
            var provider = new ExpressionCapturingQueryProvider();
            provider.UpdateEntity<Asset>()
                    .Set(x => x.Description, () => "Check")
                    .Key(x => x.ItemId, () => "123")
                    .Output(x => x.SerialNumber)
                    .Output(x => x.SerialNumber)
                    .ExecuteDictionary();

            var update = (SqlUpdateExpression)ConvertExpressionToSqlExpression(provider.CapturedExpression, out _);

            var thrown = Assert.ThrowsException<InvalidOperationException>(
                () => new Atis.Orm.Services.ElementFactoryBuilder().CreateElementFactory(provider.CapturedExpression, update));

            StringAssert.Contains(thrown.Message, nameof(Asset.SerialNumber),
                "The error must name the duplicated column.");
        }

        /// <summary>
        ///     "No OUTPUT clause" is spelled as null by callers, but must not reach consumers that way —
        ///     <c>Columns</c> and <c>Values</c> reject null, and <c>Outputs</c> should not be the one
        ///     member everybody has to null-check.
        /// </summary>
        [TestMethod]
        public void An_update_without_outputs_exposes_an_empty_list_not_null()
        {
            var provider = new ExpressionCapturingQueryProvider();
            provider.UpdateEntity<Asset>()
                    .Set(x => x.Description, () => "Check")
                    .Key(x => x.ItemId, () => "123")
                    .Execute();

            var update = (SqlUpdateExpression)ConvertExpressionToSqlExpression(provider.CapturedExpression, out _);

            Assert.IsNotNull(update.Outputs);
            Assert.AreEqual(0, update.Outputs.Count);
        }

        /// <summary>
        ///     <para>
        ///         An update with output is terminal. Its expression exposes the read-only list
        ///         returned to the caller, so it cannot be composed with another query operator.
        ///     </para>
        /// </summary>
        [TestMethod]
        public void Update_fluent_api_with_output_reports_a_read_only_list_type()
        {
            var provider = new ExpressionCapturingQueryProvider();

            provider.UpdateEntity<Asset>()
                    .Set(x => x.Description, () => "Check")
                    .Key(x => x.ItemId, () => "123")
                    .Output(x => x.SerialNumber)
                    .ExecuteDictionary();

            Assert.AreEqual(
                typeof(IReadOnlyList<IReadOnlyDictionary<string, object>>),
                provider.CapturedExpression.Type);
            Assert.IsFalse(typeof(IQueryable).IsAssignableFrom(provider.CapturedExpression.Type),
                "A terminal update result must not allow further query composition.");
            Assert.AreEqual(typeof(Dictionary<string, object>), provider.CreatedQueryElementType,
                "Update output rows must be materialized through CreateQuery<Dictionary<string, object>>.");
        }

        /// <summary>
        ///     <para>
        ///         A calculated property is not a stored column, so it cannot be assigned. This one maps
        ///         to a single column (<c>CalcFullName</c> is <c>e =&gt; e.Name</c>), which is the
        ///         dangerous shape: the SET list used to be encoded as an equality and the column name
        ///         read back off the converted expression, so it converted happily into
        ///         <c>set Name = 'X'</c> — updating a column the caller never named, with no error at any
        ///         point.
        ///     </para>
        ///     <para>
        ///         Now rejected while the expression is being built, before conversion, and the message
        ///         names the member.
        ///     </para>
        /// </summary>
        [TestMethod]
        public void Update_fluent_api_rejects_a_calculated_property_that_maps_to_one_column()
        {
            var provider = new ExpressionCapturingQueryProvider();

            var thrown = Assert.ThrowsException<InvalidOperationException>(() =>
                provider.UpdateEntity<Employee>()
                        .Set(x => x.CalcFullName, () => "X")
                        .Key(x => x.EmployeeId, () => "1")
                        .Execute());

            StringAssert.Contains(thrown.Message, nameof(Employee.CalcFullName),
                "The error must name the member the caller wrote, not an internal expression type.");
        }

        /// <summary>
        ///     The same rule for a calculated property whose expression is not a column reference at all.
        ///     Building the update used to succeed; the failure came later, at conversion, as
        ///     <c>InvalidCastException: Unable to cast SqlExpression 'SqlConditionalExpression' to
        ///     'SqlDataSourceColumnExpression'</c> — naming neither the property nor the reason.
        /// </summary>
        [TestMethod]
        public void Update_fluent_api_rejects_a_calculated_property_with_a_compound_expression()
        {
            var provider = new ExpressionCapturingQueryProvider();

            var thrown = Assert.ThrowsException<InvalidOperationException>(() =>
                provider.UpdateEntity<Marksheet>()
                        .Set(x => x.CalcPercentage, () => 50m)
                        .Key(x => x.RowId, () => Guid.Empty)
                        .Execute());

            StringAssert.Contains(thrown.Message, nameof(Marksheet.CalcPercentage),
                "The error must name the member the caller wrote.");
        }

        /// <summary>
        ///     <para>
        ///         A boxed selector names a stored column that <c>Set</c> simply cannot be given an
        ///         <c>object</c>. It is reachable because the field selector's <c>Convert</c> node is
        ///         stripped to find the member, so <c>FT</c> can be wider than the member it names.
        ///     </para>
        ///     <para>
        ///         The check used to be a <c>catch (ArgumentException)</c> around
        ///         <c>Expression.Bind</c>, which reports a type mismatch and a missing setter the same
        ///         way — so this case was diagnosed as a read-only column.
        ///     </para>
        /// </summary>
        [TestMethod]
        public void Update_fluent_api_rejects_a_value_that_does_not_fit_the_column()
        {
            var provider = new ExpressionCapturingQueryProvider();

            var thrown = Assert.ThrowsException<InvalidOperationException>(() =>
                provider.UpdateEntity<Asset>()
                        .Set(x => (object)x.RowId, () => (object)Guid.Empty)
                        .Key(x => x.ItemId, () => "123")
                        .Execute());

            StringAssert.Contains(thrown.Message, nameof(Asset.RowId),
                "The error must name the member the caller wrote.");
            StringAssert.Contains(thrown.Message, "cannot be assigned to a member of type",
                "A type mismatch must not be reported as a read-only column.");
        }

        /// <summary>
        ///     Stands in for a real provider so the expression the fluent builder produces can be
        ///     translated without a database. The unit-test <see cref="QueryProvider"/> throws from
        ///     <c>Execute</c>, which would hide the expression under test.
        /// </summary>
        private sealed class ExpressionCapturingQueryProvider : IQueryProvider
        {
            public Expression CapturedExpression { get; private set; }

            public Type CreatedQueryElementType { get; private set; }

            public IQueryable CreateQuery(Expression expression)
            {
                this.CapturedExpression = expression;
                return new Queryable(this, expression);
            }

            public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
            {
                this.CapturedExpression = expression;
                this.CreatedQueryElementType = typeof(TElement);
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
                return default;
            }
        }
    }
}
