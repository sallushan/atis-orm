using Atis.SqlExpressionEngine.Services;
using Atis.SqlExpressionEngine.SqlExpressions;
using System.Linq;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     A <see cref="SqlSelectExpression"/> builds its <see cref="SqlDerivedTableExpression"/>
    ///     (<c>ToDerivedTable</c>), and can be created again from one (<c>FromDerivedTable</c>).
    /// </summary>
    [TestClass]
    public class DerivedTableConversionTests
    {
        private SqlExpressionFactory factory;
        private SqlSelectExpression query;
        private SqlDataSourceColumnExpression firstName;

        [TestInitialize]
        public void Setup()
        {
            this.factory = new SqlExpressionFactory();
            var table = this.factory.CreateTable(
                new SqlTable("Person"),
                new[] { new TableColumn("FRST_NM", "FirstName"), new TableColumn("AGE", "Age") });
            this.query = this.factory.CreateSelectQuery(table);

            var shape = (SqlQueryShapeExpression)this.query.GetQueryShapeForFieldMapping();
            Assert.IsTrue(shape.TryResolveMember("FirstName", out var resolved));
            this.firstName = (SqlDataSourceColumnExpression)resolved;
        }

        [TestMethod]
        public void ToDerivedTable_copies_the_state_and_does_not_change_the_query()
        {
            this.query.ApplyWhere(this.Equal(this.firstName, "A"), useOrOperator: false);
            this.query.ApplyTop(5);
            var aliasBefore = this.query.FromDataSourceAlias;

            var derivedTable = this.query.ToDerivedTable();

            Assert.AreEqual(aliasBefore, derivedTable.FromSource.Alias);
            Assert.AreEqual(1, derivedTable.WhereClause.FilterConditions.Count);
            Assert.AreEqual(5, derivedTable.Top);
            // the query itself is as it was
            Assert.AreEqual(aliasBefore, this.query.FromDataSourceAlias);
            Assert.AreEqual(1, this.query.WhereClause.Count);
            Assert.AreEqual(5, this.query.Top);
            Assert.IsFalse(this.query.HasProjectionApplied);
        }

        [TestMethod]
        public void The_factory_methods_give_the_same_result_as_the_query()
        {
            this.query.ApplyWhere(this.Equal(this.firstName, "A"), useOrOperator: false);

            var fromQuery = this.query.ToDerivedTable();
            var fromFactory = this.factory.ConvertSelectQueryToDeriveTable(this.query);

            Assert.AreEqual(fromQuery.FromSource.Alias, fromFactory.FromSource.Alias);
            Assert.AreEqual(fromQuery.NodeType, fromFactory.NodeType);
            Assert.AreEqual(SqlExpressionType.DerivedTable, fromFactory.NodeType);
            Assert.AreEqual(SqlExpressionType.UnwrappableDerivedTable, this.factory.ConvertSelectQueryToUnwrappableDeriveTable(this.query).NodeType);
        }

        [TestMethod]
        public void A_plain_derived_table_is_opened_up_with_new_aliases()
        {
            this.query.ApplyWhere(this.Equal(this.firstName, "A"), useOrOperator: false);
            var derivedTable = this.query.ToDerivedTable();

            var opened = SqlSelectExpression.FromDerivedTable(derivedTable, this.factory);

            Assert.AreEqual(1, opened.WhereClause.Count);
            Assert.IsTrue(opened.DataSources.Single().QuerySource is SqlTableExpression,
                "The query continues on the table; it does not put the derived table in one more level.");
            Assert.AreNotEqual(derivedTable.FromSource.Alias, opened.FromDataSourceAlias);

            // the filter now uses the new alias of the new query
            var predicate = (SqlBinaryExpression)opened.WhereClause.Single().Predicate;
            var column = (SqlDataSourceColumnExpression)predicate.Left;
            Assert.AreEqual(opened.FromDataSourceAlias, column.DataSourceAlias);
        }

        [TestMethod]
        public void A_derived_table_with_its_own_top_becomes_the_data_source()
        {
            this.query.ApplyTop(5);
            var derivedTable = this.query.ToDerivedTable();

            var wrapped = SqlSelectExpression.FromDerivedTable(derivedTable, this.factory);

            Assert.IsTrue(wrapped.DataSources.Single().QuerySource is SqlDerivedTableExpression);
            Assert.AreEqual(0, wrapped.WhereClause.Count);
            Assert.IsNull(wrapped.Top);
        }

        [TestMethod]
        public void A_data_manipulation_derived_table_has_no_select_list_and_keeps_the_tag()
        {
            this.query.ApplyWhere(this.Equal(this.firstName, "A"), useOrOperator: false);
            this.query.Tag = "Navigation1";

            var dm = this.query.ToDataManipulationDerivedTable();

            Assert.AreEqual(SqlExpressionType.DataManipulationDerivedTable, dm.NodeType);
            Assert.IsNull(dm.SelectColumnCollection);
            Assert.AreEqual("Navigation1", dm.Tag);
            Assert.AreEqual(1, dm.WhereClause.FilterConditions.Count);
        }

        [TestMethod]
        public void CreateSelectQuery_on_a_derived_table_goes_through_the_same_path()
        {
            this.query.ApplyWhere(this.Equal(this.firstName, "A"), useOrOperator: false);
            var derivedTable = this.query.ToDerivedTable();

            var viaFactory = this.factory.CreateSelectQuery(derivedTable);

            Assert.AreEqual(1, viaFactory.WhereClause.Count);
            Assert.IsTrue(viaFactory.DataSources.Single().QuerySource is SqlTableExpression);
        }

        private SqlExpression Equal(SqlExpression left, string value)
            => this.factory.CreateBinary(left, this.factory.CreateLiteral(value), SqlExpressionType.Equal);
    }
}
