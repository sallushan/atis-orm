using System.Linq.Expressions;
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
        ///         The fluent update without an <c>Output</c> clause. This goes through
        ///         <c>Execute()</c>, which builds the <c>UpdateEntityExpression</c> with no output
        ///         fields — the case that used to die with a <see cref="NullReferenceException"/>
        ///         inside the converter before it ever reached a database.
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
        ///         Two chained <c>Output</c> calls, the second one naming a value-type column. The
        ///         second call binds to a different overload than the first, and that overload used
        ///         to take <c>Expression&lt;Func&lt;T, object&gt;&gt;</c> — which boxes a value type
        ///         behind a <c>Convert</c> node and defeated the converter's alias extraction.
        ///     </para>
        ///     <para>
        ///         Asserted against the <see cref="SqlUpdateExpression"/> rather than the rendered
        ///         SQL because this project's <see cref="SqlExpressionTranslator"/> does not render
        ///         an OUTPUT clause at all — see <c>Update_fluent_api_output_columns_survive</c>.
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

            var rowIdColumn = update.Outputs[1].ColumnExpression as SqlColumnExpression;
            Assert.IsNotNull(rowIdColumn, "An output column is read from the OUTPUT pseudo-table.");
            Assert.AreEqual("RowId", rowIdColumn.ColumnName);
        }

        /// <summary>
        ///     Stands in for a real provider so the expression the fluent builder produces can be
        ///     translated without a database. The unit-test <see cref="QueryProvider"/> throws from
        ///     <c>Execute</c>, which would hide the expression under test.
        /// </summary>
        private sealed class ExpressionCapturingQueryProvider : IQueryProvider
        {
            public Expression CapturedExpression { get; private set; }

            public IQueryable CreateQuery(Expression expression)
            {
                this.CapturedExpression = expression;
                return new Queryable(this, expression);
            }

            public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
            {
                this.CapturedExpression = expression;
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

        [TestMethod]
        public void Update_fluent_Api()
        {
            var db = new OrmDbContext();
            db.Transaction(() =>
            {
                var results = db.UpdateEntity<TestEntities.Employee>()
                            .Set(x => x.LastName, () => "Smith")
                            .Set(x => x.FirstName, () => "John")
                            .Key(x => x.EmployeeId, () => 1)
                            .Output(x => x.EmployeeId)
                            .ExecuteDictionary();
                foreach (var item in results)
                {
                    Console.WriteLine(item["EmployeeId"]);
                }
            });
        }
    }
}
