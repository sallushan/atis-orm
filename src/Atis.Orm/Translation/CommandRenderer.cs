using System;
using System.Collections.Generic;

using Atis.Orm.Abstractions;

namespace Atis.Orm.Translation
{
    /// <summary>
    ///     <para>
    ///         Default renderer: the service every caller holds, and a factory for the single-use
    ///         <see cref="CommandRenderPass"/> that does the actual walking.
    ///     </para>
    ///     <para>
    ///         Nothing accumulates here. One render produces a command text, a parameter list and a running
    ///         placeholder index, and those belong to that render alone - so they live on a fresh pass rather
    ///         than on this object, which is registered once and shared by every query in the process.
    ///     </para>
    ///     <para>
    ///         <strong>A provider supporting its own <see cref="ICommandFragment"/> type writes two small
    ///         classes:</strong> a <see cref="CommandRenderPass"/> overriding
    ///         <see cref="CommandRenderPass.RenderFragment"/> to handle the types it knows (calling
    ///         <c>base</c> for the rest), and a renderer overriding <see cref="CreateRenderPass"/> to hand
    ///         that pass back. Only the renderer is named in DI registration.
    ///     </para>
    /// </summary>
    public class CommandRenderer : ICommandRenderer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CommandRenderer"/> class.
        /// </summary>
        /// <param name="dbParameterFactory">Creates and names the <see cref="System.Data.Common.DbParameter"/>s.</param>
        /// <exception cref="ArgumentNullException"><paramref name="dbParameterFactory"/> is <c>null</c>.</exception>
        public CommandRenderer(IDbParameterFactory dbParameterFactory)
        {
            this.DbParameterFactory = dbParameterFactory ?? throw new ArgumentNullException(nameof(dbParameterFactory));
        }

        /// <summary>
        ///     Creates and names the parameters. Exposed so an overriding <see cref="CreateRenderPass"/> can
        ///     pass it on to its own pass without taking a second copy through its constructor.
        /// </summary>
        protected IDbParameterFactory DbParameterFactory { get; }

        /// <inheritdoc />
        public RenderedCommand Render(IReadOnlyList<ICommandFragment> fragments, Func<IQueryParameter, object> resolveValue)
        {
            if (fragments is null)
                throw new ArgumentNullException(nameof(fragments));
            if (resolveValue is null)
                throw new ArgumentNullException(nameof(resolveValue));

            return this.CreateRenderPass(resolveValue).Render(fragments);
        }

        /// <summary>
        ///     <para>
        ///         Creates the pass for one render. Override to return a provider's own pass - that is the
        ///         single hook that makes a custom fragment type reachable.
        ///     </para>
        /// </summary>
        /// <param name="resolveValue">Obtains the value bound to a parameter for this execution.</param>
        protected virtual CommandRenderPass CreateRenderPass(Func<IQueryParameter, object> resolveValue)
        {
            return new CommandRenderPass(this.DbParameterFactory, resolveValue);
        }
    }
}
