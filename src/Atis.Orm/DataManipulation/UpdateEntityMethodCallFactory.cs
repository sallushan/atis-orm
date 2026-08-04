using Atis.SqlExpressionEngine;
using Atis.SqlExpressionEngine.ExpressionExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Atis.Orm
{
    /// <summary>
    ///     Converts the state collected by the ORM fluent API into the same QueryExtensions.Update
    ///     method-call expression used by the SQL expression engine's query-based API.
    /// </summary>
    internal static class UpdateEntityMethodCallFactory
    {
        private static readonly EntityFluentApiNames Names = EntityFluentApiNames.Update;

        public static MethodCallExpression CreateAffectedRowsCall<T>(IReadOnlyList<FieldValuePair> setters, IReadOnlyList<FieldValuePair> keys)
        {
            var updateMethod = new Func<IQueryable<T>, Expression<Func<T, T>>, Expression<Func<T, bool>>, int>(
                QueryExtensions.Update).Method;

            return Expression.Call(
                null,
                updateMethod,
                new QueryRootExpression(typeof(T)),
                Expression.Quote(CreateSetterLambda<T>(setters)),
                Expression.Quote(CreatePredicateLambda<T>(keys)));
        }

        public static MethodCallExpression CreateOutputCall<T>(IReadOnlyList<FieldValuePair> setters, IReadOnlyList<FieldValuePair> keys, IReadOnlyList<LambdaExpression> outputs)
        {
            var updateMethod = new Func<IQueryable<T>, Expression<Func<T, T>>, Expression<Func<T, bool>>, Expression<Func<T, object[]>>, IReadOnlyList<IReadOnlyDictionary<string, object>>>(
                QueryExtensions.Update).Method;

            return Expression.Call(
                null,
                updateMethod,
                new QueryRootExpression(typeof(T)),
                Expression.Quote(CreateSetterLambda<T>(setters)),
                Expression.Quote(CreatePredicateLambda<T>(keys)),
                Expression.Quote(EntityLambdaFactory.CreateOutputLambda<T>(outputs, Names)));
        }

        private static Expression<Func<T, T>> CreateSetterLambda<T>(IReadOnlyList<FieldValuePair> setters)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            // Update requires Expression<Func<T, T>> even though entity-value setters do not read x.
            return Expression.Lambda<Func<T, T>>(
                EntityLambdaFactory.CreateMemberInit<T>(setters, Names),
                parameter);
        }

        private static Expression<Func<T, bool>> CreatePredicateLambda<T>(IReadOnlyList<FieldValuePair> keys)
        {
            if (keys is null)
                throw new ArgumentNullException(nameof(keys));
            if (keys.Count == 0)
                throw new InvalidOperationException("At least one Key call is required to update an entity.");

            var parameter = Expression.Parameter(typeof(T), "x");
            Expression predicate = null;
            foreach (var key in keys)
            {
                var field = EntityLambdaFactory.ReplaceParameter(key.FieldSelector, parameter);
                var equality = Expression.Equal(field, key.ValueSelector.Body);
                predicate = predicate is null ? equality : Expression.AndAlso(predicate, equality);
            }
            return Expression.Lambda<Func<T, bool>>(predicate, parameter);
        }
    }
}
