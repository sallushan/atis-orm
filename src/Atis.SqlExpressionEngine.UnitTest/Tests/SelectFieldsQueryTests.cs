using System;
using System.Collections.Generic;
using System.Linq;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         Translation of <see cref="QueryExtensions.SelectFields{T}"/>, which projects a column
    ///         list that is only known at run time onto a dictionary per row. Asserts generated SQL and
    ///         needs no database; <see cref="SelectFieldsExecutionTests"/> is the counterpart that runs.
    ///     </para>
    ///     <para>
    ///         The reason this exists at all is the select list, so most of what is worth asserting is
    ///         which columns come out and what they are aliased as.
    ///     </para>
    /// </summary>
    [TestClass]
    public class SelectFieldsQueryTests : TestBase
    {
        /// <summary>
        ///     The point of the whole feature: two columns asked for, two columns selected, and the
        ///     other three mapped columns of <c>Person</c> nowhere in the statement.
        /// </summary>
        [TestMethod]
        public void Selects_only_the_named_fields()
        {
            var people = new Queryable<Person>(this.queryProvider);

            var q = people.Where(x => x.Id == 1)
                          .SelectFields(x => new object[] { x.FirstName, x.Age });

            string expectedResult = @"
(
	select a_1.FRST_NM as FirstName, a_1.AGE as Age
	from dbo.Person as a_1
	where (a_1.ID = 1)
)
";
            Test("SelectFields narrows the select list", q.Expression, expectedResult);
        }

        /// <summary>
        ///     <para>
        ///         The alias is the <em>member</em> name, never the column name — <c>FirstName</c>, not
        ///         <c>FRST_NM</c>. That is what makes the row readable as
        ///         <c>row[nameof(Person.FirstName)]</c> however the column is spelled in the database,
        ///         and it is why the API is called SelectFields rather than SelectColumns.
        ///     </para>
        ///     <para>
        ///         <c>Person</c> earns its keep here: every one of its columns is named differently from
        ///         its property, so an implementation that aliased by column name could not pass.
        ///     </para>
        /// </summary>
        [TestMethod]
        public void Aliases_by_member_name_not_column_name()
        {
            var people = new Queryable<Person>(this.queryProvider);

            var q = people.SelectFields(x => new object[] { x.Id, x.LastName, x.MiddleInitial });

            string expectedResult = @"
(
	select a_1.ID as Id, a_1.LAST_NM as LastName, a_1.MID_INIT as MiddleInitial
	from dbo.Person as a_1
)
";
            Test("SelectFields aliases by member name", q.Expression, expectedResult);
        }

        /// <summary>
        ///     A projection is already in place, so there is no select list left to narrow. The
        ///     ordinary wrapping rule applies and the fields are selected from a subquery, rather than
        ///     the second projection being rejected.
        /// </summary>
        [TestMethod]
        public void Wraps_a_query_that_is_already_projected()
        {
            var people = new Queryable<Person>(this.queryProvider);

            var q = people.Select(x => new { x.FirstName, x.Age })
                          .SelectFields(x => new object[] { x.Age });

            string expectedResult = @"
(
	select a_2.Age as Age
	from (
			select a_1.FRST_NM as FirstName, a_1.AGE as Age
			from dbo.Person as a_1
		) as a_2
)
";
            Test("SelectFields after a projection", q.Expression, expectedResult);
        }

        // ---------------------------------------------------------------------------------------
        // Rejections
        // ---------------------------------------------------------------------------------------

        /// <summary>
        ///     A computed value has no member to name the dictionary key by. Rejected rather than given
        ///     an invented name, because an invented name is a contract as soon as anyone reads it.
        /// </summary>
        [TestMethod]
        public void Rejects_a_field_that_is_not_a_member()
        {
            var people = new Queryable<Person>(this.queryProvider);

            var q = people.SelectFields(x => new object[] { x.Age * 2 });

            var thrown = Assert.ThrowsException<InvalidOperationException>(
                () => ConvertExpressionToSqlExpression(q.Expression, out _));

            StringAssert.Contains(thrown.Message, nameof(QueryExtensions.SelectFields));
            StringAssert.Contains(thrown.Message, "must select a member");
        }

        [TestMethod]
        public void Rejects_an_empty_field_list()
        {
            var people = new Queryable<Person>(this.queryProvider);

            var q = people.SelectFields(x => new object[] { });

            var thrown = Assert.ThrowsException<InvalidOperationException>(
                () => ConvertExpressionToSqlExpression(q.Expression, out _));

            StringAssert.Contains(thrown.Message, "at least one field");
        }

        /// <summary>
        ///     Two fields claiming one key. Letting it through would silently drop a column, since a
        ///     dictionary keeps only the last value written for a key.
        /// </summary>
        [TestMethod]
        public void Rejects_two_fields_that_alias_to_the_same_name()
        {
            var people = new Queryable<Person>(this.queryProvider);

            var q = people.SelectFields(x => new object[] { x.FirstName, x.FirstName });

            Assert.ThrowsException<ArgumentException>(
                () => ConvertExpressionToSqlExpression(q.Expression, out _));
        }

        [TestMethod]
        public void Rejects_a_null_field_selector()
        {
            var people = new Queryable<Person>(this.queryProvider);

            Assert.ThrowsException<ArgumentNullException>(() => people.SelectFields(null));
        }

        /// <summary>
        ///     <para>
        ///         The submitted expression's type is what tells the ORM to materialize dictionaries:
        ///         <c>ElementFactoryBuilder</c> reads it, unwraps the <c>IQueryable&lt;&gt;</c> and asks
        ///         whether a dictionary row satisfies the element type. So it is part of the contract
        ///         rather than an implementation detail.
        ///     </para>
        /// </summary>
        [TestMethod]
        public void Submits_an_expression_typed_as_a_query_of_dictionaries()
        {
            var people = new Queryable<Person>(this.queryProvider);

            var q = people.SelectFields(x => new object[] { x.Id });

            Assert.AreEqual(typeof(IQueryable<IReadOnlyDictionary<string, object>>), q.Expression.Type);
        }
    }
}
