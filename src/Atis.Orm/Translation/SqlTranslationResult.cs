using System;
using System.Collections.Generic;
using System.Text;

using Atis.Orm.Abstractions;
namespace Atis.Orm.Translation
{
    public class SqlTranslationResult
    {
        /// <summary>
        ///     <para>The parameters recorded during translation, one per placeholder the translator emitted.</para>
        ///     <para>
        ///         A 1:1 placeholder plan only while <see cref="RequiresPerExecutionRendering"/> is <c>false</c>,
        ///         which is the only case that reads it. Once the text depends on values it holds placeholders
        ///         that a given execution may not emit - a dropped optional span's, or the ones in whichever
        ///         branch of a null switch is not taken - so the renderer walks <see cref="Fragments"/> instead
        ///         and binds only what it actually writes.
        ///     </para>
        /// </summary>
        public IReadOnlyList<IQueryParameter> QueryParameters { get; }

        /// <summary>
        ///     <para>
        ///         The positional fragments of the statement. A query holding an expandable parameter is
        ///         re-rendered from these on every execution, because the placeholder count depends on the
        ///         collection's length at that moment.
        ///     </para>
        /// </summary>
        public IReadOnlyList<SqlFragment> Fragments { get; }

        /// <summary>
        ///     <para>
        ///         Whether any emitted parameter can expand into a variable number of placeholders (a
        ///         collection in an <c>IN</c> list, etc.). Structural - known during the walk, independent of
        ///         values. Lets the compiler pick a re-rendering vs. a render-once compiled query.
        ///     </para>
        /// </summary>
        public bool HasExpandableParameters { get; }

        /// <summary>
        ///     <para>
        ///         Whether the statement contains a span that is dropped when its guard has no value (an
        ///         optional WHERE term - see <see cref="SqlConditionalFragment"/>). Structural, like
        ///         <see cref="HasExpandableParameters"/>.
        ///     </para>
        ///     <para>
        ///         Such a statement must be re-rendered per execution for two reasons: the SQL text depends on
        ///         which guards have values, and <see cref="QueryParameters"/> stops being a 1:1 placeholder
        ///         plan once a span can disappear.
        ///     </para>
        /// </summary>
        public bool HasConditionalFragments { get; }

        /// <summary>
        ///     <para>
        ///         Whether the statement contains a comparison spelled two ways, one of which is chosen from
        ///         the value bound at execution time (see <see cref="SqlNullSwitchFragment"/>). Structural,
        ///         like <see cref="HasExpandableParameters"/>.
        ///     </para>
        ///     <para>
        ///         Set only where a null is actually possible: a comparison against a non-nullable value type
        ///         has one spelling for every execution and stays on the render-once path.
        ///     </para>
        /// </summary>
        public bool HasNullSwitchFragments { get; }

        /// <summary>
        ///     Whether the SQL text depends on this execution's values, so the fragments must be re-rendered
        ///     every time rather than once at compile time.
        /// </summary>
        public bool RequiresPerExecutionRendering
            => this.HasExpandableParameters || this.HasConditionalFragments || this.HasNullSwitchFragments;

        public SqlTranslationResult(IReadOnlyList<IQueryParameter> queryParameters, IReadOnlyList<SqlFragment> fragments, bool hasExpandableParameters, bool hasConditionalFragments = false, bool hasNullSwitchFragments = false)
        {
            this.QueryParameters = queryParameters ?? throw new ArgumentNullException(nameof(queryParameters));
            this.Fragments = fragments ?? throw new ArgumentNullException(nameof(fragments));
            this.HasExpandableParameters = hasExpandableParameters;
            this.HasConditionalFragments = hasConditionalFragments;
            this.HasNullSwitchFragments = hasNullSwitchFragments;
        }
    }
}
