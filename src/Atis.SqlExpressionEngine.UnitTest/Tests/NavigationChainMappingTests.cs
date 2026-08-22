using System;
using System.Linq;

using Atis.Orm.Abstractions;
using Atis.Orm.Annotations;
using Atis.Orm.Translation;
using Atis.SqlExpressionEngine;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         On the <c>DataContext</c> path the only entity guaranteed to be in the model is the query
    ///         root: <c>CreateQuery&lt;T&gt;</c> maps <c>T</c> through <c>QueryableFactory</c>, and nothing
    ///         maps anything else. A navigation's target is never mapped — the metadata builder represents
    ///         it as a bare <c>QueryRootExpression(typeof(TJoined))</c> and never asks the model about
    ///         <c>TJoined</c>.
    ///     </para>
    ///     <para>
    ///         So navigating from a mapped root to an entity that was neither configured in
    ///         <c>OnModelCreating</c> nor separately used as a root fails in
    ///         <c>QueryRootExpressionConverter</c> with "Entity metadata for type 'X' not found".
    ///         The existing suite misses this because every entity in an <c>OrmDbContext</c> navigation
    ///         chain is explicitly configured, and the engine-level tests run on a model that builds
    ///         metadata for every type on demand.
    ///     </para>
    ///     <para>
    ///         Each test below owns its entity types. The model is a singleton shared by every
    ///         <c>OrmDbContext</c> in the run, so a type another test happens to query would already be
    ///         mapped and these tests would pass or fail by ordering.
    ///     </para>
    /// </summary>
    [TestClass]
    public class NavigationChainMappingTests
    {
        [TestMethod]
        public void Navigating_to_an_entity_that_was_never_a_query_root_resolves_its_mapping()
        {
            using var db = new OrmDbContext();

            var query = db.CreateQuery<NavChainOrder>()
                          .Where(x => x.NavItem().CategoryId == "C1");

            var sql = TranslateToSql(db, query);

            StringAssert.Contains(sql, nameof(NavChainItem),
                "One level of navigation from a mapped root must resolve the target's mapping. " +
                "NavChainOrder is mapped by CreateQuery, NavChainItem is not mapped by anything, and the " +
                "converter needs it to turn the navigation's QueryRootExpression into a table.");
        }

        [TestMethod]
        public void Navigating_two_levels_resolves_both_targets()
        {
            using var db = new OrmDbContext();

            var query = db.CreateQuery<NavChainRoot2>()
                          .Where(x => x.NavItem().NavCategory().CategoryName == "Books");

            var sql = TranslateToSql(db, query);

            StringAssert.Contains(sql, nameof(NavChainItem2),
                "The first navigation target must be joined.");

            StringAssert.Contains(sql, nameof(NavChainCategory2),
                "The second navigation target must be joined. Reaching it also requires the preprocessor's " +
                "probe to answer for NavChainItem2, which is a separate lookup from the converter's.");
        }

        /// <summary>
        ///     Proof of cause, not of symptom: the identical query succeeds when the navigation target is
        ///     put into the model first. Uses its own types so it cannot pre-map anything the other tests
        ///     depend on being absent.
        /// </summary>
        [TestMethod]
        public void Same_navigation_succeeds_once_the_target_is_in_the_model()
        {
            using var db = new OrmDbContext();

            // The only difference from the first test: this maps NavChainItem3 as a side effect.
            db.CreateQuery<NavChainItem3>();

            var query = db.CreateQuery<NavChainOrder3>()
                          .Where(x => x.NavItem().CategoryId == "C1");

            var sql = TranslateToSql(db, query);

            StringAssert.Contains(sql, nameof(NavChainItem3),
                "With the target already mapped the navigation translates, which isolates the cause to " +
                "the missing mapping rather than to anything about the navigation itself.");
        }

        /// <summary>
        ///     Preprocesses, converts and translates through the context's own scope — the pipeline a real
        ///     execution uses, minus the database.
        /// </summary>
        private static string TranslateToSql(OrmDbContext db, IQueryable query)
        {
            var result = db.GetService<IQueryTranslator>().Translate(query.Expression);
            return string.Concat(result.SqlTranslation
                                       .Fragments
                                       .OfType<TextCommandFragment>()
                                       .Select(x => x.Text));
        }
    }

    // --- one level: root is mapped by CreateQuery, target is mapped by nothing ---

    [DbTable]
    public class NavChainOrder
    {
        [PrimaryKey]
        public string OrderId { get; set; }
        public string ItemId { get; set; }

        [NavigationLink(NavigationType.ToParent, nameof(NavChainItem.ItemId), nameof(NavChainOrder.ItemId))]
        public Func<NavChainItem> NavItem { get; set; }
    }

    [DbTable]
    public class NavChainItem
    {
        [PrimaryKey]
        public string ItemId { get; set; }
        public string CategoryId { get; set; }
    }

    // --- two levels ---

    [DbTable]
    public class NavChainRoot2
    {
        [PrimaryKey]
        public string OrderId { get; set; }
        public string ItemId { get; set; }

        [NavigationLink(NavigationType.ToParent, nameof(NavChainItem2.ItemId), nameof(NavChainRoot2.ItemId))]
        public Func<NavChainItem2> NavItem { get; set; }
    }

    [DbTable]
    public class NavChainItem2
    {
        [PrimaryKey]
        public string ItemId { get; set; }
        public string CategoryId { get; set; }

        [NavigationLink(NavigationType.ToParent, nameof(NavChainCategory2.CategoryId), nameof(NavChainItem2.CategoryId))]
        public Func<NavChainCategory2> NavCategory { get; set; }
    }

    [DbTable]
    public class NavChainCategory2
    {
        [PrimaryKey]
        public string CategoryId { get; set; }
        public string CategoryName { get; set; }
    }

    // --- control: same shape as the one-level case, but the target gets mapped first ---

    [DbTable]
    public class NavChainOrder3
    {
        [PrimaryKey]
        public string OrderId { get; set; }
        public string ItemId { get; set; }

        [NavigationLink(NavigationType.ToParent, nameof(NavChainItem3.ItemId), nameof(NavChainOrder3.ItemId))]
        public Func<NavChainItem3> NavItem { get; set; }
    }

    [DbTable]
    public class NavChainItem3
    {
        [PrimaryKey]
        public string ItemId { get; set; }
        public string CategoryId { get; set; }
    }
}
