using System.Collections.Generic;

namespace Atis.Orm
{
    public interface ICompiledQuery
    {
        bool IsPreprocessingRequired { get; }
        IExecutionContext GetExecutionContext(IReadOnlyList<object> parameterValues, bool useInitialValues);
    }
}