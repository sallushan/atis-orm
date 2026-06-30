using Atis.Orm.Annotations;
using Atis.SqlExpressionEngine;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.ExpressionExtensions;
using Atis.SqlExpressionEngine.Services;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using Atis.Orm.Abstractions;
namespace Atis.Orm.Metadata
{
    public enum NavigationPropertyType
    {
        None,
        EntityRelationClass,
        RelationAttribute
    }

    /// <inheritdoc />
    public class EntityMetadataBuilder : IEntityMetadataBuilder
    {
        private readonly static MethodInfo createJoinedDataSourceOpenMethodInfo = typeof(EntityMetadataBuilder).GetMethod(nameof(CreateJoinedDataSourceGen), BindingFlags.NonPublic | BindingFlags.Instance)
                                                                                    ??
                                                                                    throw new InvalidOperationException($"Failed to get method info for {nameof(CreateJoinedDataSourceGen)}");
        private readonly IReflectionService reflectionService;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="reflectionService"></param>
        public EntityMetadataBuilder(IReflectionService reflectionService)
        {
            this.reflectionService = reflectionService;
        }

        /// <inheritdoc />
        public EntityMetadata Build(Type type)
        {
            var columnProperties = this.GetColumnMembers(type);
            var columns = columnProperties
                            .Select(x => new TableColumn(this.GetColumnName(x), x.Name, isPrimaryKey: x.GetCustomAttribute<PrimaryKeyAttribute>() != null))
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
            Dictionary<string, LambdaExpression> calculatedProperties = new Dictionary<string, LambdaExpression>();
            var calculatedPropertiesArray = type.GetProperties()
                                            .Where(x => this.IsCalculatedProperty(x))
                                            .ToArray();
            foreach (var calcProperty in calculatedPropertiesArray)
            {
                if (this.TryGetCalculatedPropertyExpression(type, calcProperty, out var exprPropertyValue))
                    calculatedProperties.Add(calcProperty.Name, exprPropertyValue);
                
            }

            var entityMetadata = new EntityMetadata(
                clrType: type,
                table: this.GetSqlTable(type),
                sqlColumns: columns,
                navigations: navigations,
                calculatedProperties: calculatedProperties
            );
            return entityMetadata;
        }

        protected virtual SqlTable GetSqlTable(Type type)
        {
            var dbTableAttribute = type.GetCustomAttribute<DbTableAttribute>();
            return new SqlTable(dbTableAttribute?.TableName ?? type.Name, dbTableAttribute?.Schema, dbTableAttribute?.Database, dbTableAttribute?.Server);
        }

        protected virtual bool IsCalculatedProperty(PropertyInfo propertyInfo)
            => propertyInfo.GetCustomAttribute<CalculatedPropertyAttribute>() != null;

        protected virtual bool TryGetCalculatedPropertyExpression(Type type, PropertyInfo calcProperty, out LambdaExpression expression)
        {
            var calcAttr = calcProperty.GetCustomAttribute<CalculatedPropertyAttribute>();
            var exprPropName = calcAttr.ExpressionPropertyName;
            var exprProperty = type.GetMember(exprPropName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).FirstOrDefault();
            if (exprProperty != null)
            {
                if (this.reflectionService.GetPropertyOrFieldValue(instance: null, exprProperty) is LambdaExpression exprPropertyValue)
                {
                    expression = exprPropertyValue;
                    return true;
                }
            }
            expression = null;
            return false;
        }

        protected virtual bool IsSchemaRelatedProperty(PropertyInfo propertyInfo)
            => propertyInfo.GetCustomAttribute<NavigationPropertyAttribute>() == null &&
                                        propertyInfo.GetCustomAttribute<CalculatedPropertyAttribute>() == null &&
                                        propertyInfo.GetCustomAttribute<NavigationLinkAttribute>() == null;

        protected virtual string GetColumnName(PropertyInfo propertyInfo)
            => propertyInfo.GetCustomAttribute<DbColumnAttribute>()?.ColumnName ?? propertyInfo.Name;

        private IReadOnlyList<PropertyInfo> GetColumnMembers(Type type)
        {
            return type.GetProperties()
                        .Where(x => IsSchemaRelatedProperty(x))
                        .ToArray();
        }

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

        protected virtual NavigationPropertyType GetNavigationPropertyType(Type modelType, MemberInfo member)
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
