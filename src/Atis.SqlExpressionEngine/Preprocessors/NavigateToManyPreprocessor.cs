using Atis.Expressions;
using Atis.SqlExpressionEngine.ExpressionExtensions;
using Atis.SqlExpressionEngine.SqlExpressions;
using Atis.SqlExpressionEngine.Internal;
using Atis.SqlExpressionEngine.Abstractions;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Atis.SqlExpressionEngine.Preprocessors
{
    /// <summary>
    ///     <para>
    ///         Rewrites "many navigation" usage into a canonical query form using the provided model metadata.
    ///     </para>
    ///     <para>
    ///         This class is model-agnostic: it does not use attributes or conventions directly; it only consumes metadata.
    ///     </para>
    /// </summary>
    public sealed class NavigateToManyPreprocessor : ExpressionVisitor, IExpressionPreprocessor
    {
        private readonly IModel model;

        public NavigateToManyPreprocessor(IModel model)
        {
            this.model = model ?? throw new ArgumentNullException(nameof(model));
        }

        public void Initialize()
        {
            // do nothing
        }

        public Expression Preprocess(Expression expression)
        {
            return this.Process(expression);
        }

        /// <summary>
        ///     <para>
        ///         Runs the preprocessing pass over the supplied expression.
        ///     </para>
        /// </summary>
        public Expression Process(Expression expression)
        {
            if (expression is null) throw new ArgumentNullException(nameof(expression));
            return Visit(expression);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            // First let children be processed (so nested navs inside arguments get normalized early).
            var updatedExpr = base.VisitMember(node);

            if (!(updatedExpr is MemberExpression updated))
                return updatedExpr;

            var parentExpr = updated.Expression;
            if (parentExpr is null)
                return updated;

            if (!TryResolveNavigation(updated, out var nav))
                return updated;

            // We only handle MANY here. (A separate preprocessor can handle "NavigateToOne".)
            if (nav.NavigationType != NavigationType.ToChildren)
                return updated;

            var childQueryExpr = CreateChildQueryExpression(nav, updated, parentExpr);

            // Return an expression that *represents* the child query (IQueryable<Child>).
            // Your downstream converter can interpret it as APPLY/JOIN depending on JoinPredicate.
            return childQueryExpr;
        }

        protected bool TryResolveNavigation(MemberExpression navNode, out NavigationInfo navigation)
            => model.TryGetNavigation(navNode, out navigation);

        private Expression CreateChildQueryExpression(NavigationInfo nav, MemberExpression navigationNode, Expression parentExpression)
        {
            var navigationName = this.GetNavigationName(navigationNode);
            var entityType = this.GetEntityType(navigationNode)
                                ??
                                throw new InvalidOperationException("Unable to determine entity type for navigation");
            // CreateNavSubQueryFuncCall<>
            var createNavSubQueryFuncCallOpen = this.GetType().GetMethod(nameof(GetQueryExpressionInternal), BindingFlags.NonPublic | BindingFlags.Instance)
                                                ??
                                                throw new InvalidOperationException("GetQueryExpression() method not found");
            // CreateNavSubQueryFuncCall<entityType>
            var createNavSubQueryFuncCall = createNavSubQueryFuncCallOpen.MakeGenericMethod(entityType);
            var result = createNavSubQueryFuncCall.Invoke(this, new object[] { nav, parentExpression }) as Expression
                                ??
                                throw new InvalidOperationException("GetQueryExpression() returned null");

            var subQueryNavigation = new SubQueryNavigationExpression(result, navigationName);

            return subQueryNavigation;
        }

        private string GetNavigationName(MemberExpression navigationNode)
        {
            return navigationNode.Member.Name;
        }

        private Type GetEntityType(Expression navigationNode)
        {
            var type = navigationNode.Type;
            if (type.IsGenericType && typeof(IQueryable).IsAssignableFrom(type))
            {
                return type.GetGenericArguments()[0];
            }
            return null;
        }


        private Expression GetQueryExpressionInternal<T>(NavigationInfo navigationInfo, Expression parentExpression)
        {
            if (navigationInfo.JoinedSource != null)
            {
                var joinedSourceLambda = navigationInfo.JoinedSource;
                var joinedSourceBody = joinedSourceLambda.Body;
                // below will be like this new Queryable<ChildTable>()
                joinedSourceBody = ExpressionReplacementVisitor.Replace(joinedSourceLambda.Parameters[0], parentExpression, joinedSourceBody);
                var predicate = this.CreatePredicate<T>(navigationInfo, parentExpression);
                joinedSourceBody = Expression.Call(typeof(Queryable), nameof(Queryable.Where), new[] { typeof(T) }, joinedSourceBody, predicate);
                return joinedSourceBody;
            }
            else
            {
                //return this.CreateQueryInternal<T>(navigationInfo, parentExpression).Expression;
                return this.CreateQueryExpressionInternal<T>(navigationInfo, parentExpression);
            }
        }

        /// <summary>
        ///     <para>
        ///         Creates the query root expression for a given entity type.
        ///     </para>
        ///     <para>
        ///         Override this if you need a custom root expression per entity type.
        ///     </para>
        /// </summary>
        private Expression CreateQueryRoot(Type entityType)
        {
            if (entityType is null)
                throw new ArgumentNullException(nameof(entityType));

            return new QueryRootExpression(entityType);
        }


        //private IQueryable<T> CreateQueryInternal<T>(NavigationInfo navigationInfo, Expression parentExpression)
        //{
        //    var predicate = this.CreatePredicate<T>(navigationInfo, parentExpression);
        //    var query = this.CreateQuery<T>();
        //    query = query.Where(predicate);
        //    return query;
        //}
        private Expression CreateQueryExpressionInternal<T>(NavigationInfo navigationInfo, Expression parentExpression)
        {
            var predicate = this.CreatePredicate<T>(navigationInfo, parentExpression);

            // Build: Queryable.Where<T>( <root>, predicate )
            var root = this.CreateQueryRoot(typeof(T));
            var whereCall =
                Expression.Call(
                    typeof(Queryable),
                    nameof(Queryable.Where),
                    new[] { typeof(T) },
                    root,
                    Expression.Quote(predicate));

            return whereCall;
        }



        private Expression<Func<T, bool>> CreatePredicate<T>(NavigationInfo navigationInfo, Expression parentExpression)
        {
            var relationLambda = navigationInfo.JoinCondition;

            /* 
             * relationLambda will be like this
             * 
             * parent, child => parent.PK == child.FK       <- this is actual join expression
             *    \         \________
             *     \                 \
             * Parameter 0           Parameter 1
             * 
             * Example:
             *      x.NavLines.Any(y => y.Field > 5)
             * should be converted to
             *      new Queryable<Line>().Where(     child => x.PK == child.FK  ).Any(y => y.Field > 5)
             *                         _________/       \      \________
             *                        /              Parameter 1        |
             *                       |                                  |
             *                  Parameter 0 is replaced with parentExpression
             *                                   
             * */

            var parameterToReplace = relationLambda.Parameters[0];
            var parameterToKeep = relationLambda.Parameters[1];

            var predicateBody = ExpressionReplacementVisitor.Replace(parameterToReplace, parentExpression, relationLambda.Body);
            var predicateLambda = Expression.Lambda<Func<T, bool>>(predicateBody, parameterToKeep);
            return predicateLambda;
        }
    }
}
