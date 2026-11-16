using Atis.SqlExpressionEngine.Services;
using Atis.SqlExpressionEngine.SqlExpressions;
using System.Linq;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <see cref="SqlSelectExpression.WrapIfRequired(SqlQueryOperation)"/> returns a <see cref="WrapResult"/>
    ///     that re-points expressions built before the wrap to the new outer query.
    /// </summary>
    [TestClass]
    public class WrapResultTests
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
        public void Nothing_to_wrap_returns_NotWrapped_and_Rewrite_returns_the_same_expression()
        {
            var result = this.query.WrapIfRequired(SqlQueryOperation.Join);

            Assert.IsFalse(result.Wrapped);
            Assert.AreSame(this.firstName, result.Rewrite(this.firstName));
        }

        [TestMethod]
        public void Where_then_Join_wraps_and_Rewrite_points_at_the_new_outer_data_source()
        {
            var oldAlias = this.query.DataSources.Single().Alias;
            this.query.ApplyWhere(this.Equal(this.firstName, "A"), useOrOperator: false);

            var result = this.query.WrapIfRequired(SqlQueryOperation.Join);

            Assert.IsTrue(result.Wrapped);
            var newAlias = this.query.DataSources.Single().Alias;
            Assert.AreNotEqual(oldAlias, newAlias);

            var rewritten = (SqlDataSourceColumnExpression)result.Rewrite(this.firstName);
            Assert.AreEqual(newAlias, rewritten.DataSourceAlias);
        }

        [TestMethod]
        public void Rewrite_reaches_inside_a_bigger_expression()
        {
            this.query.ApplyWhere(this.Equal(this.firstName, "A"), useOrOperator: false);
            var held = this.factory.CreateBinary(this.firstName, this.factory.CreateLiteral(53), SqlExpressionType.Add);

            var result = this.query.WrapIfRequired(SqlQueryOperation.Join);

            var rewritten = (SqlBinaryExpression)result.Rewrite(held);
            var left = (SqlDataSourceColumnExpression)rewritten.Left;
            Assert.AreEqual(this.query.DataSources.Single().Alias, left.DataSourceAlias);
            Assert.AreSame(held.Right, rewritten.Right);
        }

        [TestMethod]
        public void The_result_can_be_used_after_the_query_changed_again()
        {
            this.query.ApplyWhere(this.Equal(this.firstName, "A"), useOrOperator: false);
            var result = this.query.WrapIfRequired(SqlQueryOperation.Join);
            var newAlias = this.query.DataSources.Single().Alias;

            // The old rule was "use the replacement right after the wrap, nothing in between".
            this.query.ApplyWhere(this.Equal(result.Rewrite(this.firstName), "B"), useOrOperator: false);

            var rewritten = (SqlDataSourceColumnExpression)result.Rewrite(this.firstName);
            Assert.AreEqual(newAlias, rewritten.DataSourceAlias);
        }

        [TestMethod]
        public void RewriteAll_returns_a_new_array_and_leaves_the_input_alone()
        {
            this.query.ApplyWhere(this.Equal(this.firstName, "A"), useOrOperator: false);
            var result = this.query.WrapIfRequired(SqlQueryOperation.Join);
            var input = new SqlExpression[] { this.firstName };

            var output = result.RewriteAll(input);

            Assert.AreNotSame(input, output);
            Assert.AreSame(this.firstName, input[0]);
            Assert.AreNotSame(this.firstName, output[0]);
        }

        private SqlExpression Equal(SqlExpression left, string value)
            => this.factory.CreateBinary(left, this.factory.CreateLiteral(value), SqlExpressionType.Equal);
    }
}
