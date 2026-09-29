using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Atis.Orm.Metadata
{
    /// <summary>
    ///     <para>
    ///         The route from an entity to the property a column reads and writes: a single property for an
    ///         ordinary column, or a property of a value object for a column that belongs to one
    ///         (<c>ZonedTime.ZoneCode</c>).
    ///     </para>
    ///     <para>
    ///         <see cref="Name"/> is the dotted form, which is the key that pairs a <see cref="CrudColumn"/>
    ///         with its <see cref="Atis.SqlExpressionEngine.SqlExpressions.TableColumn"/>.
    ///     </para>
    /// </summary>
    public sealed class ColumnPath : IEquatable<ColumnPath>
    {
        /// <summary>Constructs a path over <paramref name="segments"/>, outermost property first.</summary>
        public ColumnPath(IReadOnlyList<PropertyInfo> segments)
        {
            if (segments is null || segments.Count == 0)
                throw new ArgumentException("A column path needs at least one property.", nameof(segments));
            this.Segments = segments;
            this.Name = string.Join(".", segments.Select(x => x.Name));
            this.Alias = string.Join("_", segments.Select(x => x.Name));
        }

        /// <summary>A path of one property.</summary>
        public ColumnPath(PropertyInfo property) : this(new[] { property ?? throw new ArgumentNullException(nameof(property)) })
        {
        }

        /// <summary>The properties to walk, outermost first.</summary>
        public IReadOnlyList<PropertyInfo> Segments { get; }

        /// <summary>The property the column finally reads and writes.</summary>
        public PropertyInfo Leaf => this.Segments[this.Segments.Count - 1];

        /// <summary>The property directly on the entity.</summary>
        public PropertyInfo Root => this.Segments[0];

        /// <summary>Whether the column belongs to a value object rather than to the entity itself.</summary>
        public bool IsNested => this.Segments.Count > 1;

        /// <summary>The CLR type of <see cref="Leaf"/>.</summary>
        public Type Type => this.Leaf.PropertyType;

        /// <summary>The dotted property path, for example <c>ZonedTime.ZoneCode</c>.</summary>
        public string Name { get; }

        /// <summary>
        ///     The name a value read back through this path is aliased and keyed by, for example
        ///     <c>ZonedTime_ZoneCode</c>. It is not dotted because aliases are written into the SQL unquoted.
        /// </summary>
        public string Alias { get; }

        /// <summary>
        ///     Resolves a dotted property name on <paramref name="entityType"/>, or returns <c>null</c> when
        ///     any segment does not exist.
        /// </summary>
        public static ColumnPath TryResolve(Type entityType, string dottedName)
        {
            var segments = new List<PropertyInfo>();
            var current = entityType;
            foreach (var name in dottedName.Split('.'))
            {
                var property = current.GetProperty(name);
                if (property is null)
                    return null;
                segments.Add(property);
                current = property.PropertyType;
            }
            return new ColumnPath(segments);
        }

        /// <summary>
        ///     Reads the value, or <c>null</c> when a value object on the way is itself <c>null</c>.
        /// </summary>
        public object GetValue(object entity)
        {
            var current = entity;
            foreach (var segment in this.Segments)
            {
                if (current is null)
                    return null;
                current = segment.GetValue(current);
            }
            return current;
        }

        /// <summary>
        ///     The position of the first value object on the way to the leaf that is <c>null</c> on
        ///     <paramref name="entity"/>, or <c>-1</c> when every one is present — always <c>-1</c> for a path
        ///     that is not nested.
        /// </summary>
        public int IndexOfNullContainer(object entity)
        {
            var current = entity;
            for (var i = 0; i < this.Segments.Count - 1; i++)
            {
                current = this.Segments[i].GetValue(current);
                if (current is null)
                    return i;
            }
            return -1;
        }

        /// <summary>
        ///     Writes the value, creating any value object on the way that is still <c>null</c>.
        /// </summary>
        public void SetValue(object entity, object value)
        {
            var current = entity;
            for (var i = 0; i < this.Segments.Count - 1; i++)
            {
                var segment = this.Segments[i];
                var next = segment.GetValue(current);
                if (next is null)
                {
                    var type = segment.PropertyType;
                    if (!type.IsValueType && type.GetConstructor(Type.EmptyTypes) is null)
                    {
                        throw new InvalidOperationException(
                            $"'{segment.DeclaringType?.Name}.{segment.Name}' is null and '{type.Name}' has no parameterless " +
                            $"constructor, so the value of '{this.Name}' cannot be assigned to it.");
                    }
                    if (!segment.CanWrite)
                    {
                        throw new InvalidOperationException(
                            $"'{segment.DeclaringType?.Name}.{segment.Name}' is null and has no setter, so the value of '{this.Name}' cannot be assigned to it.");
                    }
                    next = Activator.CreateInstance(type);
                    segment.SetValue(current, next);
                }
                current = next;
            }
            this.Leaf.SetValue(current, value);
        }

        /// <inheritdoc />
        public bool Equals(ColumnPath other)
            => other != null && this.Segments.SequenceEqual(other.Segments);

        /// <inheritdoc />
        public override bool Equals(object obj) => this.Equals(obj as ColumnPath);

        /// <inheritdoc />
        public override int GetHashCode() => this.Name.GetHashCode();

        /// <inheritdoc />
        public override string ToString() => this.Name;
    }
}
