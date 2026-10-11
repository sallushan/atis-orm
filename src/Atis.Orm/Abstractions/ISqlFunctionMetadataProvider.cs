using System.Reflection;

using Atis.Orm.Functions;

namespace Atis.Orm.Abstractions
{
    /// <summary>
    ///     The model-side authority for deciding whether a CLR method is a SQL function and, when it is,
    ///     which semantic kind of SQL function it represents.
    /// </summary>
    public interface ISqlFunctionMetadataProvider
    {
        bool TryGetSqlFunction(MethodInfo method, out SqlFunctionMetadata metadata);
    }

    /// <summary>Reads optional SQL-function annotations while the model discovers metadata.</summary>
    public interface ISqlFunctionAnnotationReader
    {
        bool TryGetSqlFunction(MethodInfo method, out SqlFunctionMetadata metadata);
    }
}
