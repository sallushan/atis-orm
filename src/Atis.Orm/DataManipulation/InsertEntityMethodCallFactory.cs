using Atis.SqlExpressionEngine;
using Atis.SqlExpressionEngine.ExpressionExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Atis.Orm
{
    /// <summary>Builds the standard QueryExtensions.Insert calls submitted by the fluent API.</summary>
    internal static class InsertEntityMethodCallFactory
    {
        private static readonly EntityFluentApiNames Names = EntityFluentApiNames.Insert;

        public static MethodCallExpression CreateAffectedRowsCall<T>(IReadOnlyList<FieldValuePair> values)
        {
            var insertMethod = new Func<IQueryable<T>, Expression<Func<T>>, int>(QueryExtensions.Insert).Method;
            return Expression.Call(
                null,
                insertMethod,
                new QueryRootExpression(typeof(T)),
                Expression.Quote(CreateInsertLambda<T>(values)));
        }

        public static MethodCallExpression CreateOutputCall<T>(
            IReadOnlyList<FieldValuePair> values,
            IReadOnlyList<LambdaExpression> outputs)
        {
            var insertMethod = new Func<IQueryable<T>, Expression<Func<T>>, Expression<Func<T, object[]>>, IReadOnlyList<IReadOnlyDictionary<string, object>>>(
                QueryExtensions.Insert).Method;
            return Expression.Call(
                null,
                insertMethod,
                new QueryRootExpression(typeof(T)),
                Expression.Quote(CreateInsertLambda<T>(values)),
                Expression.Quote(EntityLambdaFactory.CreateOutputLambda<T>(outputs, Names)));
        }

        /// <summary>
        ///     Unlike the update setter lambda, this one takes no parameter: an insert has no existing
        ///     row to read from, so <c>Insert</c> accepts <c>Expression&lt;Func&lt;T&gt;&gt;</c>.
        /// </summary>
        private static Expression<Func<T>> CreateInsertLambda<T>(IReadOnlyList<FieldValuePair> values)
        {
            return Expression.Lambda<Func<T>>(EntityLambdaFactory.CreateMemberInit<T>(values, Names));
        }
    }
}
