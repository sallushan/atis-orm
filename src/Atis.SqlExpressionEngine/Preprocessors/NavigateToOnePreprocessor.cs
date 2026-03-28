using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.ExpressionExtensions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.Preprocessors
{
    /// <summary>
    ///     <para>
    ///         Preprocesses "navigate-to-one" expressions and converts them into explicit
    ///         navigation join calls understood by the SQL expression engine.
    ///     </para>
    ///     <para>
    ///         This preprocessor works in two passes:
    ///     </para>
    ///     <para>
    ///         1) Extraction Pass:
    ///            While traversing the expression tree, navigation member accesses
    ///            (e.g., s.Parent or s.Parent()) are detected using the provided IModel.
    ///            These are temporarily rewritten into NavigationMemberExpression nodes
    ///            and metadata is collected describing where a join must be injected.
    ///     </para>
    ///     <para>
    ///         2) Injection Pass:
    ///            The tree is traversed again. At the appropriate query method boundary
    ///            (e.g., Where, Select, GroupBy), the original query source argument is
    ///            wrapped inside a NavigationJoinCallExpression. This effectively converts
    ///            implicit navigation usage into an explicit join representation.
    ///     </para>
    ///     <para>
    ///         Supported navigation property shapes:
    ///     </para>
    ///     <para>
    ///         - ParentEntity NavParent { get; set; }
    ///     </para>
    ///     <para>
    ///         - Func&lt;ParentEntity&gt; NavParent { get; set; }
    ///     </para>
    ///     <para>
    ///         Example:
    ///     </para>
    ///     <para>
    ///         Given:
    ///             query.Where(s => s.Parent.Name == "John")
    ///     </para>
    ///     <para>
    ///         Pass 1:
    ///             s.Parent  →  NavigationMemberExpression
    ///             (metadata recorded to inject join)
    ///     </para>
    ///     <para>
    ///         Pass 2:
    ///             query source is wrapped as:
    ///                 NavigationJoinCallExpression(...)
    ///     </para>
    ///     <para>
    ///         Result:
    ///             The query is normalized so that navigation access becomes
    ///             an explicit join node in the SQL expression tree.
    ///     </para>
    ///     <para>
    ///         This class is model-agnostic and relies entirely on IModel
    ///         to resolve navigation metadata.
    ///     </para>
    /// </summary>

    public class NavigateToOnePreprocessor : ExpressionVisitor, IExpressionPreprocessor
    {
        private sealed class NavigationJoinMetadata
        {
            public NavigationJoinMetadata(Expression queryMethod)
            {
                QueryMethod = queryMethod ?? throw new ArgumentNullException(nameof(queryMethod));
                Navigations = new Dictionary<string, (LambdaExpression ParentExpression, NavigationInfo NavInfo)>();
            }

            public Expression QueryMethod { get; }
            public Dictionary<string, (LambdaExpression ParentExpression, NavigationInfo NavInfo)> Navigations { get; }
        }

        private readonly IModel model;

        private readonly Stack<Expression> stack = new Stack<Expression>();
        private readonly Stack<Expression> queryMethodStack = new Stack<Expression>();
        private readonly Dictionary<Expression, NavigationJoinMetadata> navigationJoinMetadata = new Dictionary<Expression, NavigationJoinMetadata>();
        private readonly Dictionary<Expression, Expression> newNodeVsOldNode = new Dictionary<Expression, Expression>();

        private enum FlowType
        {
            Extract,
            Inject,
        }

        private FlowType flowType;

        public NavigateToOnePreprocessor(IModel model)
        {
            this.model = model ?? throw new ArgumentNullException(nameof(model));
        }

        /// <inheritdoc />
        public void Initialize()
        {
            stack.Clear();
            queryMethodStack.Clear();
            navigationJoinMetadata.Clear();
            newNodeVsOldNode.Clear();
        }

        /// <inheritdoc />
        public Expression Preprocess(Expression expression)
        {
            if (expression is null) throw new ArgumentNullException(nameof(expression));

            Initialize();

            flowType = FlowType.Extract;
            var visited = Visit(expression);

            flowType = FlowType.Inject;
            visited = Visit(visited);

            return visited;
        }

        /// <inheritdoc />
        public override Expression Visit(Expression node)
        {
            if (node is null) return null;

            var wasQueryMethod = false;

            stack.Push(node);
            if (IsQueryMethod(node))
            {
                wasQueryMethod = true;
                queryMethodStack.Push(node);
            }

            var visited = base.Visit(node);

            if (flowType == FlowType.Extract)
            {
                var stackArray = stack.ToArray();

                if (TryGetNavigationInfo(visited, stackArray, out var parentExpression, out var navigationInfo))
                {
                    CreateMetadataForNavigationJoin(parentExpression, navigationInfo);

                    var joinedDataSourceType = node.Type;
                    visited = new NavigationMemberExpression(
                        parentExpression,
                        navigationInfo.PropertyName,
                        joinedDataSourceType);
                }

                // map node with visited
                newNodeVsOldNode[visited] = node;
            }
            else
            {
                // Inject pass
                if (newNodeVsOldNode.TryGetValue(node, out var oldNode))
                {
                    if (navigationJoinMetadata.TryGetValue(oldNode, out var meta))
                    {
                        visited = WrapFirstArgInNavigationJoinCall(meta, visited);
                        navigationJoinMetadata.Remove(oldNode);
                    }
                }
            }

            stack.Pop();
            if (wasQueryMethod)
                queryMethodStack.Pop();

            return visited;
        }

        protected virtual bool IsQueryMethod(Expression node)
            => node is MethodCallExpression;

        protected virtual bool DoesParameterBelongToQueryMethod(ParameterExpression parameter, Expression queryMethod)
        {
            if (queryMethod is MethodCallExpression methodCall)
            {
                foreach (var arg in methodCall.Arguments)
                {
                    var lambda =
                        (arg as UnaryExpression)?.Operand as LambdaExpression
                        ??
                        arg as LambdaExpression;

                    if (lambda?.Parameters.Contains(parameter) ?? false)
                        return true;
                }
            }
            return false;
        }

        protected virtual Expression GetQuerySourceArgumentFromQueryMethod(Expression queryMethod)
        {
            if (queryMethod is MethodCallExpression mc && mc.Arguments.Count > 0)
            {
                // default assumption: first argument is the query source
                return mc.Arguments[0];
            }
            return null;
        }

        protected virtual Expression CreateQueryMethodCall(Expression oldQueryMethodNode, Expression wrappedQuerySourceArg)
        {
            if (oldQueryMethodNode is MethodCallExpression mc)
            {
                var otherArgs = mc.Arguments.Skip(1).ToArray();
                var allArgs = new[] { wrappedQuerySourceArg }.Concat(otherArgs).ToArray();
                return Expression.Call(mc.Method, allArgs);
            }
            throw new InvalidOperationException($"Unsupported query method node type '{oldQueryMethodNode.NodeType}'.");
        }

        protected virtual Type GetEnumerableEntityType(Type enumerableType)
        {
            if (enumerableType is null) return null;

            if (enumerableType.IsGenericType)
            {
                var genDef = enumerableType.GetGenericTypeDefinition();
                if (genDef == typeof(IQueryable<>) || genDef == typeof(IEnumerable<>))
                    return enumerableType.GetGenericArguments()[0];
            }

            var ienum = enumerableType.GetInterfaces()
                .FirstOrDefault(i =>
                    i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            return ienum?.GetGenericArguments()[0];
        }

        protected virtual bool TryResolveNavigation(MemberExpression navNode, out NavigationInfo navigation)
            => model.TryGetNavigation(navNode, out navigation);

        protected virtual bool IsSupportedNavigationType(NavigationType navigationType)
            => navigationType == NavigationType.ToParent
               || navigationType == NavigationType.ToParentOptional
               || navigationType == NavigationType.ToSingleChild;

        protected virtual bool TryExtractQueryMethodInjectionPoint(
            ParameterExpression parameterExpression,
            Expression parentExpression,
            NavigationInfo navigationInfo,
            out LambdaExpression parentExpressionLambda,
            out Expression queryMethod)
        {
            if (TryGetQueryMethodFromParameter(parameterExpression, out var firstTryQueryMethod))
            {
                Expression injectionPoint = firstTryQueryMethod;

                // Keep the older special-case behavior: if navigation is inside an aggregate,
                // try injecting at GroupBy instead of inside the aggregate call.
                if (firstTryQueryMethod is MethodCallExpression maybeAggregate && IsAggregateMethod(maybeAggregate))
                {
                    var better = GetInjectionPointForAggregate(parameterExpression, maybeAggregate);
                    if (better != null)
                        injectionPoint = better;
                }

                var newParentParam = Expression.Parameter(parameterExpression.Type, "s_p");
                var replacedParent = ExpressionReplacementVisitor.Replace(parameterExpression, newParentParam, parentExpression);
                parentExpressionLambda = Expression.Lambda(replacedParent, newParentParam);

                queryMethod = injectionPoint;
                return true;
            }

            parentExpressionLambda = null;
            queryMethod = null;
            return false;
        }

        protected virtual bool IsAggregateMethod(MethodCallExpression methodCall)
        {
            // Conservative default: typical Queryable aggregate names.
            // Consumers can override if they need a different definition.
            var name = methodCall.Method.Name;

            return name == nameof(Queryable.Sum)
                || name == nameof(Queryable.Average)
                || name == nameof(Queryable.Min)
                || name == nameof(Queryable.Max)
                || name == nameof(Queryable.Count)
                || name == nameof(Queryable.LongCount);
        }

        protected virtual Expression GetInjectionPointForAggregate(ParameterExpression parameterExpression, MethodCallExpression aggregateCall)
        {
            // same logic you had in UnitTest NavigateToOnePreprocessor.GetInjectionPoint()
            if (aggregateCall.Arguments.FirstOrDefault() is ParameterExpression parentParam)
            {
                if (TryGetQueryMethodFromParameter(parentParam, out var outer))
                {
                    if (outer is MethodCallExpression outerMc
                        && outerMc.Arguments.FirstOrDefault() is MethodCallExpression outerArg0Mc
                        && outerArg0Mc.Method.Name == nameof(Enumerable.GroupBy))
                    {
                        return outerArg0Mc;
                    }
                }
            }

            return null;
        }

        private bool TryGetQueryMethodFromParameter(ParameterExpression parameterExpression, out Expression queryMethod)
        {
            if (queryMethodStack.Count > 0)
            {
                foreach (var method in queryMethodStack)
                {
                    if (DoesParameterBelongToQueryMethod(parameterExpression, method))
                    {
                        queryMethod = method;
                        return true;
                    }
                }
            }

            queryMethod = null;
            return false;
        }

        private bool TryGetNavigationInfo(
            Expression node,
            IReadOnlyCollection<Expression> stackArray,
            out Expression parentExpression,
            out NavigationInfo navigationInfo)
        {
            // Handle:
            //  - x.NavProp
            //  - x.NavProp()  (when NavProp is Func<T>)
            var memberExpression = this.GetMemberExpression(node, stackArray);
            if (memberExpression != null
                && model.TryGetNavigation(memberExpression, out var nav)
                && IsSupportedNavigationType(nav.NavigationType))
            {
                parentExpression = GetParentExpression(node, stackArray); // important
                navigationInfo = nav;
                return true;
            }

            parentExpression = null;
            navigationInfo = null;
            return false;
        }


        private Expression GetParentExpression(Expression currentNode, IReadOnlyCollection<Expression> expressionStack)
        {
            return this.GetMemberExpression(currentNode, expressionStack)?.Expression;
        }

        private MemberExpression GetMemberExpression(Expression currentNode, IReadOnlyCollection<Expression> expressionStack)
        {
            var node = currentNode;
            if (node is MemberExpression memberExpression &&
                !(expressionStack.Skip(1).FirstOrDefault() is InvocationExpression))
            {
                // x.NavProp
                return memberExpression;
            }
            else if (node is InvocationExpression invocationExpression &&
                        invocationExpression.Expression is MemberExpression memberExpression2)
            {
                // x.NavProp()
                return memberExpression2;
            }
            return null;
        }

        private void CreateMetadataForNavigationJoin(Expression parentExpression, NavigationInfo navigationInfo)
        {
            var parameterExpression = ExtractParameter(parentExpression)
                ?? throw new InvalidOperationException($"{nameof(parentExpression)} is null");

            if (TryExtractQueryMethodInjectionPoint(parameterExpression, parentExpression, navigationInfo, out var parentExpressionLambda, out var queryMethod))
            {
                var parentBody = parentExpressionLambda.Body;
                var navigationKey = $"{parentBody}.{navigationInfo.PropertyName}";

                if (!navigationJoinMetadata.TryGetValue(queryMethod, out var meta))
                {
                    meta = new NavigationJoinMetadata(queryMethod);
                    navigationJoinMetadata.Add(queryMethod, meta);
                }

                if (meta.Navigations.Count == 0 || !meta.Navigations.ContainsKey(navigationKey))
                    meta.Navigations.Add(navigationKey, (parentExpressionLambda, navigationInfo));
            }
        }

        private Expression WrapFirstArgInNavigationJoinCall(NavigationJoinMetadata navigationMetadata, Expression queryMethodNode)
        {
            var querySourceArgument =
                GetQuerySourceArgumentFromQueryMethod(queryMethodNode)
                ??
                throw new InvalidOperationException($"Query source arg not extracted from '{queryMethodNode}'.");

            var querySourceEntityType =
                GetEnumerableEntityType(querySourceArgument.Type)
                ??
                throw new InvalidOperationException($"Query source entity type not extracted from '{querySourceArgument}'.");

            var wrappedQuerySource = querySourceArgument;

            var navigations = navigationMetadata.Navigations.Values.ToArray();
            for (var i = 0; i < navigations.Length; i++)
            {
                var navigation = navigations[i];
                var joinedDataSource = navigation.NavInfo.JoinedSource
                    ?? throw new InvalidOperationException("JoinedSource is null.");

                var sqlJoinType = GetJoinType(navigation.NavInfo);

                // preserve your "inner after left becomes left" rule
                if (i > 0 && sqlJoinType == SqlJoinType.Inner)
                {
                    var parentNavigation = navigations[i - 1];
                    var parentJoinType = GetJoinType(parentNavigation.NavInfo);
                    if (parentJoinType == SqlJoinType.Left || parentJoinType == SqlJoinType.OuterApply)
                        sqlJoinType = SqlJoinType.Left;
                }

                wrappedQuerySource = new NavigationJoinCallExpression(
                    wrappedQuerySource,
                    navigation.ParentExpression,
                    navigation.NavInfo.PropertyName,
                    joinedDataSource,
                    navigation.NavInfo.JoinCondition,
                    sqlJoinType,
                    navigation.NavInfo.NavigationType);
            }

            return CreateQueryMethodCall(queryMethodNode, wrappedQuerySource);
        }

        private SqlJoinType GetJoinType(NavigationInfo navigationInfo)
        {
            SqlJoinType joinType;
            switch (navigationInfo.NavigationType)
            {
                case NavigationType.ToParent:
                    joinType = SqlJoinType.Inner;
                    break;
                case NavigationType.ToParentOptional:
                    joinType = SqlJoinType.Left;
                    break;
                case NavigationType.ToSingleChild:
                    joinType = SqlJoinType.Left;
                    break;
                default:
                    throw new NotSupportedException($"Navigation type '{navigationInfo.NavigationType}' is not supported.");
            }

            if (navigationInfo.JoinCondition is null)
                joinType = SqlJoinType.OuterApply;

            return joinType;
        }

        private ParameterExpression ExtractParameter(Expression expression)
        {
            if (expression is NavigationMemberExpression navMember)
                return ExtractParameter(navMember.Expression);

            if (expression is MemberExpression memberExpression)
                return ExtractParameter(memberExpression.Expression);

            if (expression is ParameterExpression parameterExpression)
                return parameterExpression;

            // TODO: if we are getting this error and the navigation is on a variable
            // instead of ParameterExpression, then we'll modify the code so that 
            // we skip those cases.
            // E.g.
            //      var someEntity = loadEntityWithNavigationsSet();
            //      var query = table.Where(x => x.Filed1 == someEntity.NavParent().Field1).ToList();
            throw new InvalidOperationException("Expression does not contain a ParameterExpression.");
        }
    }
}
