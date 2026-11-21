using Atis.SqlExpressionEngine.Internal;
using Atis.SqlExpressionEngine.Services;
using Atis.SqlExpressionEngine.SqlExpressions;
using Atis.SqlExpressionEngine.Visitors;
using System;
using System.Linq;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     An expression built before a query was wrapped still points at the old alias. The query re-points it by
    ///     replaying the wraps it did. If something still points at a retired alias after that, the query refuses it
    ///     with a clear message instead of producing SQL that uses a data source that no longer exists.
    /// </summary>
    [TestClass]
    public class StaleExpressionGuardTests
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

        private WrapResult WrapQuery()
        {
            this.query.ApplyWhere(this.Equal(this.firstName, "A"), useOrOperator: false);
            var result = this.query.WrapIfRequired(SqlQueryOperation.Join);
            Assert.IsTrue(result.Wrapped);
            return result;
        }

        // A column of the retired data source that the wrap did not carry into the sub-query's select list:
        // replaying the wrap cannot find where it went.
        private SqlDataSourceColumnExpression UnfixableColumn()
            => new SqlDataSourceColumnExpression(this.firstName.DataSourceAlias, "NotInTheSelectList");

        private static int Hash(SqlExpression expression) => new SqlExpressionHashGenerator().Generate(expression);

        [TestMethod]
        public void Stale_predicate_is_repointed_to_the_wrapped_query()
        {
            var result = this.WrapQuery();

            this.query.ApplyWhere(this.Equal(this.firstName, "B"), useOrOperator: false);

            var applied = this.query.WhereClause.Single().Predicate;
            Assert.AreEqual(Hash(this.Equal(result.Rewrite(this.firstName), "B")), Hash(applied));
            Assert.AreNotEqual(Hash(this.Equal(this.firstName, "B")), Hash(applied));
        }

        [TestMethod]
        public void Rewritten_predicate_is_accepted()
        {
            var result = this.WrapQuery();

            this.query.ApplyWhere(this.Equal(result.Rewrite(this.firstName), "B"), useOrOperator: false);

            Assert.AreEqual(1, this.query.WhereClause.Count);
        }

        [TestMethod]
        public void Expression_that_points_at_an_unrelated_outer_query_is_accepted()
        {
            this.WrapQuery();
            var outerColumn = new SqlDataSourceColumnExpression(Guid.NewGuid(), "SomeColumn");

            this.query.ApplyWhere(this.Equal(outerColumn, "B"), useOrOperator: false);

            Assert.AreEqual(1, this.query.WhereClause.Count);
            Assert.AreEqual(Hash(this.Equal(outerColumn, "B")), Hash(this.query.WhereClause.Single().Predicate));
        }

        [TestMethod]
        public void Stale_projection_is_repointed_to_the_wrapped_query()
        {
            var result = this.WrapQuery();
            var projection = new SqlMemberInitExpression(new[] { new SqlMemberAssignment("Name", this.firstName) });

            this.query.ApplyProjection(projection);

            var applied = this.query.SelectList.Single().ColumnExpression;
            Assert.AreEqual(Hash(result.Rewrite(this.firstName)), Hash(applied));
        }

        [TestMethod]
        public void Stale_order_by_is_repointed_to_the_wrapped_query()
        {
            var result = this.WrapQuery();

            this.query.ApplyOrderBy(this.firstName, SortDirection.Ascending);

            var applied = this.query.OrderByClause.Single().ColumnExpression;
            Assert.AreEqual(Hash(result.Rewrite(this.firstName)), Hash(applied));
        }

        [TestMethod]
        public void Stale_query_shape_is_repointed_to_the_wrapped_query()
        {
            var result = this.WrapQuery();
            var shape = new SqlMemberInitExpression(new[] { new SqlMemberAssignment("Name", this.firstName) });

            this.query.UpdateModelBinding(shape);

            var appliedShape = (SqlQueryShapeExpression)this.query.GetQueryShapeForFieldMapping();
            Assert.IsTrue(appliedShape.TryResolveMember("Name", out var applied));
            Assert.AreEqual(Hash(result.Rewrite(this.firstName)), Hash(applied));
        }

        [TestMethod]
        public void Two_wraps_are_replayed_in_order()
        {
            var firstWrap = this.WrapQuery();
            // build something after the first wrap, then wrap again
            var shape = (SqlQueryShapeExpression)this.query.GetQueryShapeForFieldMapping();
            Assert.IsTrue(shape.TryResolveMember("FirstName", out var afterFirstWrap));
            this.query.ApplyWhere(this.Equal(afterFirstWrap, "C"), useOrOperator: false);
            var secondWrap = this.query.WrapIfRequired(SqlQueryOperation.Join);
            Assert.IsTrue(secondWrap.Wrapped);

            // an expression from before the first wrap needs both wraps
            this.query.ApplyWhere(this.Equal(this.firstName, "B"), useOrOperator: false);

            var expected = this.Equal(secondWrap.Rewrite(firstWrap.Rewrite(this.firstName)), "B");
            Assert.AreEqual(Hash(expected), Hash(this.query.WhereClause.Single().Predicate));
        }

        [TestMethod]
        public void Stale_expression_that_cannot_be_repointed_is_refused_with_a_message_that_explains_the_fix()
        {
            this.WrapQuery();

            var ex = Assert.ThrowsException<InvalidOperationException>(
                () => this.query.ApplyWhere(this.Equal(this.UnfixableColumn(), "B"), useOrOperator: false));

            StringAssert.Contains(ex.Message, nameof(SqlSelectExpression.ApplyWhere));
            StringAssert.Contains(ex.Message, "retired");
            StringAssert.Contains(ex.Message, nameof(WrapResult.Rewrite));
        }

        [TestMethod]
        public void The_refusal_does_not_depend_on_the_test_only_self_checks()
        {
            // Production runs with the self-checks off. The refusal must still work.
            var previous = SqlSelectExpressionDiagnostics.Mode;
            SqlSelectExpressionDiagnostics.Mode = SelectValidationMode.Off;
            try
            {
                this.WrapQuery();

                Assert.ThrowsException<InvalidOperationException>(
                    () => this.query.ApplyWhere(this.Equal(this.UnfixableColumn(), "B"), useOrOperator: false));
            }
            finally
            {
                SqlSelectExpressionDiagnostics.Mode = previous;
            }
        }

        [TestMethod]
        public void The_repointing_does_not_depend_on_the_test_only_self_checks()
        {
            var previous = SqlSelectExpressionDiagnostics.Mode;
            SqlSelectExpressionDiagnostics.Mode = SelectValidationMode.Off;
            try
            {
                var result = this.WrapQuery();

                this.query.ApplyWhere(this.Equal(this.firstName, "B"), useOrOperator: false);

                Assert.AreEqual(Hash(this.Equal(result.Rewrite(this.firstName), "B")), Hash(this.query.WhereClause.Single().Predicate));
            }
            finally
            {
                SqlSelectExpressionDiagnostics.Mode = previous;
            }
        }

        [TestMethod]
        public void Nothing_is_refused_before_the_first_wrap()
        {
            this.query.ApplyWhere(this.Equal(this.firstName, "A"), useOrOperator: false);
            this.query.ApplyWhere(this.Equal(this.firstName, "B"), useOrOperator: false);

            Assert.AreEqual(2, this.query.WhereClause.Count);
        }

        private SqlExpression Equal(SqlExpression left, string value)
            => this.factory.CreateBinary(left, this.factory.CreateLiteral(value), SqlExpressionType.Equal);
    }
}
