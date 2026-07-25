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

        public SqlTranslationResult(IReadOnlyList<IQueryParameter> queryParameters, IReadOnlyList<SqlFragment> fragments, bool hasExpandableParameters)
        {
            this.QueryParameters = queryParameters ?? throw new ArgumentNullException(nameof(queryParameters));
            this.Fragments = fragments ?? throw new ArgumentNullException(nameof(fragments));
            this.HasExpandableParameters = hasExpandableParameters;
        }
    }
}
