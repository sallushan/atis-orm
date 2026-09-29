using System.Collections.Generic;

namespace Atis.Orm.Abstractions
{
    public interface ICompiledQuery
    {
        /// <param name="queryContext">
        ///     Supplies the values of contextual parameters. May be <c>null</c> only for a query that has none;
        ///     a contextual parameter met without one is an error.
        /// </param>
        IExecutionContext GetExecutionContext(IReadOnlyDictionary<string, object> parameterValuesByIdentity, bool useInitialValues, IQueryContext queryContext = null);
    }
}