using Atis.Orm.Abstractions;
using Atis.Orm.Services;
using Atis.SqlExpressionEngine.Services;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

using Model = Atis.SqlExpressionEngine.UnitTest.Services.Model;
using ReflectionService = Atis.SqlExpressionEngine.Services.ReflectionService;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     Covers the finalized query-execution cache: the variable-only
    ///     <see cref="ExpressionVariableValuesExtractor"/> and the
    ///     <see cref="PreprocessingRequirementTester"/> that lets cache hits skip preprocessing.
    /// </summary>
    [TestClass]
    public class PreprocessingCacheTests : TestBase
    {
        private static ExpressionVariableValuesExtractor NewExtractor()
            => new ExpressionVariableValuesExtractor(new ExpressionEvaluator(), new VariableIdentityProvider());

        private static PreprocessingRequirementTester NewTester()
            => new PreprocessingRequirementTester(NewExtractor());

        [TestMethod]
        public void Extractor_collects_only_variables_not_constants()
        {
            var invId = "INV-1";
            // invId is a captured variable (closure member) -> parameter.
            // "ACME" is an inline constant -> literal, must NOT be collected.
            var expr = queryProvider.DataSet<Invoice>()
                                    .Where(x => x.InvoiceId == invId && x.Description == "ACME")
                                    .Expression;

            var nodes = NewExtractor().ExtractParameterNodes(expr);
            var values = NewExtractor().ExtractVariableValues(expr);

            Assert.AreEqual(1, nodes.Count, "Only the captured variable should be collected, not the inline constant.");
            CollectionAssert.AreEqual(new object[] { "INV-1" }, values.ToArray());
        }

        [TestMethod]
        public void Extractor_excludes_query_typed_members()
        {
            // dataContext.Invoices is a variable member access, but of query type (IQueryable) -> a source
            // root, not a parameter. It (and its container) must not be collected.
            Expression<System.Func<Invoice, bool>> pred =
                x => dataContext.Invoices.Any(y => y.InvoiceId == x.InvoiceId);

            var nodes = NewExtractor().ExtractParameterNodes(pred);

            Assert.AreEqual(0, nodes.Count, "Query-typed members and their container must be excluded.");
        }

        [TestMethod]
        public void Extractor_reextracts_current_variable_values()
        {
            var invId = "INV-1";
            var expr = queryProvider.DataSet<Invoice>().Where(x => x.InvoiceId == invId).Expression;

            var first = NewExtractor().ExtractVariableValues(expr);
            CollectionAssert.AreEqual(new object[] { "INV-1" }, first.ToArray());

            // Changing the captured variable and re-extracting from the same tree must reflect the new value
            // (this is the cache-hit rebinding contract).
            invId = "INV-2";
            var second = NewExtractor().ExtractVariableValues(expr);
            CollectionAssert.AreEqual(new object[] { "INV-2" }, second.ToArray());
        }

        [TestMethod]
        public void Tester_returns_false_for_simple_parameterized_query()
        {
            var invId = "INV-1";
            var original = queryProvider.DataSet<Invoice>().Where(x => x.InvoiceId == invId).Expression;
            var preprocessed = PreprocessExpression(original, new Model(new ReflectionService()));

            // Root replacement rewrites the source but leaves the captured-variable node untouched, so
            // preprocessing can be skipped on a cache hit.
            Assert.IsFalse(NewTester().IsPreprocessingRequired(original, preprocessed));
        }

        [TestMethod]
        public void Tester_returns_true_when_variable_nodes_differ()
        {
            var a = "INV-1";
            var b = "INV-2";
            // Two independently built trees -> different captured-variable member node instances.
            var left = queryProvider.DataSet<Invoice>().Where(x => x.InvoiceId == a).Expression;
            var right = queryProvider.DataSet<Invoice>().Where(x => x.InvoiceId == b).Expression;

            Assert.IsTrue(NewTester().IsPreprocessingRequired(left, right));
        }

        [TestMethod]
        public void Tester_calculated_property_injected_constant_does_not_force_true()
        {
            decimal? total = 100m;
            // CalcInvoiceTotal expands (via preprocessing) into a NavLines.Sum(...) subquery, potentially
            // injecting constants, but no new *variable*. The captured 'total' node survives, so the tester
            // must still allow skipping preprocessing.
            var original = queryProvider.DataSet<Invoice>().Where(x => x.CalcInvoiceTotal > total).Expression;
            var preprocessed = PreprocessExpression(original, new Model(new ReflectionService()));

            Assert.IsFalse(NewTester().IsPreprocessingRequired(original, preprocessed),
                "An injected constant is a literal and must not force re-preprocessing.");
        }

        // --- End-to-end cache-hit tests (require the test SQL Server) -------------------------------------

        [TestMethod]
        public async Task Cache_hit_rebinds_new_variable_value()
        {
            var setup = new TestDatabaseSetup("Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True");
            await setup.SetupAsync();

            using var dbc = new OrmDbContext();
            var employees = dbc.CreateQuery<TestEntities.Employee>();

            // First run: cache miss -> compiles & caches, binds @p0 from the current value (1 -> 'John').
            int id = 1;
            var firstRun = employees.Where(x => x.EmployeeId == id).Select(x => x.FirstName).ToList();
            CollectionAssert.AreEqual(new[] { "John" }, firstRun);

            // Second run: same query shape, different captured value -> cache HIT; the finalized path must
            // re-extract the variable and rebind @p0 (2 -> 'Sarah'), not replay the whole pipeline.
            id = 2;
            var secondRun = employees.Where(x => x.EmployeeId == id).Select(x => x.FirstName).ToList();
            CollectionAssert.AreEqual(new[] { "Sarah" }, secondRun);
        }

        [TestMethod]
        public async Task Cache_hit_rebinds_projection_and_where_variables_by_identity()
        {
            var setup = new TestDatabaseSetup("Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True");
            await setup.SetupAsync();

            using var dbc = new OrmDbContext();
            var employees = dbc.CreateQuery<TestEntities.Employee>();

            // Two same-typed (int) variables: 'tag' in the projection (SELECT list) and 'minId' in the WHERE.
            // SQL emits SELECT before WHERE, so the translator produces parameters in order [tag, minId].
            // A LINQ ExpressionVisitor over Select(Where(src, ...), ...) visits the inner Where first, so the
            // value re-extractor produces [minId, tag] — the reverse. Positional cache-hit binding therefore
            // swaps the two on the second run. Identity-based binding must keep them correct.
            int minId = 1;
            int tag = 100;
            var firstRun = employees
                .Where(x => x.EmployeeId >= minId)
                .Select(x => new { Tag = tag, x.EmployeeId })
                .ToList();
            Assert.AreEqual(25, firstRun.Count, "All 25 employees have EmployeeId >= 1.");
            Assert.IsTrue(firstRun.All(r => r.Tag == 100), "First run projects Tag = 100 (InitialValue).");

            // Cache hit: same shape, new captured values. Buggy positional binding would send 'tag' (200) to
            // the WHERE (EmployeeId >= 200 -> 0 rows) and 'minId' (24) to the Tag column.
            minId = 24;
            tag = 200;
            var secondRun = employees
                .Where(x => x.EmployeeId >= minId)
                .Select(x => new { Tag = tag, x.EmployeeId })
                .ToList();

            Assert.AreEqual(2, secondRun.Count, "Only EmployeeId 24 and 25 satisfy EmployeeId >= 24.");
            Assert.IsTrue(secondRun.All(r => r.Tag == 200), "Second run must rebind the projected Tag to 200.");
        }

        [TestMethod]
        public async Task Cache_hit_with_literal_and_variable_mix()
        {
            var setup = new TestDatabaseSetup("Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True");
            await setup.SetupAsync();

            using var dbc = new OrmDbContext();
            var employees = dbc.CreateQuery<TestEntities.Employee>();

            // Take(3) is a literal parameter; the id predicate is a variable. On a cache hit the literal keeps
            // its InitialValue and must not consume a value slot (running variable-index binding).
            int id = 1;
            var firstRun = employees.Where(x => x.EmployeeId >= id).Take(3).ToList();
            Assert.AreEqual(3, firstRun.Count, "25 employees have EmployeeId >= 1, capped at 3 by Take.");

            id = 24;
            var secondRun = employees.Where(x => x.EmployeeId >= id).Take(3).ToList();
            Assert.AreEqual(2, secondRun.Count, "Only EmployeeId 24 and 25 match on the cached re-run.");
        }
    }
}
