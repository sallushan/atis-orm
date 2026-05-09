using Atis.SqlExpressionEngine;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.ExpressionExtensions;
using Atis.SqlExpressionEngine.SqlExpressions;
using Atis.SqlExpressionEngine.UnitTest.Metadata;
using Atis.SqlExpressionEngine.UnitTest.Preprocessors;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Atis.SqlExpressionEngine.UnitTest.Services
{
    internal class Model : Atis.SqlExpressionEngine.Services.Model
    {
        private readonly IReflectionService reflectionService;

        public Model(IReflectionService reflectionService)
        {
            this.reflectionService = reflectionService;
        }

        private IReadOnlyList<MemberInfo> GetColumnMembers(Type type)
        {
            return type.GetProperties()
                        .Where(x => x.GetCustomAttribute<NavigationPropertyAttribute>() == null &&
                                        x.GetCustomAttribute<CalculatedPropertyAttribute>() == null &&
                                        x.GetCustomAttribute<NavigationLinkAttribute>() == null)
                        .ToArray();
        }

        //public override IReadOnlyList<MemberInfo> GetPrimaryKeys(Type type)
        //{
        //    return this.GetColumnMembers(type)
        //                    .Where(x => x.GetCustomAttribute<PrimaryKeyAttribute>() != null)
        //                    .ToArray();
        //}

        //public override IReadOnlyList<TableColumn> GetTableColumns(Type type)
        //{
        //    return this.GetColumnMembers(type)
        //                    .Select(x => new TableColumn(x.GetCustomAttribute<DbColumnAttribute>()?.ColumnName ?? x.Name, x.Name))
        //                    .ToArray();
        //}

        private readonly Dictionary<Type, EntityMetadata> entityMetadataCache = new Dictionary<Type, EntityMetadata>();
        private readonly Dictionary<EntityMetadata, IDictionary<string, MemberInfo>> entityPropertiesCache = new Dictionary<EntityMetadata, IDictionary<string, MemberInfo>>();

        public override EntityMetadata GetEntity(Type type)
        {
            if (this.entityMetadataCache.TryGetValue(type, out var entityMetadata))
                return entityMetadata;
            //return base.GetEntity(type);
            var columnProperties = this.GetColumnMembers(type);
            var columns = columnProperties
                            .Select(x => new TableColumn(x.GetCustomAttribute<DbColumnAttribute>()?.ColumnName ?? x.Name, x.Name, isPrimaryKey: x.GetCustomAttribute<PrimaryKeyAttribute>() != null))
                            .ToArray();
            var navigationProperties = type.GetProperties()
                                                .Select(x => new { Prop = x, NavigationType = this.GetNavigationPropertyType(type, x) })
                                                .Where(x => x.NavigationType != NavigationPropertyType.None)
                                                .ToArray();
            Dictionary<string, NavigationInfo> navigations = new Dictionary<string, NavigationInfo>();
            foreach (var navigationProperty in navigationProperties)
            {
                if (this.TryGetNavigationInternal(type, navigationProperty.Prop, navigationProperty.NavigationType, out var navigationInfo))
                {
                    navigations.Add(navigationProperty.Prop.Name, navigationInfo);
                }
            }
            entityMetadata = new EntityMetadata(
                clrType: type,
                table: new SqlTable(type.Name),
                sqlColumns: columns,
                navigations: navigations
            );
            this.entityPropertiesCache.Add(entityMetadata, columnProperties.ToDictionary(x => x.Name, x => x));
            this.entityMetadataCache.Add(type, entityMetadata);
            return entityMetadata;
        }

        public override MemberInfo GetMember(EntityMetadata entity, TableColumn column)
        {
            if (entity is null)
                throw new ArgumentNullException(nameof(entity));
            if (column is null)
                throw new ArgumentNullException(nameof(column));
            if (this.entityPropertiesCache.TryGetValue(entity, out var columnProperties))
                return columnProperties.TryGetValue(column.ModelPropertyName, out var member) ? member : null;
            return null;
        }

        private enum NavigationPropertyType
        {
            None,
            EntityRelationClass,
            RelationAttribute
        }

        //public bool TryGetNavigation(MemberExpression memberExpression, out NavigationInfo navigationInfo)
        //{
        //    var member = this.ResolveMember(memberExpression);

        //    var modelType = memberExpression.Expression?.Type;
        //    var navigationPropertyType = this.GetNavigationPropertyType(memberExpression.Expression?.Type, memberExpression.Member);

        //    return this.TryGetNavigationInternal(modelType, member, navigationPropertyType, out navigationInfo);
        //}

        private bool TryGetNavigationInternal(Type modelType, MemberInfo member, NavigationPropertyType navigationPropertyType, out NavigationInfo navigationInfo)
        {
            if (navigationPropertyType != NavigationPropertyType.None)
            {
                switch (navigationPropertyType)
                {
                    case NavigationPropertyType.EntityRelationClass:
                        {
                            LambdaExpression relationLambda = null;
                            var (entityRelation, navigationType) = GetRelationAndTypeByEntityRelationClass(modelType, member);
                            // entityRelation.JoinExpression can be null for outer apply
                            if (entityRelation.JoinExpression != null)
                                relationLambda = entityRelation.JoinExpression as LambdaExpression
                                                        ?? (entityRelation.JoinExpression as UnaryExpression)?.Operand as LambdaExpression
                                                        ?? throw new InvalidOperationException("Invalid relation expression");
                            LambdaExpression otherDataSource;
                            Type joinedSourceParamType;
                            Type joinedSourceType;
                            if (navigationType == NavigationType.ToParent || navigationType == NavigationType.ToParentOptional)
                            {
                                otherDataSource = entityRelation.FromChildToParent();
                                joinedSourceParamType = entityRelation.ChildType;
                                joinedSourceType = entityRelation.ParentType;
                            }
                            else
                            {
                                otherDataSource = entityRelation.FromParentToChild();
                                joinedSourceParamType = entityRelation.ParentType;
                                joinedSourceType = entityRelation.ChildType;
                            }
                            if (otherDataSource is null)
                                throw new InvalidOperationException($"Unable to get navigation data source for navigation property {member.Name}");
                            //parentExpression = this.GetParentExpression(node, stackArray);
                            navigationInfo = new NavigationInfo(navigationType, relationLambda, otherDataSource ?? throw new InvalidOperationException("otherDataSource is null"), member.Name);
                            return true;
                        }
                    case NavigationPropertyType.RelationAttribute:
                        {
                            var (navigationType, relationLambda) = GetNavigationTypeAndRelationLambdaFromRelationAttribute(modelType, member, out var parentEntityType, out var childEntityType);
                            //parentExpression = this.GetParentExpression(node, stackArray);
                            Type joinedSourceParamType;
                            Type joinedSourceType;
                            if (navigationType == NavigationType.ToParent || navigationType == NavigationType.ToParentOptional)
                            {
                                joinedSourceParamType = childEntityType;
                                joinedSourceType = parentEntityType;
                            }
                            else
                            {
                                joinedSourceParamType = parentEntityType;
                                joinedSourceType = childEntityType;
                            }
                            navigationInfo = new NavigationInfo(navigationType, relationLambda, joinedSource: this.CreateJoinedDataSource(joinedSourceParamType, joinedSourceType), member.Name);
                            return true;
                        }
                    default:
                        throw new InvalidOperationException("Invalid navigation property");
                }
            }
            //parentExpression = null;
            navigationInfo = null;
            return false;
        }

        //private bool IsSupportedNavigationType(NavigationType navigationType)
        //{
        //    return navigationType == NavigationType.ToSingleChild || navigationType == NavigationType.ToParent || navigationType == NavigationType.ToParentOptional;
        //}

        private NavigationPropertyType GetNavigationPropertyType(Type? modelType, MemberInfo member)
        {
            var navPropAttribute = this.GetCustomAttribute<NavigationPropertyAttribute>(modelType, member);
            if (navPropAttribute != null)
                return NavigationPropertyType.EntityRelationClass;
            var relationAttribute = this.GetCustomAttribute<NavigationLinkAttribute>(modelType, member);
            if (relationAttribute != null)
                return NavigationPropertyType.RelationAttribute;
            return NavigationPropertyType.None;
        }

        private T GetCustomAttribute<T>(Type modelType, MemberInfo member) where T : Attribute
        {
            member = this.ResolveMemberInfo(modelType, member);
            return member.GetCustomAttribute<T>();
        }


        private MemberInfo ResolveMemberInfo(Type modelType, MemberInfo member)
        {
            MemberInfo resolvedMemberInfo = member;
            if (modelType != null && modelType != member.ReflectedType)
            {
                resolvedMemberInfo = this.reflectionService.GetPropertyOrField(modelType, member.Name)
                                        ??
                                        resolvedMemberInfo;
            }
            return resolvedMemberInfo;
        }


        private (IEntityRelation entityRelation, NavigationType navigationType) GetRelationAndTypeByEntityRelationClass(Type modelType, MemberInfo member)
        {
            var relationAttribute = this.GetCustomAttribute<NavigationPropertyAttribute>(modelType, member)
                                        ??
                                    throw new InvalidOperationException("Invalid navigation property");
            var relationType = relationAttribute.RelationType;
            var relation = Activator.CreateInstance(relationType) as IEntityRelation
                            ??
                            throw new InvalidOperationException("Invalid relation type");
            return (relation, relationAttribute.NavigationType);
        }

        //private MemberExpression GetMemberExpression(Expression navNode, IReadOnlyCollection<Expression> expressionStack)
        //{
        //    var node = navNode;
        //    if (node is MemberExpression memberExpression &&
        //        !(expressionStack.Skip(1).FirstOrDefault() is InvocationExpression))
        //    {
        //        // x.NavProp
        //        return memberExpression;
        //    }
        //    else if (node is InvocationExpression invocationExpression &&
        //                invocationExpression.Expression is MemberExpression memberExpression2)
        //    {
        //        // x.NavProp()
        //        return memberExpression2;
        //    }
        //    return null;
        //}


        //private MemberInfo ResolveMember(MemberExpression memberExpression)
        //{
        //    var resolvedMember = memberExpression.Member;
        //    if (memberExpression.Expression != null && memberExpression.Expression.Type != resolvedMember.ReflectedType)
        //    {
        //        resolvedMember = this.reflectionService.GetPropertyOrField(memberExpression.Expression.Type, resolvedMember.Name);
        //    }
        //    return resolvedMember;
        //}


        private (NavigationType navigationType, LambdaExpression relationLambda) GetNavigationTypeAndRelationLambdaFromRelationAttribute(Type modelType, MemberInfo member, out Type parentType, out Type childType)
        {
            var relationAttribute = this.GetCustomAttribute<NavigationLinkAttribute>(modelType, member)
                                        ?? throw new InvalidOperationException($"{nameof(NavigationLinkAttribute)} is not set on member '{member.Name}'.");

            var navigationType = relationAttribute.NavigationType;

            if (!(relationAttribute.ParentKeys?.Count >= 1 && relationAttribute.ForeignKeysInChild?.Count >= 1))
                throw new InvalidOperationException("ParentKeys or ForeignKeysInChild is not set.");

            if (relationAttribute.ParentKeys.Count != relationAttribute.ForeignKeysInChild.Count)
                throw new InvalidOperationException($"ParentKeys and ForeignKeysInChild must have the same number of elements.");

            Type childModelType = modelType ?? throw new InvalidOperationException($"ReflectedType property is null for member '{member.Name}'.");

            var parentModelType = (member as PropertyInfo ?? throw new InvalidOperationException("Member is not a property")).PropertyType
                                    ?? throw new InvalidOperationException($"PropertyType is null.");

            if (parentModelType.IsGenericType && parentModelType.GetGenericTypeDefinition() == typeof(Func<>))
            {
                parentModelType = parentModelType.GetGenericArguments()[0];
            }
            else if (this.reflectionService.IsEnumerableType(parentModelType))
            {
                // since we are using this method for both ToChildren and ToParent, therefore, the
                // navigation property can be defined as Func<T> or IQueryable<T>
                parentModelType = this.reflectionService.GetElementType(parentModelType);
            }

            if (navigationType == NavigationType.ToSingleChild || navigationType == NavigationType.ToChildren)
            {
                // swapping
                (childModelType, parentModelType) = (parentModelType, childModelType);
            }

            var parentParameter = Expression.Parameter(parentModelType, "auto_p");
            var childParameter = Expression.Parameter(childModelType, "auto_c");

            var parentKeys = relationAttribute.ParentKeys;
            var foreignKeysInChild = relationAttribute.ForeignKeysInChild;

            var joinConditions = parentKeys.Zip(foreignKeysInChild, (parentKey, foreignKey) =>
            {
                var parentProperty = parentModelType.GetProperty(parentKey)
                                    ?? throw new InvalidOperationException($"Property '{parentKey}' not found in '{parentModelType.Name}'.");

                var foreignProperty = childModelType.GetProperty(foreignKey)
                                    ?? throw new InvalidOperationException($"Property '{foreignKey}' not found in '{childModelType.Name}'.");

                return Expression.Equal(Expression.Property(parentParameter, parentProperty), Expression.Property(childParameter, foreignProperty));
            }).ToList();  // Convert to list to check count safely

            // Ensure there is at least one condition before calling Aggregate()
            if (joinConditions.Count == 0)
                throw new InvalidOperationException("No valid key mappings were found.");

            // Use Aggregate only when there are multiple conditions
            var joinExpression = joinConditions.Count == 1
                ? joinConditions[0]
                : joinConditions.Aggregate(Expression.AndAlso);

            var relationLambda = Expression.Lambda(joinExpression, parentParameter, childParameter);

            parentType = parentModelType;
            childType = childModelType;
            return (navigationType, relationLambda);
        }

        private readonly static MethodInfo createJoinedDataSourceOpenMethodInfo = typeof(Model).GetMethod(nameof(CreateJoinedDataSourceGen), BindingFlags.NonPublic | BindingFlags.Instance);

        private LambdaExpression CreateJoinedDataSource(Type parentType, Type joinedTable)
        {
            // here we will create the CreateJoinedDataSource<TParent>()
            var closedMethodInfo = createJoinedDataSourceOpenMethodInfo.MakeGenericMethod(parentType, joinedTable);
            // now we will call the method to get CreateJoinedDataSource
            var dataSourceExpression = (LambdaExpression)closedMethodInfo.Invoke(this, Array.Empty<object>());
            return dataSourceExpression;
        }

        /// <summary>
        /// Creates a joined data source expression for the specified parent type.
        /// </summary>
        /// <typeparam name="TParent">The type of the parent entity.</typeparam>
        /// <returns>The joined data source expression.</returns>
        private Expression<Func<TParent, IQueryable<TJoined>>> CreateJoinedDataSourceGen<TParent, TJoined>()
        {
            //var queryProvider = this.GetQueryProvider();
            //return qs_param => QueryExtensions.DataSet<TJoined>(queryProvider);
            var qs_param = Expression.Parameter(typeof(TParent), "qs_param");
            var queryRootExpression = new QueryRootExpression(typeof(TJoined));
            var lambda = Expression.Lambda<Func<TParent, IQueryable<TJoined>>>(queryRootExpression, qs_param);
            return lambda;
        }
    }
}
