using System;

namespace Atis.SqlExpressionEngine.Exceptions
{
    /// <summary>
    ///     <para>
    ///         Thrown when a marker method that only has meaning inside a translated expression tree is
    ///         invoked directly, i.e. against in-memory objects rather than through the query provider.
    ///     </para>
    ///     <para>
    ///         <see cref="WhereBuilder"/>'s methods are markers: a preprocessor rewrites the call before it
    ///         ever runs, so the body is unreachable on the SQL path. They deliberately have no in-memory
    ///         implementation, because the two would have to agree on what "no value" means and the old
    ///         library's eager bodies disagreed with its expression forms in both directions.
    ///     </para>
    /// </summary>
    public class DirectCallNotSupportedException : NotSupportedException
    {
        /// <summary>Creates the exception for <paramref name="methodName"/>.</summary>
        /// <param name="methodName">The marker method that was invoked directly.</param>
        public DirectCallNotSupportedException(string methodName)
            : base($"'{methodName}' is a marker method and cannot be called directly. It only has meaning " +
                   $"inside an expression tree that is translated to SQL, where a preprocessor replaces the " +
                   $"call. Calling it in memory - including through LINQ to Objects, e.g. " +
                   $"list.AsQueryable().Where(sameLambda) - reaches this method body and throws. There is no " +
                   $"in-memory equivalent by design; write the predicate with ordinary C# operators instead.")
        {
            this.MethodName = methodName;
        }

        /// <summary>Creates the exception with a caller-supplied message.</summary>
        public DirectCallNotSupportedException(string methodName, string message) : base(message)
        {
            this.MethodName = methodName;
        }

        /// <summary>The marker method that was invoked directly.</summary>
        public string MethodName { get; }
    }
}
