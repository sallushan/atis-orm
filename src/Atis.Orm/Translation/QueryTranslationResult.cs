using Atis.SqlExpressionEngine.Internal;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Atis.Orm.Translation
{
    public class QueryTranslationResult
    {
        public QueryTranslationResult(SqlExpression sqlExpression, Expression preprocessedExpression, SqlTranslationResult translationResult)
            : this(sqlExpression, preprocessedExpression, translationResult, children: null)
        {
        }

        public QueryTranslationResult(SqlExpression sqlExpression, Expression preprocessedExpression, SqlTranslationResult translationResult, IReadOnlyList<ChildQueryTranslation> children)
        {
            this.SqlExpression = sqlExpression;
            this.PreprocessedExpression = preprocessedExpression;
            this.SqlTranslation = translationResult;
            this.Children = children ?? Array.Empty<ChildQueryTranslation>();
        }

        /// <summary>
        ///     The query to run for the main rows. When the query has child collections this is the query with the
        ///     sub-query columns taken out, see <see cref="ChildQueryExtractionResult.ParentQuery"/>.
        /// </summary>
        public SqlExpression SqlExpression { get; }
        public Expression PreprocessedExpression { get; }
        public SqlTranslationResult SqlTranslation { get; }

        /// <summary>
        ///     The child collections of the query, each translated on its own. Empty for a query that has none.
        /// </summary>
        public IReadOnlyList<ChildQueryTranslation> Children { get; }
    }

    /// <summary>
    ///     <para>
    ///         A child query along with its translation. It is an independent statement: its parameters are numbered
    ///         from the start and are rebound from the same expression as the parent's.
    ///     </para>
    /// </summary>
    public class ChildQueryTranslation
    {
        /// <summary>Initializes a new instance of the <see cref="ChildQueryTranslation"/> class.</summary>
        public ChildQueryTranslation(ChildQuery childQuery, SqlTranslationResult translation)
        {
            this.ChildQuery = childQuery ?? throw new ArgumentNullException(nameof(childQuery));
            this.Translation = translation ?? throw new ArgumentNullException(nameof(translation));
        }

        /// <summary>The child query.</summary>
        public ChildQuery ChildQuery { get; }

        /// <summary>The translation of <see cref="Atis.SqlExpressionEngine.Internal.ChildQuery.Query"/>.</summary>
        public SqlTranslationResult Translation { get; }
    }
}
