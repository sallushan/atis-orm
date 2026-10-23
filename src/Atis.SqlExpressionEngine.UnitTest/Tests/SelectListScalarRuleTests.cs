using Atis.SqlExpressionEngine.SqlExpressions;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     A select list is a scalar one when exactly one of its columns is part of the projection and that column
    ///     is scalar. Helper columns (added only so that a join condition or a link can reach a value) do not count.
    /// </summary>
    [TestClass]
    public class SelectListScalarRuleTests
    {
        private static SelectColumn Column(string alias, bool scalar = false, bool helper = false)
            => new SelectColumn(new SqlDataSourceColumnExpression(Guid.NewGuid(), alias), alias, scalar, helper);

        [TestMethod]
        public void One_scalar_column_is_a_scalar_select_list()
        {
            var scalar = Column("Col1", scalar: true);

            var list = new SqlSelectListExpression(new[] { scalar });

            Assert.IsTrue(list.IsScalar);
            Assert.AreSame(scalar, list.ScalarSelectColumn);
        }

        [TestMethod]
        public void Helper_columns_next_to_the_scalar_column_do_not_change_that()
        {
            var scalar = Column("Col1", scalar: true);

            var list = new SqlSelectListExpression(new[] { scalar, Column("SubQueryCol1", helper: true), Column("ChildKey1", helper: true) });

            Assert.IsTrue(list.IsScalar);
            Assert.AreSame(scalar, list.ScalarSelectColumn);
        }

        [TestMethod]
        public void Helper_column_in_front_of_the_scalar_column_does_not_change_that_either()
        {
            var scalar = Column("Col1", scalar: true);

            var list = new SqlSelectListExpression(new[] { Column("ChildKey1", helper: true), scalar });

            Assert.AreSame(scalar, list.ScalarSelectColumn);
        }

        [TestMethod]
        public void A_second_column_that_is_part_of_the_projection_makes_it_not_scalar()
        {
            var list = new SqlSelectListExpression(new[] { Column("Col1", scalar: true), Column("Other") });

            Assert.IsFalse(list.IsScalar);
            Assert.IsNull(list.ScalarSelectColumn);
        }

        [TestMethod]
        public void One_projected_column_that_is_not_scalar_is_not_a_scalar_select_list_even_with_helpers()
        {
            var list = new SqlSelectListExpression(new[] { Column("Name"), Column("SubQueryCol1", helper: true) });

            Assert.IsFalse(list.IsScalar);
        }

        [TestMethod]
        public void Only_helper_columns_is_not_a_scalar_select_list()
        {
            var list = new SqlSelectListExpression(new[] { Column("ChildKey1", helper: true) });

            Assert.IsFalse(list.IsScalar);
        }
    }
}
