using Atis.SqlExpressionEngine.Internal;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

using Atis.Orm.Abstractions;
using Atis.Orm.DataAccess;
using Atis.Orm.Querying;
namespace Atis.Orm.Services
{
    public class ElementFactoryBuilder : IElementFactoryBuilder
    {
        public Func<IDataReader, object> CreateElementFactory(Expression expression, SqlExpression sqlExpression)
        {
            if (sqlExpression is SqlDerivedTableExpression derivedTable)
            {
                return CreateElementFactoryForDerivedTable(expression, derivedTable);
            }
            else if (sqlExpression is SqlUpdateExpression updateExpression)
            {
                if (updateExpression.Outputs.Count == 0)
                    throw new InvalidOperationException($"{nameof(sqlExpression)} of type {nameof(SqlUpdateExpression)} must have at least one output column to create an element factory.");
                return this.CreateElementFactoryForDataManipulationOutput(expression, updateExpression.Outputs, "update");
            }
            else if (sqlExpression is SqlInsertExpression insertExpression)
            {
                if (insertExpression.Outputs.Count == 0)
                    throw new InvalidOperationException($"{nameof(sqlExpression)} of type {nameof(SqlInsertExpression)} must have at least one output column to create an element factory.");
                return this.CreateElementFactoryForDataManipulationOutput(expression, insertExpression.Outputs, "insert");
            }

            throw new NotSupportedException($"SQL expression of type {sqlExpression.GetType().Name} is not supported.");
        }

        /// <summary>
        ///     <para>
        ///         Reads back the OUTPUT clause of an INSERT or an UPDATE. Both carry the same thing —
        ///         a list of output columns and no query shape — so they materialize the same way.
        ///     </para>
        /// </summary>
        private Func<IDataReader, object> CreateElementFactoryForDataManipulationOutput(
            Expression expression,
            IReadOnlyList<SelectColumn> outputs,
            string operation)
        {
            // The element type the caller asked for decides the shape, exactly as it does for a
            // derived table below. Asking whether a dictionary row satisfies it, rather than
            // matching one exact closed generic, keeps this from being a private handshake with
            // whichever call site happened to pick that type.
            var elementType = GetElementType(expression.Type);
            if (elementType.IsAssignableFrom(typeof(Dictionary<string, object>)))
            {
                // The OUTPUT aliases are the column names, known here rather than discovered from the
                // reader — so a duplicate is caught once, now, instead of silently dropping a value on
                // every row. Everything past that is the same shape ExecuteDictionary produces.
                var columnNames = outputs.Select(x => x.Alias).ToArray();
                var keyComparer = DictionaryRow.DefaultKeyComparer;
                DictionaryRow.EnsureNoDuplicateColumns(columnNames, keyComparer);
                return dr => DictionaryRow.Read(dr, columnNames, keyComparer);
            }

            // Materializing an OUTPUT clause into a typed shape needs a QueryShape to map members onto
            // ordinals, and the shape-building path below takes it from a SqlDerivedTableExpression.
            // These statements have Outputs (which give the ordinals) but no shape, and their source is
            // the wrong one to borrow -- the OUTPUT list can select different columns than the query
            // does. The converter would have to build the output projection's shape alongside Outputs
            // and carry it on the statement.
            throw new NotSupportedException(
                $"An {operation} with an OUTPUT clause can currently only be materialized as " +
                $"{nameof(Dictionary<string, object>)}<string, object>, not as '{elementType.Name}'.");
        }

        /// <inheritdoc />
        public ChildAwareElementFactory CreateChildAwareElementFactory(Expression expression, SqlDerivedTableExpression parentQuery, IReadOnlyList<ChildQuery> children)
        {
            if (parentQuery is null)
                throw new ArgumentNullException(nameof(parentQuery));
            if (children is null)
                throw new ArgumentNullException(nameof(children));

            var elementType = GetElementType(expression.Type);
            var ordinalMap = BuildOrdinalMap(parentQuery.SelectColumnCollection.SelectColumns);
            var childContext = new ChildReadContext(children, Expression.Parameter(typeof(ChildCollections), "children"), ordinalMap, this);

            var drParam = Expression.Parameter(typeof(IDataReader), "dr");
            var body = BuildReadExpression(elementType, parentQuery.QueryShape, ordinalMap, drParam, childContext);

            // The link values are read first, once each and in ascending order: the reader may be sequential, and the
            // collections have to be known before the object is constructed from the columns that follow them.
            var readKeys = Expression.Assign(childContext.KeyValuesVariable,
                                    Expression.Call(typeof(ChildCollections).GetMethod(nameof(ChildCollections.ReadKeyValues)), drParam, Expression.Constant(childContext.KeyOrdinals)));
            var block = Expression.Block(typeof(object), new[] { childContext.KeyValuesVariable }, readKeys, Expression.Convert(body, typeof(object)));
            var lambda = Expression.Lambda<Func<IDataReader, ChildCollections, object>>(block, drParam, childContext.CollectionsParameter);

            System.Diagnostics.Debug.WriteLine($"Child aware element factory expression for {elementType.Name}: {lambda}");

            return new ChildAwareElementFactory(lambda.Compile(), childContext.RowFactories);
        }

        /// <summary>
        ///     Everything the shape walk needs to turn a sub-query column into a read of the child collection of the
        ///     current parent row, and what it produced: the row factory of every child query it met.
        /// </summary>
        private sealed class ChildReadContext
        {
            private readonly ElementFactoryBuilder builder;
            private readonly Dictionary<SqlExpression, int> parentOrdinalMap;

            public ChildReadContext(IReadOnlyList<ChildQuery> children, ParameterExpression collectionsParameter, Dictionary<SqlExpression, int> parentOrdinalMap, ElementFactoryBuilder builder)
            {
                this.Children = children;
                this.CollectionsParameter = collectionsParameter;
                this.parentOrdinalMap = parentOrdinalMap;
                this.builder = builder;
                this.RowFactories = new Func<IDataReader, object>[children.Count];
                this.KeyValuesVariable = Expression.Variable(typeof(object[]), "keyValues");
                this.KeyOrdinals = children
                                    .SelectMany(x => x.ParentKeyColumns)
                                    .Select(this.GetParentOrdinal)
                                    .Distinct()
                                    .OrderBy(x => x)
                                    .ToArray();
            }

            public IReadOnlyList<ChildQuery> Children { get; }
            public ParameterExpression CollectionsParameter { get; }
            public Func<IDataReader, object>[] RowFactories { get; }

            /// <summary>Holds the link values of the current parent row, read once before anything else of the row.</summary>
            public ParameterExpression KeyValuesVariable { get; }

            /// <summary>The ordinals of every link column of every child query, ascending: what <see cref="KeyValuesVariable"/> is read from.</summary>
            public int[] KeyOrdinals { get; }

            private int GetParentOrdinal(SqlExpression column)
            {
                if (!this.parentOrdinalMap.TryGetValue(column, out var ordinal))
                    throw new InvalidOperationException("A link column of a child query is not in the select list of the parent query. This is a bug in the query splitting.");
                return ordinal;
            }

            public Expression BuildCollectionRead(Type memberType, SqlQueryableExpression source, ParameterExpression drParam)
            {
                var childIndex = -1;
                for (var i = 0; i < this.Children.Count; i++)
                {
                    if (ReferenceEquals(this.Children[i].Source, source))
                    {
                        childIndex = i;
                        break;
                    }
                }
                if (childIndex < 0)
                    throw new InvalidOperationException("A sub-query selected in the projection has no child query. This is a bug in the query splitting.");

                var elementType = GetCollectionElementType(memberType);
                if (elementType is null)
                    throw new NotSupportedException($"A sub-query selected in the projection is loaded as a separate query and its rows can only be assigned to a member typed as a List<T>, IList<T>, ICollection<T>, IReadOnlyList<T>, IEnumerable<T> or an array, but the member is '{memberType}'. A member typed IQueryable<T> is not materialized: select the sub-query with ToList() or ToArray().");

                var child = this.Children[childIndex];
                this.RowFactories[childIndex] = this.builder.CreateChildRowFactory(child, elementType);

                var keyPositions = child.ParentKeyColumns
                                        .Select(column => Array.IndexOf(this.KeyOrdinals, this.GetParentOrdinal(column)))
                                        .ToArray();

                var key = Expression.Call(typeof(ChildCollections).GetMethod(nameof(ChildCollections.MakeKey)), this.KeyValuesVariable, Expression.Constant(keyPositions));
                var getMethodName = memberType.IsArray ? nameof(ChildCollections.GetArray) : nameof(ChildCollections.GetList);
                var get = Expression.Call(this.CollectionsParameter, typeof(ChildCollections).GetMethod(getMethodName).MakeGenericMethod(elementType), Expression.Constant(childIndex), key);
                return get.Type == memberType ? (Expression)get : Expression.Convert(get, memberType);
            }

            /// <summary>
            ///     The element type if <paramref name="memberType"/> is something a list or an array can be assigned to.
            ///     <c>IQueryable&lt;T&gt;</c> is deliberately not one of them.
            /// </summary>
            private static Type GetCollectionElementType(Type memberType)
            {
                if (memberType.IsArray)
                    return memberType.GetElementType();
                if (!memberType.IsGenericType)
                    return null;
                var definition = memberType.GetGenericTypeDefinition();
                if (definition == typeof(List<>) ||
                    definition == typeof(IList<>) ||
                    definition == typeof(ICollection<>) ||
                    definition == typeof(IReadOnlyList<>) ||
                    definition == typeof(IReadOnlyCollection<>) ||
                    definition == typeof(IEnumerable<>))
                    return memberType.GetGenericArguments()[0];
                return null;
            }
        }

        /// <summary>
        ///     Reads one row of a child query as a <see cref="ChildRow"/>: the child item, read the way any query of that
        ///     element type is read, and the link value, read from the key members of the child query's shape.
        /// </summary>
        private Func<IDataReader, object> CreateChildRowFactory(ChildQuery child, Type itemType)
        {
            var query = child.Query;
            var ordinalMap = BuildOrdinalMap(query.SelectColumnCollection.SelectColumns);
            var shape = (query.QueryShape as SqlMemberInitExpression)
                        ??
                        (query.QueryShape as SqlDataSourceQueryShapeExpression)?.ShapeExpression as SqlMemberInitExpression
                        ??
                        throw new InvalidOperationException($"The query shape of a child query is not a {nameof(SqlMemberInitExpression)}.");

            var drParam = Expression.Parameter(typeof(IDataReader), "dr");
            var itemBinding = shape.Bindings.Single(x => x.MemberName == ChildQuery.ItemMemberName);
            var item = BuildReadExpression(itemType, itemBinding.SqlExpression, ordinalMap, drParam);

            var keyOrdinals = shape.Bindings
                                    .Where(x => x.MemberName.StartsWith(ChildQuery.KeyMemberNamePrefix, StringComparison.Ordinal))
                                    .OrderBy(x => int.Parse(x.MemberName.Substring(ChildQuery.KeyMemberNamePrefix.Length)))
                                    .Select(x => ordinalMap.TryGetValue(x.SqlExpression, out var ordinal)
                                                    ? ordinal
                                                    : throw new InvalidOperationException("A link column of a child query is not in its select list. This is a bug in the query splitting."))
                                    .ToArray();
            // The item is the first argument and the key the second: the reader may be sequential, and the item's
            // columns come before the key's in the select list, so they must be read before it.
            var keyValues = Expression.Call(typeof(ChildCollections).GetMethod(nameof(ChildCollections.ReadKeyValues)), drParam, Expression.Constant(keyOrdinals));
            var key = Expression.Call(typeof(ChildCollections).GetMethod(nameof(ChildCollections.MakeKey)), keyValues, Expression.Constant(Enumerable.Range(0, keyOrdinals.Length).ToArray()));
            var row = Expression.New(typeof(ChildRow).GetConstructors().Single(), Expression.Convert(item, typeof(object)), key);
            return Expression.Lambda<Func<IDataReader, object>>(row, drParam).Compile();
        }

        private Func<IDataReader, object> CreateElementFactoryForDerivedTable(Expression expression, SqlDerivedTableExpression derivedTable)
        {
            var elementType = GetElementType(expression.Type);
            var queryShape = derivedTable.QueryShape;
            var selectColumns = derivedTable.SelectColumnCollection.SelectColumns;

            // A dictionary row satisfies the element type, so the caller asked for names rather than
            // a type -- which is what SelectFields is for, and the only way to project a column list
            // that is not known until run time. The aliases are known here at plan time, exactly as
            // they are for an OUTPUT clause above, so the same pair of DictionaryRow calls applies
            // and every dictionary path keeps one set of rules for casing, nulls and duplicates.
            if (elementType.IsAssignableFrom(typeof(Dictionary<string, object>)))
            {
                var columnNames = selectColumns.Select(x => x.Alias).ToArray();
                var keyComparer = DictionaryRow.DefaultKeyComparer;
                DictionaryRow.EnsureNoDuplicateColumns(columnNames, keyComparer);
                return dr => DictionaryRow.Read(dr, columnNames, keyComparer);
            }

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

        private Expression BuildReadExpression(Type targetType, SqlExpression queryShape, Dictionary<SqlExpression, int> ordinalMap, ParameterExpression drParam, ChildReadContext childContext = null)
        {
            // Case 0: a sub-query selected in the projection, loaded by its own query
            if (queryShape is SqlQueryableExpression queryable)
            {
                if (childContext is null)
                    throw new NotSupportedException($"A sub-query selected in the projection cannot be materialized here: its rows are loaded by a separate query, which this query does not have. Member type is '{targetType}'.");
                return childContext.BuildCollectionRead(targetType, queryable, drParam);
            }

            // Case 1: Scalar - direct column read (SqlDataSourceColumnExpression, SqlFunctionExpression, etc.)
            if (IsScalarShape(queryShape))
            {
                return BuildScalarReadExpression(targetType, queryShape, ordinalMap, drParam);
            }

            // Case 2: SqlDataSourceQueryShapeExpression - unwrap and process ShapeExpression
            if (queryShape is SqlDataSourceQueryShapeExpression dsQueryShape)
            {
                return BuildReadExpression(targetType, dsQueryShape.ShapeExpression, ordinalMap, drParam, childContext);
            }

            // Case 3: SqlMemberInitExpression - complex object
            if (queryShape is SqlMemberInitExpression memberInit)
            {
                return BuildComplexReadExpression(targetType, memberInit, ordinalMap, drParam, childContext);
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

        private Expression BuildComplexReadExpression(Type targetType, SqlMemberInitExpression memberInit, Dictionary<SqlExpression, int> ordinalMap, ParameterExpression drParam, ChildReadContext childContext)
        {
            // Check if parameterless constructor exists
            var parameterlessConstructor = targetType.GetConstructor(Type.EmptyTypes);

            if (parameterlessConstructor != null)
            {
                // Use property/field setters with MemberInit
                return BuildMemberInitExpression(targetType, memberInit, ordinalMap, drParam, childContext);
            }
            else
            {
                // Anonymous type or no parameterless constructor - use constructor with parameters
                return BuildConstructorExpression(targetType, memberInit, ordinalMap, drParam, childContext);
            }
        }

        private Expression BuildMemberInitExpression(Type targetType, SqlMemberInitExpression memberInit, Dictionary<SqlExpression, int> ordinalMap, ParameterExpression drParam, ChildReadContext childContext)
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
                var valueExpr = BuildReadExpression(memberType, sqlBinding.SqlExpression, ordinalMap, drParam, childContext);

                bindings.Add(Expression.Bind(memberInfo, valueExpr));
            }

            return Expression.MemberInit(newExpr, bindings);
        }

        private Expression BuildConstructorExpression(Type targetType, SqlMemberInitExpression memberInit, Dictionary<SqlExpression, int> ordinalMap, ParameterExpression drParam, ChildReadContext childContext)
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

                var valueExpr = BuildReadExpression(param.ParameterType, binding.SqlExpression, ordinalMap, drParam, childContext);
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
