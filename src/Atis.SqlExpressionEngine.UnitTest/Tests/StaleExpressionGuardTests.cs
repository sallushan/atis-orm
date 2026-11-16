using Atis.SqlExpressionEngine.Internal;
using Atis.SqlExpressionEngine.Services;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Linq;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     An expression built before a query was wrapped still points at the old alias. The query refuses it
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

        [TestMethod]
        public void Stale_predicate_is_refused_with_a_message_that_explains_the_fix()
        {
            this.WrapQuery();

            var ex = Assert.ThrowsException<InvalidOperationException>(
                () => this.query.ApplyWhere(this.Equal(this.firstName, "B"), useOrOperator: false));

            StringAssert.Contains(ex.Message, nameof(SqlSelectExpression.ApplyWhere));
            StringAssert.Contains(ex.Message, "retired");
            StringAssert.Contains(ex.Message, nameof(WrapResult.Rewrite));
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
        }

        [TestMethod]
        public void Stale_projection_is_refused()
        {
            this.WrapQuery();
            var projection = new SqlMemberInitExpression(new[] { new SqlMemberAssignment("Name", this.firstName) });

            Assert.ThrowsException<InvalidOperationException>(() => this.query.ApplyProjection(projection));
        }

        [TestMethod]
        public void Stale_order_by_is_refused()
        {
            this.WrapQuery();

            Assert.ThrowsException<InvalidOperationException>(
                () => this.query.ApplyOrderBy(this.firstName, SortDirection.Ascending));
        }

        [TestMethod]
        public void Stale_query_shape_is_refused()
        {
            this.WrapQuery();
            var shape = new SqlMemberInitExpression(new[] { new SqlMemberAssignment("Name", this.firstName) });

            Assert.ThrowsException<InvalidOperationException>(() => this.query.UpdateModelBinding(shape));
        }

        [TestMethod]
        public void The_guard_does_not_depend_on_the_test_only_self_checks()
        {
            // Production runs with the self-checks off. The guard must still work.
            var previous = SqlSelectExpressionDiagnostics.Mode;
            SqlSelectExpressionDiagnostics.Mode = SelectValidationMode.Off;
            try
            {
                this.WrapQuery();

                Assert.ThrowsException<InvalidOperationException>(
                    () => this.query.ApplyWhere(this.Equal(this.firstName, "B"), useOrOperator: false));
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
