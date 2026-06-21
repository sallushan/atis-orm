using Atis.SqlExpressionEngine.ExpressionExtensions;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Atis.Orm
{
    /// <summary>
    ///     <para>
    ///         Builds the <c>JoinedSource</c> lambda for a navigation, of the form
    ///         <c>(thisEntity) =&gt; IQueryable&lt;target&gt;</c> whose body is a
    ///         <see cref="QueryRootExpression"/> over the target type.
    ///     </para>
    /// </summary>
    internal static class NavigationDataSourceFactory
    {
        /// <summary>
        ///     Creates a <c>Func&lt;thisType, IQueryable&lt;targetType&gt;&gt;</c> lambda whose body is a
        ///     <see cref="QueryRootExpression"/> for <paramref name="targetType"/>.
        /// </summary>
        /// <param name="thisType">The type of the entity declaring the navigation.</param>
        /// <param name="targetType">The related entity type.</param>
        /// <returns>The joined data source lambda expression.</returns>
        public static LambdaExpression Create(Type thisType, Type targetType)
        {
            if (thisType == null) throw new ArgumentNullException(nameof(thisType));
            if (targetType == null) throw new ArgumentNullException(nameof(targetType));

            var parameter = Expression.Parameter(thisType, "src");
            var body = new QueryRootExpression(targetType);
            var delegateType = typeof(Func<,>).MakeGenericType(thisType, typeof(IQueryable<>).MakeGenericType(targetType));
            return Expression.Lambda(delegateType, body, parameter);
        }
    }
}
