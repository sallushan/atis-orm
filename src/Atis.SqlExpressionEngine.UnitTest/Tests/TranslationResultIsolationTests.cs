using System.Linq.Expressions;
using Atis.Orm.Translation;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         A <c>SqlTranslationResult</c> outlives the translation that produced it: a compiled query
    ///         holds its parameter plan for as long as it stays in the compiled-query cache. The
    ///         translator is a singleton that reuses one list across translations, so the result must
    ///         own a copy — otherwise translating anything else rewrites the parameter plan of every
    ///         query already compiled.
    ///     </para>
    /// </summary>
    [TestClass]
    public class TranslationResultIsolationTests : TestBase
    {
        [TestMethod]
        public void A_later_translation_does_not_rewrite_an_earlier_results_parameter_plan()
        {
            var assets = new Queryable<Asset>(this.queryProvider);
            var serial = "S1";
            var itemId = "I1";

            // Captured locals become parameters; inline constants would be literals and prove nothing.
            Expression<Func<IQueryable<Asset>>> twoParameters =
                () => assets.Where(x => x.SerialNumber == serial && x.ItemId == itemId);
            Expression<Func<IQueryable<Asset>>> oneParameter =
                () => assets.Where(x => x.SerialNumber == serial);

            var translator = new SqlExpressionTranslatorBase();

            var first = translator.Translate(ConvertExpressionToSqlExpression(twoParameters.Body, out _));
            Assert.AreEqual(2, first.QueryParameters.Count,
                "Guard: the first query must contribute two parameters for this test to mean anything.");

            translator.Translate(ConvertExpressionToSqlExpression(oneParameter.Body, out _));

            Assert.AreEqual(2, first.QueryParameters.Count,
                "The first result's parameter plan must survive a later translation. When it did not, a " +
                "cached two-parameter query executed with one parameter and the server rejected it with " +
                "'Must declare the scalar variable \"@p1\"'.");
            Assert.AreEqual("S1", first.QueryParameters[0].InitialValue);
            Assert.AreEqual("I1", first.QueryParameters[1].InitialValue);
        }

        /// <summary>
        ///     <para>
        ///         The translator accumulates state per call, so two contexts must never share one.
        ///         It used to be registered as a singleton while its only consumer,
        ///         <c>IQueryTranslator</c>, was scoped.
        ///     </para>
        /// </summary>
        [TestMethod]
        public void Each_context_gets_its_own_sql_expression_translator()
        {
            using var first = new OrmDbContext();
            using var second = new OrmDbContext();

            Assert.AreNotSame(
                first.GetService<Atis.Orm.Abstractions.ISqlExpressionTranslator>(),
                second.GetService<Atis.Orm.Abstractions.ISqlExpressionTranslator>(),
                "A stateful translator shared across contexts is corrupted by concurrent translation.");

            // Control: the change must not have demoted genuine singletons. The model in particular is
            // shared by design — OnModelCreating runs once per root provider.
            Assert.AreSame(
                first.GetService<Atis.Orm.Abstractions.IOrmModel>(),
                second.GetService<Atis.Orm.Abstractions.IOrmModel>(),
                "The model is a singleton and must stay shared between contexts.");
        }

        /// <summary>
        ///     <para>
        ///         <c>Translate</c> is the entry point for a whole statement and clears every piece of
        ///         per-statement state, so calling it from inside a <c>Translate*</c> method wipes the
        ///         translation in progress. The failure is silent - aliases renumber, parameters vanish from
        ///         the plan, and a statement still comes out - so re-entry is refused instead.
        ///     </para>
        /// </summary>
        [TestMethod]
        public void Calling_Translate_recursively_is_rejected()
        {
            var assets = new Queryable<Asset>(this.queryProvider);
            var serial = "S1";
            Expression<Func<IQueryable<Asset>>> query = () => assets.Where(x => x.SerialNumber == serial);
            var sqlExpression = ConvertExpressionToSqlExpression(query.Body, out _);

            var ex = Assert.ThrowsException<InvalidOperationException>(
                () => new RecursivelyTranslatingTranslator().Translate(sqlExpression));

            StringAssert.Contains(ex.Message, "while a translation was already in progress");
            // The message has to name the method that was actually meant, or the reader is left guessing.
            StringAssert.Contains(ex.Message, "TranslateExpression");
        }

        /// <summary>A derived translator making the mistake the guard exists to catch.</summary>
        private sealed class RecursivelyTranslatingTranslator : SqlExpressionTranslatorBase
        {
            protected override void TranslateTable(Atis.SqlExpressionEngine.SqlExpressions.SqlTableExpression node)
                => this.Translate(node);
        }
    }
}
