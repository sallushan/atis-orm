using System.Collections.Generic;

namespace Atis.Orm.Abstractions
{
    public interface ICompiledQuery
    {
        bool IsPreprocessingRequired { get; }
        IExecutionContext GetExecutionContext(IReadOnlyDictionary<string, object> parameterValuesByIdentity, bool useInitialValues);
    }
}