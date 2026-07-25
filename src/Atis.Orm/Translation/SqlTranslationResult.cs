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


        public SqlTranslationResult(IReadOnlyList<IQueryParameter> queryParameters, IReadOnlyList<SqlFragment> fragments)
        {
            this.QueryParameters = queryParameters ?? throw new ArgumentNullException(nameof(queryParameters));
            this.Fragments = fragments ?? throw new ArgumentNullException(nameof(fragments));
        }
    }
}
