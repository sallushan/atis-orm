using System;
using System.Collections.Generic;
using System.Text;

using Atis.Orm.Abstractions;
namespace Atis.Orm.Translation
{
    public class SqlTranslationResult
    {
        /// <summary>The parameters recorded during translation, one per placeholder the translator emitted.</summary>
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
        ///     Whether the SQL text depends on this execution's values, so the fragments must be re-rendered
        ///     every time rather than once at compile time.
        /// </summary>
        public bool RequiresPerExecutionRendering => this.HasExpandableParameters || this.HasConditionalFragments;

        public SqlTranslationResult(IReadOnlyList<IQueryParameter> queryParameters, IReadOnlyList<SqlFragment> fragments, bool hasExpandableParameters, bool hasConditionalFragments = false)
        {
            this.QueryParameters = queryParameters ?? throw new ArgumentNullException(nameof(queryParameters));
            this.Fragments = fragments ?? throw new ArgumentNullException(nameof(fragments));
            this.HasExpandableParameters = hasExpandableParameters;
            this.HasConditionalFragments = hasConditionalFragments;
        }
    }
}
