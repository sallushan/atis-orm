using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Atis.Orm
{
    public class ElementFactoryBuilder : IElementFactoryBuilder
    {
        public Func<IDataReader, object> CreateElementFactory(Expression expression, SqlExpression sqlExpression)
        {
            if (sqlExpression is SqlDerivedTableExpression derivedTable)
            {
                return CreateElementFactoryForDerivedTable(expression, derivedTable);
            }

            throw new NotSupportedException($"SQL expression of type {sqlExpression.GetType().Name} is not supported.");
        }

        private Func<IDataReader, object> CreateElementFactoryForDerivedTable(Expression expression, SqlDerivedTableExpression derivedTable)
        {
            var elementType = GetElementType(expression.Type);
            var queryShape = derivedTable.QueryShape;
            var selectColumns = derivedTable.SelectColumnCollection.SelectColumns;

            // Build ordinal map: SqlExpression -> index in SelectColumns
            var ordinalMap = BuildOrdinalMap(selectColumns);

            // Create the element factory based on QueryShape type
            var typedFactory = CreateTypedElementFactory(elementType, queryShape, ordinalMap);

            return typedFactory;
        }

        private Dictionary<SqlExpression, int> BuildOrdinalMap(IReadOnlyList<SelectColumn> selectColumns)
        {
            var map = new Dictionary<SqlExpression, int>(ReferenceEqualityComparer.Instance);
            for (int i = 0; i < selectColumns.Count; i++)
            {
                map[selectColumns[i].ColumnExpression] = i;
            }
            return map;
        }

        private Func<IDataReader, object> CreateTypedElementFactory(Type elementType, SqlExpression queryShape, Dictionary<SqlExpression, int> ordinalMap)
        {
            // Parameter: IDataReader dr
            var drParam = Expression.Parameter(typeof(IDataReader), "dr");

            // Build the body expression based on QueryShape type
            var bodyExpression = BuildReadExpression(elementType, queryShape, ordinalMap, drParam);

            // Convert to object if necessary
            var objectBody = Expression.Convert(bodyExpression, typeof(object));

            // Compile: dr => (object)bodyExpression
            var lambda = Expression.Lambda<Func<IDataReader, object>>(objectBody, drParam);

            System.Diagnostics.Debug.WriteLine($"Element Factory Expression for {elementType.Name}: {lambda}");

            return lambda.Compile();
        }

        private Expression BuildReadExpression(Type targetType, SqlExpression queryShape, Dictionary<SqlExpression, int> ordinalMap, ParameterExpression drParam)
        {
            // Case 1: Scalar - direct column read (SqlDataSourceColumnExpression, SqlFunctionCallExpression, etc.)
            if (IsScalarShape(queryShape))
            {
                return BuildScalarReadExpression(targetType, queryShape, ordinalMap, drParam);
            }

            // Case 2: SqlDataSourceQueryShapeExpression - unwrap and process ShapeExpression
            if (queryShape is SqlDataSourceQueryShapeExpression dsQueryShape)
            {
                return BuildReadExpression(targetType, dsQueryShape.ShapeExpression, ordinalMap, drParam);
            }

            // Case 3: SqlMemberInitExpression - complex object
            if (queryShape is SqlMemberInitExpression memberInit)
            {
                return BuildComplexReadExpression(targetType, memberInit, ordinalMap, drParam);
            }

            throw new NotSupportedException($"QueryShape of type {queryShape.GetType().Name} is not supported.");
        }

        private bool IsScalarShape(SqlExpression queryShape)
        {
            // Scalar shapes are leaf expressions that map directly to a single column
            return (queryShape as SqlQueryShapeExpression)?.IsScalar == true || 
                    !(queryShape is SqlQueryShapeExpression);
        }

        private Expression BuildScalarReadExpression(Type targetType, SqlExpression queryShape, Dictionary<SqlExpression, int> ordinalMap, ParameterExpression drParam)
        {
            // For scalar, we need to find the ordinal
            // The queryShape itself should be in the ordinalMap, or it's wrapped
            if (!ordinalMap.TryGetValue(queryShape, out int ordinal))
            {
                // If not found directly, it might be the first column (for Count, Max, etc.)
                ordinal = 0;
            }

            return BuildColumnReadExpression(targetType, ordinal, drParam);
        }

        private Expression BuildComplexReadExpression(Type targetType, SqlMemberInitExpression memberInit, Dictionary<SqlExpression, int> ordinalMap, ParameterExpression drParam)
        {
            // Check if parameterless constructor exists
            var parameterlessConstructor = targetType.GetConstructor(Type.EmptyTypes);

            if (parameterlessConstructor != null)
            {
                // Use property/field setters with MemberInit
                return BuildMemberInitExpression(targetType, memberInit, ordinalMap, drParam);
            }
            else
            {
                // Anonymous type or no parameterless constructor - use constructor with parameters
                return BuildConstructorExpression(targetType, memberInit, ordinalMap, drParam);
            }
        }

        private Expression BuildMemberInitExpression(Type targetType, SqlMemberInitExpression memberInit, Dictionary<SqlExpression, int> ordinalMap, ParameterExpression drParam)
        {
            // new T { Prop1 = ..., Prop2 = ... }
            var newExpr = Expression.New(targetType);
            var bindings = new List<MemberBinding>();

            foreach (var sqlBinding in memberInit.Bindings)
            {
                var memberName = sqlBinding.MemberName;
                var memberInfo = GetMemberInfo(targetType, memberName);

                if (memberInfo == null)
                {
                    // Member not found on target type - skip (might be non-projectable)
                    continue;
                }

                var memberType = GetMemberType(memberInfo);
                var valueExpr = BuildReadExpression(memberType, sqlBinding.SqlExpression, ordinalMap, drParam);

                bindings.Add(Expression.Bind(memberInfo, valueExpr));
            }

            return Expression.MemberInit(newExpr, bindings);
        }

        private Expression BuildConstructorExpression(Type targetType, SqlMemberInitExpression memberInit, Dictionary<SqlExpression, int> ordinalMap, ParameterExpression drParam)
        {
            // For anonymous types, find constructor and match parameters by name
            var constructors = targetType.GetConstructors();
            if (constructors.Length != 1)
            {
                throw new InvalidOperationException($"Type {targetType.Name} must have exactly one constructor for anonymous type handling.");
            }

            var constructor = constructors[0];
            var parameters = constructor.GetParameters();
            var arguments = new Expression[parameters.Length];

            foreach (var param in parameters)
            {
                // Find matching binding by parameter name (case-insensitive)
                var binding = memberInit.Bindings
                    .FirstOrDefault(b => string.Equals(b.MemberName, param.Name, StringComparison.OrdinalIgnoreCase));

                if (binding == null)
                {
                    throw new InvalidOperationException($"No binding found for constructor parameter '{param.Name}' on type {targetType.Name}.");
                }

                var valueExpr = BuildReadExpression(param.ParameterType, binding.SqlExpression, ordinalMap, drParam);
                arguments[param.Position] = valueExpr;
            }

            return Expression.New(constructor, arguments);
        }

        private Expression BuildColumnReadExpression(Type targetType, int ordinal, ParameterExpression drParam)
        {
            // Build: dr.IsDBNull(ordinal) ? default(T) : (T)dr.GetValue(ordinal)
            // Or use specific GetXxx methods for better performance

            var ordinalExpr = Expression.Constant(ordinal);

            // dr.IsDBNull(ordinal)
            var isDbNullMethod = typeof(IDataRecord).GetMethod(nameof(IDataRecord.IsDBNull));
            var isDbNullCall = Expression.Call(drParam, isDbNullMethod, ordinalExpr);

            // default(T)
            var defaultExpr = Expression.Default(targetType);

            // dr.GetValue(ordinal) or specific GetXxx method
            var getValueExpr = BuildGetValueExpression(targetType, ordinal, drParam);

            // dr.IsDBNull(ordinal) ? default(T) : getValue
            return Expression.Condition(isDbNullCall, defaultExpr, getValueExpr);
        }

        private Expression BuildGetValueExpression(Type targetType, int ordinal, ParameterExpression drParam)
        {
            var ordinalExpr = Expression.Constant(ordinal);
            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            // Try to use specific GetXxx methods for common types
            string methodName = GetDataReaderMethodName(underlyingType);

            if (methodName != null)
            {
                var method = typeof(IDataRecord).GetMethod(methodName);
                var callExpr = Expression.Call(drParam, method, ordinalExpr);

                // If target is nullable, we need to convert
                if (targetType != underlyingType)
                {
                    return Expression.Convert(callExpr, targetType);
                }

                return callExpr;
            }

            // Fallback: use GetValue and cast
            var getValueMethod = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetValue));
            var getValueCall = Expression.Call(drParam, getValueMethod, ordinalExpr);
            return Expression.Convert(getValueCall, targetType);
        }

        private string GetDataReaderMethodName(Type type)
        {
            if (type == typeof(bool)) return nameof(IDataRecord.GetBoolean);
            if (type == typeof(byte)) return nameof(IDataRecord.GetByte);
            if (type == typeof(char)) return nameof(IDataRecord.GetChar);
            if (type == typeof(DateTime)) return nameof(IDataRecord.GetDateTime);
            if (type == typeof(decimal)) return nameof(IDataRecord.GetDecimal);
            if (type == typeof(double)) return nameof(IDataRecord.GetDouble);
            if (type == typeof(float)) return nameof(IDataRecord.GetFloat);
            if (type == typeof(Guid)) return nameof(IDataRecord.GetGuid);
            if (type == typeof(short)) return nameof(IDataRecord.GetInt16);
            if (type == typeof(int)) return nameof(IDataRecord.GetInt32);
            if (type == typeof(long)) return nameof(IDataRecord.GetInt64);
            if (type == typeof(string)) return nameof(IDataRecord.GetString);
            return null; // Use GetValue fallback
        }

        private MemberInfo GetMemberInfo(Type type, string memberName)
        {
            // Try property first
            var property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property != null) return property;

            // Try field
            var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            return field;
        }

        private Type GetMemberType(MemberInfo memberInfo)
        {
            if (memberInfo is PropertyInfo prop)
                return prop.PropertyType;
            else if (memberInfo is FieldInfo field)
                return field.FieldType;
            else
                throw new InvalidOperationException($"Unsupported member type: {memberInfo.GetType().Name}");
        }

        private Type GetElementType(Type type)
        {
            // Handle IQueryable<T>, IEnumerable<T>, Task<T>, etc.
            if (type.IsGenericType)
            {
                var genericDef = type.GetGenericTypeDefinition();

                if (genericDef == typeof(IQueryable<>) ||
                    genericDef == typeof(IEnumerable<>) ||
                    genericDef == typeof(IAsyncEnumerable<>) ||
                    genericDef == typeof(Task<>))
                {
                    return type.GetGenericArguments()[0];
                }
            }

            // If it's IQueryable or IEnumerable (non-generic), try to find element type
            if (typeof(IQueryable).IsAssignableFrom(type) || typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
            {
                var interfaces = type.GetInterfaces();
                var genericEnumerable = interfaces.FirstOrDefault(i =>
                    i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
                if (genericEnumerable != null)
                {
                    return genericEnumerable.GetGenericArguments()[0];
                }
            }

            // Return the type itself (for scalar results like int, string, etc.)
            return type;
        }

        /// <summary>
        /// Reference equality comparer for SqlExpression instances
        /// </summary>
        private class ReferenceEqualityComparer : IEqualityComparer<SqlExpression>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            public bool Equals(SqlExpression x, SqlExpression y) => ReferenceEquals(x, y);
            public int GetHashCode(SqlExpression obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}