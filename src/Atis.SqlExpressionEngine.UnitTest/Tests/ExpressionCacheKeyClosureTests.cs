using Atis.Orm.Services;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     The cache key and the identity used to rebind values on a cache hit must agree on what a captured variable
    ///     is. The identity includes the closure type, so the key has to as well. Otherwise two call sites with the same
    ///     query shape and same-named captured locals, in different closure classes, share one plan, miss the identity
    ///     lookup, and run with the first call site's values.
    /// </summary>
    [TestClass]
    public class ExpressionCacheKeyClosureTests
    {
        private static readonly Queryable<Employee> Employees = new Queryable<Employee>(new QueryProvider());

        private static class SiteA
        {
            public static Expression Build(string name) => Employees.Where(e => e.Name == name).Expression;
        }

        private static class SiteB
        {
            public static Expression Build(string name) => Employees.Where(e => e.Name == name).Expression;
        }

        private static int Key(Expression expression) => (int)new ExpressionCacheKeyProvider().GetCacheKey(expression);

        [TestMethod]
        public void Same_shape_and_same_local_name_in_different_closure_classes_get_different_keys()
        {
            Assert.AreNotEqual(Key(SiteA.Build("x")), Key(SiteB.Build("x")));
        }

        [TestMethod]
        public void The_same_call_site_with_another_value_keeps_its_key()
        {
            Assert.AreEqual(Key(SiteA.Build("x")), Key(SiteA.Build("y")));
        }

        [TestMethod]
        public void Equality_also_tells_closure_classes_apart()
        {
            var comparer = ExpressionEqualityComparer.Instance;
            Assert.IsFalse(comparer.Equals(SiteA.Build("x"), SiteB.Build("x")));
            Assert.IsTrue(comparer.Equals(SiteA.Build("x"), SiteA.Build("y")));
        }
    }
}
