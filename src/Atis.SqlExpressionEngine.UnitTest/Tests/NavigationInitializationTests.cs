using System;
using System.Collections.Generic;
using System.Linq;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     Phase-1 lazy navigation initialization: a materialized entity's <c>IQueryable&lt;&gt;</c> and
    ///     <c>Func&lt;&gt;</c> navigation properties are set to lazily-executed queries / delegates, while
    ///     plain single-reference navigations are left untouched. These tests run DB-free: they
    ///     initialize a manually-created entity and assert the navigation properties / their translated
    ///     SQL.
    /// </summary>
    [TestClass]
    public class NavigationInitializationTests : TestBase
    {
        [TestMethod]
        public void Lazy_IQueryable_children_navigation_is_set_and_translates_with_parameterized_key()
        {
            using var dbc = new OrmDbContext();
            // touch the model so OnModelCreating runs and FluentAuthor/FluentBook are registered
            dbc.CreateQuery<FluentAuthor>();

            var author = new FluentAuthor { Id = 42 };
            dbc.GetNavigationInitializer().Initialize(author);

            Assert.IsNotNull(author.Books, "IQueryable<> children navigation should be initialized to a lazy query");

            var sql = dbc.TranslateToSql(author.Books);
            Console.WriteLine(sql);
            // JoinCondition is (parent, child) => parent.Id == child.AuthorId; the parent (author) is
            // bound as a constant so its Id flows in as a parameter (NOT inlined as a column).
            string expected = @"
SELECT t1.Id AS Id, t1.BOOK_TITLE AS Title, t1.AuthorId AS AuthorId, t1.Year AS Year
FROM BOOK AS t1
WHERE (@p0 = t1.AuthorId)
";
            ValidateQueryResults(sql, expected);
        }

        [TestMethod]
        public void Lazy_CompositeKey_children_navigation_translates_with_all_key_parameters()
        {
            using var dbc = new OrmDbContext();
            dbc.CreateQuery<FluentCompany>();

            var company = new FluentCompany { CompanyId = 7, DivisionId = 3 };
            dbc.GetNavigationInitializer().Initialize(company);

            Assert.IsNotNull(company.Employees, "Composite-key children navigation should be initialized");

            var sql = dbc.TranslateToSql(company.Employees);
            Console.WriteLine(sql);
            string expected = @"
SELECT t1.CompanyId AS CompanyId, t1.DivisionId AS DivisionId, t1.EmployeeId AS EmployeeId, t1.EmployeeName AS EmployeeName
FROM EMPLOYEE AS t1
WHERE ((@p0 = t1.CompanyId) AND (@p1 = t1.DivisionId))
";
            ValidateQueryResults(sql, expected);
        }

        [TestMethod]
        public void Lazy_single_reference_navigations_are_left_null_in_phase1()
        {
            using var dbc = new OrmDbContext();
            dbc.CreateQuery<FluentAuthor>();

            var author = new FluentAuthor { Id = 1 };
            dbc.GetNavigationInitializer().Initialize(author);

            // plain single-reference navigations (and the HasOneRow single-row reference) need eager
            // loading / proxies and are out of phase-1 scope.
            Assert.IsNull(author.PrimaryBook, "single-reference child navigation should be left null");
            Assert.IsNull(author.Country, "single-reference parent navigation should be left null");
            Assert.IsNull(author.LatestBook, "HasOneRow single-reference navigation should be left null");
        }

        [TestMethod]
        public void Lazy_init_is_noop_for_null_and_non_entity_shapes()
        {
            using var dbc = new OrmDbContext();
            dbc.CreateQuery<FluentAuthor>();
            var initializer = dbc.GetNavigationInitializer();

            // null and non-mapped (anonymous / scalar) shapes must be ignored without throwing.
            initializer.Initialize(null);
            initializer.Initialize(new { Id = 1, Name = "x" });
            initializer.Initialize(42);
        }

        [TestMethod]
        public void Lazy_Func_parent_navigation_is_set_as_delegate()
        {
            using var dbc = new OrmDbContext();
            // Employee uses attribute-based navigations including Func<> shapes.
            dbc.CreateQuery<Employee>();

            var employee = new Employee { EmployeeId = "E1", ManagerId = "M1" };
            dbc.GetNavigationInitializer().Initialize(employee);

            // Func<Employee> (ToParent) -> compiled delegate
            Assert.IsNotNull(employee.NavManager, "Func<> parent navigation should be initialized to a delegate");
            // IQueryable<> children navigations -> lazy queries
            Assert.IsNotNull(employee.NavDegrees, "IQueryable<> children navigation should be initialized");
            Assert.IsNotNull(employee.NavSubOrdinates, "IQueryable<> self-referencing children navigation should be initialized");
        }
    }
}
