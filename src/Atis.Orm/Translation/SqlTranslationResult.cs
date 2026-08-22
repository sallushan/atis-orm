using System;
using System.Collections.Generic;

using Atis.Orm.Abstractions;
namespace Atis.Orm.Translation
{
    /// <summary>
    ///     <para>
    ///         What one translation produced: the fragments the statement is built from, the parameters
    ///         recorded while emitting them, and whether the command text depends on the values a given
    ///         execution binds.
    ///     </para>
    /// </summary>
    public class SqlTranslationResult
    {
        /// <summary>
        ///     <para>The parameters recorded during translation, one per placeholder the translator emitted.</para>
        ///     <para>
        ///         A 1:1 placeholder plan only while <see cref="RequiresPerExecutionRendering"/> is <c>false</c>,
        ///         which is the only case that reads it. Once the text depends on values it holds placeholders
        ///         that a given execution may not emit - a dropped optional term's, or the ones in whichever
        ///         branch of a null switch is not taken - so the renderer walks <see cref="Fragments"/> instead
        ///         and binds only what it actually writes.
        ///     </para>
        /// </summary>
        public IReadOnlyList<IQueryParameter> QueryParameters { get; }

        /// <summary>
        ///     <para>
        ///         The positional fragments of the statement. A query whose text is value-dependent is
        ///         re-rendered from these on every execution - because the placeholder count follows a
        ///         collection's length, or because a term appears only for some values.
        ///     </para>
        /// </summary>
        public IReadOnlyList<ICommandFragment> Fragments { get; }

        /// <summary>
        ///     <para>
        ///         Whether the command text depends on this execution's values, so the fragments must be
        ///         re-rendered every time rather than once at compile time. Structural - known during the
        ///         walk, independent of any particular value. Lets the compiler pick a re-rendering vs. a
        ///         render-once compiled query.
        ///     </para>
        /// </summary>
        public bool RequiresPerExecutionRendering { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlTranslationResult"/> class.
        /// </summary>
        /// <param name="queryParameters">The parameters recorded during translation.</param>
        /// <param name="fragments">The positional fragments of the statement.</param>
        /// <param name="requiresPerExecutionRendering">Whether the command text is value-dependent.</param>
        /// <exception cref="ArgumentNullException">Any argument is <c>null</c>.</exception>
        public SqlTranslationResult(IReadOnlyList<IQueryParameter> queryParameters, IReadOnlyList<ICommandFragment> fragments, bool requiresPerExecutionRendering)
        {
            this.QueryParameters = queryParameters ?? throw new ArgumentNullException(nameof(queryParameters));
            this.Fragments = fragments ?? throw new ArgumentNullException(nameof(fragments));
            this.RequiresPerExecutionRendering = requiresPerExecutionRendering;
        }
    }
}
