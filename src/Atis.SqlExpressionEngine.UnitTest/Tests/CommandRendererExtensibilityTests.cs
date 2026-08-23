using System.Data.Common;

using Atis.Orm.Abstractions;
using Atis.Orm.SqlServer;
using Atis.Orm.Translation;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         Pins the provider extension point: a fragment type the base renderer has never heard of, plus
    ///         a pass that knows how to write it.
    ///     </para>
    ///     <para>
    ///         Worth its own test because nothing else in the repository subclasses the renderer, so the
    ///         seam can be closed off - by making the pass unreachable, or by dropping a hook - without any
    ///         other test noticing. The failure would only surface in a provider outside this solution.
    ///     </para>
    /// </summary>
    [TestClass]
    public class CommandRendererExtensibilityTests
    {
        [TestMethod]
        public void A_provider_can_render_a_fragment_type_the_base_renderer_does_not_know()
        {
            var renderer = new HintRenderer(new SqlDbParameterFactory(new SqlDbParameterNameGenerator()));

            var rendered = renderer.Render(
                new ICommandFragment[]
                {
                    new TextCommandFragment("select 1 from t1 "),
                    new TableHintCommandFragment("nolock"),
                },
                p => p.InitialValue);

            Assert.AreEqual("select 1 from t1 WITH (nolock)", rendered.Sql);
        }

        [TestMethod]
        public void The_base_renderer_rejects_a_fragment_type_it_does_not_know()
        {
            // The other half of the contract: unknown fragments are refused rather than silently skipped, so
            // a provider that forgets to override the pass finds out immediately.
            var renderer = new CommandRenderer(new SqlDbParameterFactory(new SqlDbParameterNameGenerator()));

            Assert.ThrowsException<NotSupportedException>(
                () => renderer.Render(new ICommandFragment[] { new TableHintCommandFragment("nolock") }, p => p.InitialValue));
        }

        [TestMethod]
        public void A_render_pass_refuses_to_render_twice()
        {
            // A pass accumulates its output, so reusing one would append the second command to the first.
            var pass = new CommandRenderPass(new SqlDbParameterFactory(new SqlDbParameterNameGenerator()), p => p.InitialValue);
            var fragments = new ICommandFragment[] { new TextCommandFragment("select 1") };

            Assert.AreEqual("select 1", pass.Render(fragments).Sql);
            Assert.ThrowsException<InvalidOperationException>(() => pass.Render(fragments));
        }

        // A provider's own fragment type: structure only, no rendering policy, like every built-in one.
        private sealed class TableHintCommandFragment : ICommandFragment
        {
            public TableHintCommandFragment(string hint)
            {
                this.Hint = hint;
            }

            public string Hint { get; }

            public bool RequirePerExecutionRendering => false;
        }

        // The two classes a provider writes: a pass that knows the new fragment, and a renderer that hands
        // that pass back. Only the renderer is named in DI registration.
        private sealed class HintRenderPass : CommandRenderPass
        {
            public HintRenderPass(IDbParameterFactory dbParameterFactory, Func<IQueryParameter, object> resolveValue)
                : base(dbParameterFactory, resolveValue)
            {
            }

            protected override void RenderFragment(ICommandFragment fragment)
            {
                if (fragment is TableHintCommandFragment hint)
                {
                    this.Sql.Append("WITH (").Append(hint.Hint).Append(")");
                    return;
                }

                base.RenderFragment(fragment);
            }
        }

        private sealed class HintRenderer : CommandRenderer
        {
            public HintRenderer(IDbParameterFactory dbParameterFactory)
                : base(dbParameterFactory)
            {
            }

            protected override CommandRenderPass CreateRenderPass(Func<IQueryParameter, object> resolveValue)
                => new HintRenderPass(this.DbParameterFactory, resolveValue);
        }
    }
}
