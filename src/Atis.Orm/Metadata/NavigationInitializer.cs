using Atis.Expressions;
using Atis.SqlExpressionEngine;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.ExpressionExtensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Atis.Orm.Abstractions;
using Atis.Orm.Services;
namespace Atis.Orm.Metadata
{
    /// <summary>
    ///     <para>
    ///         Performance-focused <see cref="INavigationInitializer"/>. Populates the lazy navigation
    ///         properties of a materialized entity. Exactly two property shapes are supported:
    ///         <c>IQueryable&lt;TChild&gt;</c> (assigned a composable lazy query at initialization time)
    ///         and <c>Func&lt;TOther&gt;</c> (assigned a delegate that loads the single related entity on
    ///         invocation). Any other property shape is skipped.
    ///     </para>
    ///     <para>
    ///         All per-type reflection (property lookup, expression building, delegate compilation)
    ///         happens once per entity type; the result is kept in a process-level cache so that a type
    ///         with no supported navigations costs a single dictionary lookup per materialized row.
    ///         This assumes every <see cref="DataContext"/> configuration in the process maps a given
    ///         CLR type with the same navigations.
    ///     </para>
    ///     <para>
    ///         Single-entity (<c>Func&lt;TOther&gt;</c>) navigations are served from a
    ///         <see cref="TimedCacheManager{TKey, TValue}"/>: a loaded entity is reused for repeated
    ///         calls within <see cref="CacheExpiration"/> of its last access (sliding expiration), but
    ///         only while the join keys still match (the compiled <see cref="NavigationInfo.JoinCondition"/>
    ///         is re-evaluated against the caller, so changing a foreign key forces a reload). The cache's
    ///         cleanup timer evicts expired entries so a long-lived initializer does not pin entities that
    ///         went out of scope; disposing the DI scope disposes the timer.
    ///     </para>
    ///     <para>
    ///         Registered as a Scoped service so that the lazy queries it builds re-query through the
    ///         same provider (i.e. the same <see cref="DataContext"/>) that produced the entity.
    ///     </para>
    /// </summary>
    public class NavigationInitializer : INavigationInitializer, IDisposable
    {
        private static MethodInfo _getChildrenQueryMethod;
        private static MethodInfo GetChildrenQueryMethod =>
            _getChildrenQueryMethod ??
            (_getChildrenQueryMethod = typeof(NavigationInitializer).GetMethod(nameof(GetChildrenQuery), BindingFlags.NonPublic | BindingFlags.Instance));

        private static MethodInfo _getOtherEntityMethod;
        private static MethodInfo GetOtherEntityMethod =>
            _getOtherEntityMethod ??
            (_getOtherEntityMethod = typeof(NavigationInitializer).GetMethod(nameof(GetOtherEntity), BindingFlags.NonPublic | BindingFlags.Instance));

        /// <summary>
        ///     Process-level cache of the compiled per-type initialization action. A cached <c>null</c>
        ///     value means "this type has no supported navigations" and makes <see cref="Initialize"/>
        ///     a single dictionary lookup for such types. The initializer instance is passed as a
        ///     parameter so the compiled action never captures a scoped instance.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, Action<NavigationInitializer, object>> iteratorCache
            = new ConcurrentDictionary<Type, Action<NavigationInitializer, object>>();

        /// <summary>
        ///     Process-level cache of compiled join-condition match functions, keyed by the
        ///     <see cref="NavigationInfo"/> instance. Compiled lazily on the first navigation
        ///     *invocation* (the deferred path), not during row materialization.
        /// </summary>
        private static readonly ConcurrentDictionary<NavigationInfo, Func<object, object, bool>> matchFunctionCache
            = new ConcurrentDictionary<NavigationInfo, Func<object, object, bool>>();

        private readonly IQueryableFactory queryableFactory;
        private readonly IOrmModel model;
        private readonly IReflectionService reflectionService;

        /// <summary>
        ///     Per-scope cache of loaded single-entity navigation results, one entry per navigation
        ///     (deliberately capacity-1: a caller with different join keys replaces the entry).
        ///     Created lazily on the first single-entity load so the cleanup timer only exists for
        ///     scopes that actually use single-entity navigations.
        /// </summary>
        private TimedCacheManager<NavigationInfo, SingleEntityCacheEntry> singleEntityCache;
        private readonly object cacheCreationLock = new object();

        private TimedCacheManager<NavigationInfo, SingleEntityCacheEntry> SingleEntityCache
        {
            get
            {
                if (this.singleEntityCache == null)
                {
                    lock (this.cacheCreationLock)
                    {
                        if (this.singleEntityCache == null)
                            this.singleEntityCache = new TimedCacheManager<NavigationInfo, SingleEntityCacheEntry>(this.CacheExpiration);
                    }
                }
                return this.singleEntityCache;
            }
        }

        /// <summary>
        ///     Creates the initializer. <paramref name="queryableFactory"/> is used to build the lazy
        ///     navigation queries, <paramref name="model"/> supplies the navigation metadata per entity
        ///     type, and <paramref name="reflectionService"/> resolves the navigation properties while
        ///     the per-type initialization action is being compiled.
        /// </summary>
        public NavigationInitializer(IQueryableFactory queryableFactory, IOrmModel model, IReflectionService reflectionService)
        {
            this.queryableFactory = queryableFactory ?? throw new ArgumentNullException(nameof(queryableFactory));
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.reflectionService = reflectionService ?? throw new ArgumentNullException(nameof(reflectionService));
        }

        /// <summary>
        ///     How long a cached single-entity navigation result stays valid after its last access
        ///     (sliding expiration). Short by design: the cache only exists to absorb repeated
        ///     navigation calls within one code flow. Takes effect when the first single-entity
        ///     navigation is loaded; changing it afterwards has no effect.
        /// </summary>
        public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromSeconds(5);

        /// <inheritdoc />
        public void Initialize(object entity)
        {
            if (entity is null)
                return;

            var entityType = entity.GetType();
            if (!iteratorCache.TryGetValue(entityType, out var iterator))
            {
                // Only cache a verdict once the model has metadata for the type, so a type that is
                // registered later (or a DTO/anonymous projection) is not permanently marked as
                // having no navigations.
                if (!this.model.TryGet(entityType, out var metadata))
                    return;
                iterator = iteratorCache.GetOrAdd(entityType, t => this.CreateNavigationPropertiesIterator(t, metadata));
            }
            iterator?.Invoke(this, entity);
        }

        /// <summary>
        ///     Builds the compiled initialization action for <paramref name="entityType"/>, or
        ///     <c>null</c> when the type has no writable navigation property of a supported shape.
        ///     Every already-populated navigation is left untouched (keeps a future Include's value).
        /// </summary>
        private Action<NavigationInitializer, object> CreateNavigationPropertiesIterator(Type entityType, EntityMetadata metadata)
        {
            var initializerParam = Expression.Parameter(typeof(NavigationInitializer), "initializer");
            var entityObjParam = Expression.Parameter(typeof(object), "entityObj");
            var entityVar = Expression.Variable(entityType, "entity");
            var body = new List<Expression>
            {
                Expression.Assign(entityVar, Expression.Convert(entityObjParam, entityType)),
            };

            foreach (var navigation in metadata.Navigations.Values)
            {
                var property = this.reflectionService.GetPropertyOrField(entityType, navigation.PropertyName) as PropertyInfo;
                if (property is null || !property.CanWrite)
                    continue;

                // JoinedSource is (thisEntity) => IQueryable<TOther>.
                var targetType = this.reflectionService.GetElementType(navigation.JoinedSource.ReturnType);
                var entityAsObject = Expression.Convert(entityVar, typeof(object));

                Expression valueExpression;
                if (property.PropertyType == typeof(IQueryable<>).MakeGenericType(targetType))
                {
                    // IQueryable<TChild> -> build the lazy query at initialization time.
                    valueExpression = Expression.Call(
                        initializerParam,
                        GetChildrenQueryMethod.MakeGenericMethod(targetType),
                        entityAsObject,
                        Expression.Constant(navigation));
                }
                else if (property.PropertyType == typeof(Func<>).MakeGenericType(targetType))
                {
                    // Func<TOther> -> assign a closure; the load (and its caching) is deferred to
                    // invocation, so nothing expensive runs during row materialization.
                    var loadCall = Expression.Call(
                        initializerParam,
                        GetOtherEntityMethod.MakeGenericMethod(targetType),
                        entityAsObject,
                        Expression.Constant(navigation));
                    valueExpression = Expression.Lambda(property.PropertyType, loadCall);
                }
                else
                {
                    continue;
                }

                var propertyAccess = Expression.Property(entityVar, property);
                body.Add(Expression.IfThen(
                    Expression.ReferenceEqual(propertyAccess, Expression.Constant(null, property.PropertyType)),
                    Expression.Assign(propertyAccess, valueExpression)));
            }

            if (body.Count == 1)
                return null;

            var block = Expression.Block(new[] { entityVar }, body);
            return Expression.Lambda<Action<NavigationInitializer, object>>(block, initializerParam, entityObjParam).Compile();
        }

        private IQueryable<TChildEntity> GetChildrenQuery<TChildEntity>(object thisEntity, NavigationInfo navigation)
        {
            return this.CreateNavigationQuery<TChildEntity>(thisEntity, navigation);
        }

        private TOtherEntity GetOtherEntity<TOtherEntity>(object thisEntity, NavigationInfo navigation)
        {
            // HasOneRow-style navigation has no join condition to validate a cached entity against.
            if (navigation.JoinCondition is null)
                return this.CreateNavigationQuery<TOtherEntity>(thisEntity, navigation).FirstOrDefault();

            if (this.SingleEntityCache.TryGetItem(navigation, out var entry)
                && entry.OtherEntity != null
                && entry.MatchFunction(thisEntity, entry.OtherEntity))
            {
                return (TOtherEntity)entry.OtherEntity;
            }

            var otherEntity = this.CreateNavigationQuery<TOtherEntity>(thisEntity, navigation).FirstOrDefault();
            if (otherEntity != null)
            {
                this.SingleEntityCache.SetItem(navigation, new SingleEntityCacheEntry(otherEntity, GetOrAddMatchFunction(navigation)));
            }
            return otherEntity;
        }

        /// <summary>
        ///     <para>
        ///         Builds the lazy query for a navigation, filtered to <paramref name="thisEntity"/>'s
        ///         related rows. <see cref="NavigationInfo.JoinedSource"/> always defines the related
        ///         data source: for key-based navigations it is a plain <see cref="QueryRootExpression"/>,
        ///         while custom relations may supply a correlated query (e.g.
        ///         <c>entity =&gt; entity.NavChildren.Take(1)</c>) or a dynamically built query over a
        ///         <see cref="QueryRootExpression"/> of a different entity type.
        ///     </para>
        ///     <para>
        ///         This entity is bound into the source as a constant, every
        ///         <see cref="QueryRootExpression"/> is materialized through the
        ///         <see cref="IQueryableFactory"/> using the query root's own entity type (which also
        ///         registers that type's metadata), and <see cref="NavigationInfo.JoinCondition"/> â€”
        ///         when present â€” is applied on top as a <c>Where</c> correlating the source to this
        ///         entity.
        ///     </para>
        /// </summary>
        private IQueryable<TOtherEntity> CreateNavigationQuery<TOtherEntity>(object thisEntity, NavigationInfo navigation)
        {
            var joinedSource = navigation.JoinedSource;
            var entityParam = joinedSource.Parameters[0];
            var source = ExpressionReplacementVisitor.Replace(entityParam, Expression.Constant(thisEntity, entityParam.Type), joinedSource.Body);
            source = new QueryRootReplacementVisitor(this).Visit(source);
            if (navigation.JoinCondition != null)
            {
                var predicate = BuildPredicate(thisEntity, navigation);
                source = Expression.Call(
                    typeof(Queryable),
                    nameof(Queryable.Where),
                    new[] { typeof(TOtherEntity) },
                    source,
                    Expression.Quote(predicate));
            }
            return this.queryableFactory.CreateQueryable<TOtherEntity>(source);
        }

        private static readonly MethodInfo createQueryableOpenMethod = typeof(IQueryableFactory)
            .GetMethods()
            .First(m => m.Name == nameof(IQueryableFactory.CreateQueryable) && m.GetParameters().Length == 0);

        private static readonly ConcurrentDictionary<Type, MethodInfo> createQueryableMethodCache
            = new ConcurrentDictionary<Type, MethodInfo>();

        private IQueryable CreateRootQueryable(Type entityType)
        {
            var closedMethod = createQueryableMethodCache.GetOrAdd(entityType, t => createQueryableOpenMethod.MakeGenericMethod(t));
            return (IQueryable)closedMethod.Invoke(this.queryableFactory, Array.Empty<object>());
        }

        /// <summary>
        ///     Turns the navigation's <c>(parent, child) =&gt; parent.PK == child.FK</c> join condition
        ///     into a predicate over the related entity by binding <paramref name="thisEntity"/> into
        ///     the appropriate parameter as a constant.
        /// </summary>
        private static LambdaExpression BuildPredicate(object thisEntity, NavigationInfo navigation)
        {
            var joinCondition = navigation.JoinCondition;
            ParameterExpression entityParam;
            ParameterExpression keepParam;
            switch (navigation.NavigationType)
            {
                case NavigationType.ToParent:
                case NavigationType.ToParentOptional:
                    // this entity is the child (parameter 1); the related parent is kept.
                    entityParam = joinCondition.Parameters[1];
                    keepParam = joinCondition.Parameters[0];
                    break;
                default:
                    // ToChildren / ToSingleChild: this entity is the parent (parameter 0); child is kept.
                    entityParam = joinCondition.Parameters[0];
                    keepParam = joinCondition.Parameters[1];
                    break;
            }
            var predicateBody = ExpressionReplacementVisitor.Replace(entityParam, Expression.Constant(thisEntity, entityParam.Type), joinCondition.Body);
            return Expression.Lambda(predicateBody, keepParam);
        }

        /// <summary>
        ///     Compiles the join condition into <c>(thisEntity, otherEntity) =&gt; bool</c> â€” returns
        ///     <c>true</c> while the join keys still match, i.e. the cached entity is NOT stale. The
        ///     parameters are ordered per <see cref="NavigationType"/> because the join condition is
        ///     always <c>(parent, child)</c> while "this" entity is the child for ToParent navigations.
        /// </summary>
        private static Func<object, object, bool> GetOrAddMatchFunction(NavigationInfo navigation)
        {
            return matchFunctionCache.GetOrAdd(navigation, nav =>
            {
                var parameters = nav.JoinCondition.Parameters;
                ParameterExpression thisParam;
                ParameterExpression otherParam;
                if (nav.NavigationType == NavigationType.ToParent || nav.NavigationType == NavigationType.ToParentOptional)
                {
                    thisParam = parameters[1];
                    otherParam = parameters[0];
                }
                else
                {
                    thisParam = parameters[0];
                    otherParam = parameters[1];
                }
                var thisObjParam = Expression.Parameter(typeof(object), "thisEntity");
                var otherObjParam = Expression.Parameter(typeof(object), "otherEntity");
                var body = ExpressionReplacementVisitor.Replace(thisParam, Expression.Convert(thisObjParam, thisParam.Type), nav.JoinCondition.Body);
                body = ExpressionReplacementVisitor.Replace(otherParam, Expression.Convert(otherObjParam, otherParam.Type), body);
                return Expression.Lambda<Func<object, object, bool>>(body, thisObjParam, otherObjParam).Compile();
            });
        }

        /// <inheritdoc />
        public void Dispose()
        {
            lock (this.cacheCreationLock)
            {
                this.singleEntityCache?.Dispose();
            }
        }

        private sealed class SingleEntityCacheEntry
        {
            public SingleEntityCacheEntry(object otherEntity, Func<object, object, bool> matchFunction)
            {
                this.OtherEntity = otherEntity;
                this.MatchFunction = matchFunction ?? throw new ArgumentNullException(nameof(matchFunction));
            }

            public object OtherEntity { get; }
            public Func<object, object, bool> MatchFunction { get; }
        }

        /// <summary>
        ///     Replaces every <see cref="QueryRootExpression"/> node in a tree with a constant queryable
        ///     created through the <see cref="IQueryableFactory"/> for the query root's own entity type,
        ///     so a navigation's <c>JoinedSource</c> becomes an executable query and each root entity
        ///     type's metadata gets registered.
        /// </summary>
        private sealed class QueryRootReplacementVisitor : ExpressionVisitor
        {
            private readonly NavigationInitializer initializer;

            public QueryRootReplacementVisitor(NavigationInitializer initializer)
            {
                this.initializer = initializer;
            }

            public override Expression Visit(Expression node)
            {
                // The queryable's own Expression is substituted (not a re-typed constant) because the
                // SQL engine recognizes the query source by the constant's runtime queryable type.
                if (node is QueryRootExpression queryRoot)
                    return this.initializer.CreateRootQueryable(queryRoot.EntityType).Expression;
                return base.Visit(node);
            }
        }
    }
}
