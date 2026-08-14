using Atis.SqlExpressionEngine.ExpressionExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Atis.Orm
{
    /// <summary>
    ///     <para>
    ///         Builds the <c>Queryable.Where</c> call that reads one keyed row back. Used only by the
    ///         read-back path on a database with no <c>OUTPUT</c> clause, where the values the database
    ///         generated have to be fetched by a query of their own.
    ///     </para>
    ///     <para>
    ///         It is a plain <c>Where</c> over the entity's own root, so the ordinary query pipeline
    ///         translates and materializes it — no statement shape and no element factory exists here that
    ///         is not already exercised by every other query.
    ///     </para>
    /// </summary>
    internal static class SelectEntityMethodCallFactory
    {
        private static readonly EntityFluentApiNames Names = EntityFluentApiNames.ReadBack;

        public static MethodCallExpression CreateKeyedSelectCall<T>(IReadOnlyList<FieldValuePair> keys)
        {
            var whereMethod = new Func<IQueryable<T>, Expression<Func<T, bool>>, IQueryable<T>>(Queryable.Where).Method;

            return Expression.Call(
                null,
                whereMethod,
                new QueryRootExpression(typeof(T)),
                Expression.Quote(EntityLambdaFactory.CreateKeyPredicate<T>(keys, Names)));
        }
    }
}
