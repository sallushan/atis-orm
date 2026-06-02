using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Atis.Expressions
{
    public interface IConverterDependencies
    {
        T GetRequired<T>() where T : class;
        bool TryGetValue<T>(out T value) where T : class;
        bool ContainsType(Type type);
    }

    internal class ConverterDependencies : IConverterDependencies
    {
        private readonly Dictionary<Type, object> internalContainer = new Dictionary<Type, object>();

        public T GetRequired<T>() where T : class
        {
            if (this.TryGetValue<T>(out var value))
            {
                return value;
            }
            throw new MissingConverterDependencyException(typeof(T));
        }

        public void Add(Type type, object value)
        {
            if (type is null) throw new ArgumentNullException(nameof(type));
            if (value is null) throw new ArgumentNullException(nameof(value));
            if (!type.IsInstanceOfType(value))
                throw new ArgumentException($"Value of type '{value.GetType()}' is not assignable to type '{type}'.", nameof(value));
            this.internalContainer.Add(type, value);
        }

        public bool ContainsType(Type type) => this.internalContainer.ContainsKey(type);

        public void Clear()
        {
            this.internalContainer.Clear();
        }

        public bool TryGetValue<T>(out T value) where T : class
        {
            if (this.internalContainer.TryGetValue(typeof(T), out var obj))
            {
                value = (T)obj;
                return true;
            }
            value = default;
            return false;
        }
    }
}
