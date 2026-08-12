using System.Collections.Generic;

namespace Atis.Orm.Abstractions
{
    public interface ICompiledQuery
    {
        IExecutionContext GetExecutionContext(IReadOnlyDictionary<string, object> parameterValuesByIdentity, bool useInitialValues);
    }
}