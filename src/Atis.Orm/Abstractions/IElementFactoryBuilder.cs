using Atis.Orm.Querying;
using Atis.SqlExpressionEngine.Internal;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;

namespace Atis.Orm.Abstractions
{
    public interface IElementFactoryBuilder
    {
        Func<IDataReader, object> CreateElementFactory(Expression expression, SqlExpression sqlExpression);

        /// <summary>
        ///     <para>
        ///         Creates the element factory of a query that has child collections, i.e. sub-queries selected in the
        ///         projection that are loaded by queries of their own. Each parent row is read together with the
        ///         <see cref="ChildCollections"/> of the execution, which supplies the collection of every child
        ///         member before the parent object is constructed.
        ///     </para>
        ///     <para>
        ///         A child member whose type is not a list or an array (<c>IQueryable&lt;T&gt;</c> for one) is refused
        ///         with an error that says what to write instead.
        ///     </para>
        /// </summary>
        /// <param name="expression">The query expression, which gives the element type.</param>
        /// <param name="parentQuery">The query for the main rows, see <see cref="ChildQueryExtractionResult.ParentQuery"/>.</param>
        /// <param name="children">The child queries, see <see cref="ChildQueryExtractionResult.ChildQueries"/>.</param>
        ChildAwareElementFactory CreateChildAwareElementFactory(Expression expression, SqlDerivedTableExpression parentQuery, IReadOnlyList<ChildQuery> children);
    }

    /// <summary>
    ///     <para>
    ///         What <see cref="IElementFactoryBuilder.CreateChildAwareElementFactory"/> produces.
    ///     </para>
    /// </summary>
    public sealed class ChildAwareElementFactory
    {
        /// <summary>Initializes a new instance of the <see cref="ChildAwareElementFactory"/> class.</summary>
        public ChildAwareElementFactory(Func<IDataReader, ChildCollections, object> parentFactory, IReadOnlyList<Func<IDataReader, object>> childRowFactories)
        {
            this.ParentFactory = parentFactory ?? throw new ArgumentNullException(nameof(parentFactory));
            this.ChildRowFactories = childRowFactories ?? throw new ArgumentNullException(nameof(childRowFactories));
        }

        /// <summary>Reads one row of the parent query.</summary>
        public Func<IDataReader, ChildCollections, object> ParentFactory { get; }

        /// <summary>
        ///     Reads one row of the child query at the same index as a <see cref="ChildRow"/>. <c>null</c> for a child query
        ///     whose sub-query is not part of the projected result, which therefore does not have to be run.
        /// </summary>
        public IReadOnlyList<Func<IDataReader, object>> ChildRowFactories { get; }
    }
}
