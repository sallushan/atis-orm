using System.Collections.Generic;

namespace Atis.Orm
{
    public interface ICompiledQuery
    {
        IExecutionContext GetExecutionContext(IReadOnlyList<object> parameterValues, bool useInitialValues);
    }
}