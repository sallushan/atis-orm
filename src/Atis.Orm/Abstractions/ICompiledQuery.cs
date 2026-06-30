using System.Collections.Generic;

namespace Atis.Orm.Abstractions
{
    public interface ICompiledQuery
    {
        bool IsPreprocessingRequired { get; }
        IExecutionContext GetExecutionContext(IReadOnlyList<object> parameterValues, bool useInitialValues);
    }
}