using Atis.SqlExpressionEngine;
using Atis.SqlExpressionEngine.ExpressionExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Atis.Orm
{
    /// <summary>
    ///     Builds the standard QueryExtensions.Delete call submitted by the fluent API. There is no
    ///     output counterpart to the insert and update factories: a delete leaves no row image behind
    ///     for the engine to return.
    /// </summary>
    internal static class DeleteEntityMethodCallFactory
    {
        private static readonly EntityFluentApiNames Names = EntityFluentApiNames.Delete;

        public static MethodCallExpression CreateAffectedRowsCall<T>(IReadOnlyList<FieldValuePair> keys)
        {
            var deleteMethod = new Func<IQueryable<T>, Expression<Func<T, bool>>, int>(
                QueryExtensions.Delete).Method;

            return Expression.Call(
                null,
                deleteMethod,
                new QueryRootExpression(typeof(T)),
                Expression.Quote(EntityLambdaFactory.CreateKeyPredicate<T>(keys, Names)));
        }
    }
}
