using Atis.SqlExpressionEngine;
using Atis.SqlExpressionEngine.ExpressionExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Atis.Orm
{
    /// <summary>
    ///     <para>
    ///         Builds the query that reads one keyed row back. Used only by the read-back path on a
    ///         database with no <c>OUTPUT</c> clause, where the values the database generated have to be
    ///         fetched by a query of their own.
    ///     </para>
    ///     <para>
    ///         It is a plain <c>Where</c> over the entity's own root, narrowed by <c>SelectFields</c> to
    ///         the columns actually being read back, so the ordinary query pipeline translates and
    ///         materializes it — no statement shape and no element factory exists here that is not
    ///         already exercised by every other query.
    ///     </para>
    ///     <para>
    ///         The projection is what keeps the second round trip from fetching the whole row to copy two
    ///         values out of it. It also has to be a <c>SelectFields</c> rather than a <c>Select</c> into
    ///         the entity: the column list is the entity's generated columns, which are only known from
    ///         metadata at run time, and there is nothing to write in source to project onto.
    ///     </para>
    /// </summary>
    internal static class SelectEntityMethodCallFactory
    {
        private static readonly EntityFluentApiNames Names = EntityFluentApiNames.ReadBack;

        /// <param name="keys">The primary key columns, and the entity's value for each, that find the row.</param>
        /// <param name="fields">The columns to bring back, as <c>x =&gt; x.Member</c> selectors.</param>
        public static MethodCallExpression CreateKeyedSelectCall<T>(
            IReadOnlyList<FieldValuePair> keys,
            IReadOnlyList<LambdaExpression> fields)
        {
            var whereMethod = new Func<IQueryable<T>, Expression<Func<T, bool>>, IQueryable<T>>(Queryable.Where).Method;
            var keyedRow = Expression.Call(
                null,
                whereMethod,
                new QueryRootExpression(typeof(T)),
                Expression.Quote(EntityLambdaFactory.CreateKeyPredicate<T>(keys, Names)));

            var selectFieldsMethod = new Func<IQueryable<T>, Expression<Func<T, object[]>>, IQueryable<IReadOnlyDictionary<string, object>>>(
                QueryExtensions.SelectFields).Method;
            return Expression.Call(
                null,
                selectFieldsMethod,
                keyedRow,
                Expression.Quote(EntityLambdaFactory.CreateOutputLambda<T>(fields, Names)));
        }
    }
}
