using System;
using System.Reflection;

namespace Atis.Orm.Functions
{
    /// <summary>Model metadata for an ordinary <c>Name(argument, ...)</c> SQL function.</summary>
    public class SqlFunctionMetadata
    {
        public SqlFunctionMetadata(MethodInfo method, string functionName = null)
        {
            this.Method = Normalize(method ?? throw new ArgumentNullException(nameof(method)));
            this.FunctionName = string.IsNullOrWhiteSpace(functionName) ? method.Name : functionName;
        }

        public MethodInfo Method { get; }

        public string FunctionName { get; }

        internal static MethodInfo Normalize(MethodInfo method)
            => method.IsGenericMethod && !method.IsGenericMethodDefinition
                ? method.GetGenericMethodDefinition()
                : method;
    }

    /// <summary>Metadata for the explicit SQL-template escape hatch.</summary>
    public sealed class SqlTemplateFunctionMetadata : SqlFunctionMetadata
    {
        public SqlTemplateFunctionMetadata(MethodInfo method, SqlFunctionTemplate template, bool isCondition = false, string functionName = null)
            : base(method, functionName)
        {
            this.Template = template ?? throw new ArgumentNullException(nameof(template));
            this.IsCondition = isCondition;
        }

        public SqlFunctionTemplate Template { get; }

        public bool IsCondition { get; }
    }

    /// <summary>The fluent operations that build an OVER clause rather than emitting a function call.</summary>
    public enum SqlWindowClauseOperation
    {
        Over,
        PartitionBy,
        OrderBy,
        OrderByDescending,
    }

    /// <summary>Metadata for one OVER-clause builder method.</summary>
    public sealed class SqlWindowClauseMetadata : SqlFunctionMetadata
    {
        public SqlWindowClauseMetadata(MethodInfo method, SqlWindowClauseOperation operation)
            : base(method)
        {
            this.Operation = operation;
        }

        public SqlWindowClauseOperation Operation { get; }
    }

    /// <summary>Metadata for a provider-neutral window function.</summary>
    public sealed class SqlWindowFunctionMetadata : SqlFunctionMetadata
    {
        public SqlWindowFunctionMetadata(MethodInfo method, SqlWindowFunction function)
            : base(method)
        {
            this.Function = function;
        }

        public SqlWindowFunction Function { get; }
    }
}
