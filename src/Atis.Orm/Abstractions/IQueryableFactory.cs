using System.Linq;
using System.Linq.Expressions;

namespace Atis.Orm.Abstractions 
{
    /// <summary>
    ///   A factory interface for creating IQueryable instances.
    /// </summary>
    public interface IQueryableFactory
    {
        /// <summary>
        ///   Creates a new instance of <see cref="IQueryable{T}"/> with the specified type parameter.
        /// </summary>
        /// <typeparam name="T">The type of the elements in the query.</typeparam>
        /// <returns>A new instance of <see cref="IQueryable{T}"/>.</returns>
        IQueryable<T> CreateQueryable<T>();

        /// <summary>
        ///   Creates a new instance of <see cref="IQueryable{T}"/> with the specified type parameter and expression.
        /// </summary>
        /// <typeparam name="T">The type of the elements in the query.</typeparam>
        /// <param name="expression">The expression to be used by the query.</param>
        /// <returns>A new instance of <see cref="IQueryable{T}"/>.</returns>
        IQueryable<T> CreateQueryable<T>(Expression expression);
    }
}