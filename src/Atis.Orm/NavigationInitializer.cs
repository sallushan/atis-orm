using Atis.Expressions;
using Atis.SqlExpressionEngine;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.ExpressionExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Atis.Orm
{
    /// <summary>
    ///     <para>
    ///         Default <see cref="INavigationInitializer"/>. Populates lazy navigation properties of a
    ///         materialized entity by building a filtered <see cref="OrmQueryable{T}"/> from the
    ///         entity's <see cref="NavigationInfo"/> metadata, binding the entity instance as a constant
    ///         into the join predicate.
    ///     </para>
    ///     <para>
    ///         Registered as a Scoped service so that the lazy queries it builds re-query through the
    ///         same <see cref="IAsyncQueryProvider"/> (i.e. the same <see cref="DataContext"/>) that
    ///         produced the entity. The provider is resolved lazily to avoid a construction cycle
    ///         (<c>OrmQueryProvider -&gt; QueryExecutor -&gt; NavigationInitializer -&gt; provider</c>);
    ///         by the time <see cref="Initialize"/> runs during enumeration the provider is fully built.
    ///     </para>
    /// </summary>
    public class NavigationInitializer : INavigationInitializer
    {
        private static MethodInfo _firstOrDefaultMethod;
        private static MethodInfo FirstOrDefaultMethod =>
            _firstOrDefaultMethod ??
            (_firstOrDefaultMethod = typeof(Queryable).GetMethods()
                .First(m => m.Name == nameof(Queryable.FirstOrDefault) && m.GetParameters().Length == 1));

        private static MethodInfo _toListMethod;
        private static MethodInfo ToListMethod =>
            _toListMethod ??
            (_toListMethod = typeof(Enumerable).GetMethods()
                .First(m => m.Name == nameof(Enumerable.ToList) && m.GetParameters().Length == 1));

        private readonly IServiceProvider serviceProvider;
        private readonly IOrmModel model;
        private readonly IEntityMetadataBuilder metadataBuilder;
        private readonly IReflectionService reflectionService;

        private IAsyncQueryProvider queryProvider;

        public NavigationInitializer(IServiceProvider serviceProvider, IOrmModel model, IEntityMetadataBuilder metadataBuilder, IReflectionService reflectionService)
        {
            this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.metadataBuilder = metadataBuilder ?? throw new ArgumentNullException(nameof(metadataBuilder));
            this.reflectionService = reflectionService ?? throw new ArgumentNullException(nameof(reflectionService));
        }

        private IAsyncQueryProvider QueryProvider =>
            this.queryProvider ??
            (this.queryProvider = (IAsyncQueryProvider)this.serviceProvider.GetService(typeof(IAsyncQueryProvider)));

        /// <inheritdoc />
        public void Initialize(object entity)
        {
            if (entity is null)
                return;

            // No-op for non-entity shapes (anonymous/DTO projections, scalars).
            if (!this.model.TryGet(entity.GetType(), out var metadata))
                return;

            foreach (var nav in metadata.Navigations.Values)
            {
                var prop = this.reflectionService.GetPropertyOrField(entity.GetType(), nav.PropertyName) as PropertyInfo;
                if (prop is null || !prop.CanWrite)
                    continue;

                // Don't overwrite an already-populated navigation (keeps a future Include's value).
                if (this.reflectionService.GetPropertyOrFieldValue(entity, prop) != null)
                    continue;

                try
                {
                    var value = this.BuildNavigationValue(entity, nav, this.reflectionService.GetPropertyOrFieldType(prop));
                    if (value != null)
                        prop.SetValue(entity, value);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"An error occurred while setting navigation property '{nav.PropertyName}' of entity '{entity.GetType().Name}'.", ex);
                }
            }
        }

        /// <summary>
        ///     Produces the value to assign to a navigation property, or <c>null</c> when the property
        ///     shape is out of phase-1 scope (plain reference / non-queryable collection).
        /// </summary>
        private object BuildNavigationValue(object entity, NavigationInfo nav, Type propType)
        {
            // JoinedSource is (thisEntity) => IQueryable<TRelated>.
            var targetType = this.reflectionService.GetElementType(nav.JoinedSource.ReturnType);

            // IQueryable<TRelated> navigation -> assign the composable lazy query directly.
            if (IsGenericQueryable(propType))
            {
                return this.BuildNavigationQuery(entity, nav, targetType);
            }

            // Func<...> navigation -> assign a delegate that runs the query on invocation.
            if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(Func<>))
            {
                var navQuery = this.BuildNavigationQuery(entity, nav, targetType);
                var funcReturn = propType.GetGenericArguments()[0];
                var queryableType = typeof(IQueryable<>).MakeGenericType(targetType);

                Expression body;
                if (funcReturn == targetType)
                {
                    // Single-valued: () => query.FirstOrDefault()
                    var firstOrDefault = FirstOrDefaultMethod.MakeGenericMethod(targetType);
                    body = Expression.Call(firstOrDefault, Expression.Constant(navQuery, queryableType));
                }
                else if (funcReturn.IsAssignableFrom(queryableType))
                {
                    // Func<IEnumerable<TRelated>> (or IQueryable<TRelated>) -> return the lazy query as-is.
                    body = Expression.Constant(navQuery, queryableType);
                }
                else
                {
                    // Func<IList<TRelated>> / Func<List<TRelated>> etc. -> materialize via ToList().
                    var toList = ToListMethod.MakeGenericMethod(targetType);
                    body = Expression.Call(toList, Expression.Constant(navQuery, typeof(IEnumerable<>).MakeGenericType(targetType)));
                }

                return Expression.Lambda(propType, body).Compile();
            }

            // Out of phase-1 scope (plain T reference, plain IEnumerable<T>, etc.).
            return null;
        }

        /// <summary>
        ///     Builds an <see cref="OrmQueryable{T}"/> for <paramref name="nav"/> filtered to the related
        ///     rows of <paramref name="entity"/>.
        /// </summary>
        private object BuildNavigationQuery(object entity, NavigationInfo nav, Type targetType)
        {
            // Ensure the target entity's metadata is registered before the query is translated.
            this.model.GetOrAdd(targetType, t => this.metadataBuilder.Build(t));

            var queryableType = typeof(OrmQueryable<>).MakeGenericType(targetType);
            var root = (IQueryable)this.reflectionService.CreateInstance(queryableType, new object[] { this.QueryProvider });

            Expression queryExpression;
            if (nav.JoinCondition != null)
            {
                // Standard navigation (HasMany / HasParent / HasChild): root.Where(predicate)
                var predicate = this.BuildPredicate(entity, nav);
                queryExpression = Expression.Call(
                    typeof(Queryable),
                    nameof(Queryable.Where),
                    new[] { targetType },
                    root.Expression,
                    Expression.Quote(predicate));
            }
            else
            {
                // HasOneRow correlated subquery: the JoinedSource body already encodes the filter /
                // ordering / Take(1). Replace the QueryRootExpression with the real root queryable and
                // bind the entity instance as a constant.
                var joinedSource = nav.JoinedSource;
                var entityParam = joinedSource.Parameters[0];
                var entityConstant = Expression.Constant(entity, entityParam.Type);
                var body = ExpressionReplacementVisitor.Replace(entityParam, entityConstant, joinedSource.Body);
                var rootConstant = Expression.Constant(root, typeof(IQueryable<>).MakeGenericType(targetType));
                queryExpression = new QueryRootReplacer(rootConstant).Visit(body);
            }

            return this.reflectionService.CreateInstance(queryableType, new object[] { this.QueryProvider, queryExpression });
        }

        /// <summary>
        ///     Turns the navigation's <c>(parent, child) =&gt; parent.PK == child.FK</c> join condition
        ///     into a <c>Func&lt;TRelated, bool&gt;</c> predicate by binding the entity instance into the
        ///     appropriate parameter as a constant.
        /// </summary>
        private LambdaExpression BuildPredicate(object entity, NavigationInfo nav)
        {
            var joinCondition = nav.JoinCondition;
            ParameterExpression entityParam;
            ParameterExpression keepParam;
            switch (nav.NavigationType)
            {
                case NavigationType.ToParent:
                case NavigationType.ToParentOptional:
                    // entity is the child (parameter 1); the related parent is kept.
                    entityParam = joinCondition.Parameters[1];
                    keepParam = joinCondition.Parameters[0];
                    break;
                default:
                    // ToChildren / ToSingleChild: entity is the parent (parameter 0); child is kept.
                    entityParam = joinCondition.Parameters[0];
                    keepParam = joinCondition.Parameters[1];
                    break;
            }

            var entityConstant = Expression.Constant(entity, entityParam.Type);
            var predicateBody = ExpressionReplacementVisitor.Replace(entityParam, entityConstant, joinCondition.Body);
            return Expression.Lambda(predicateBody, keepParam);
        }

        private static bool IsGenericQueryable(Type type)
        {
            return type.IsGenericType
                   && typeof(IQueryable).IsAssignableFrom(type)
                   && type.GetGenericArguments().Length == 1;
        }

        /// <summary>
        ///     Replaces every <see cref="QueryRootExpression"/> node in a tree with a supplied constant
        ///     queryable, so a navigation's <c>JoinedSource</c> can be turned into an executable query.
        /// </summary>
        private sealed class QueryRootReplacer : ExpressionVisitor
        {
            private readonly Expression replacement;

            public QueryRootReplacer(Expression replacement)
            {
                this.replacement = replacement;
            }

            public override Expression Visit(Expression node)
            {
                if (node is QueryRootExpression)
                    return this.replacement;
                return base.Visit(node);
            }
        }
    }
}
