using System;
using System.Reflection;

using Atis.Orm.Functions;

namespace Atis.Orm.Metadata
{
    /// <summary>Fluent SQL-function metadata configuration used by <see cref="ModelBuilder"/>.</summary>
    public sealed class SqlFunctionBuilder
    {
        private readonly MethodInfo method;
        private string functionName;
        private string template;
        private bool isPredicate;
        private SqlWindowFunction? windowFunction;

        internal SqlFunctionBuilder(MethodInfo method)
        {
            this.method = SqlFunctionMetadata.Normalize(method ?? throw new ArgumentNullException(nameof(method)));
            this.functionName = method.Name;
        }

        public SqlFunctionBuilder HasName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A SQL function name must not be blank.", nameof(name));
            this.functionName = name;
            return this;
        }

        public SqlFunctionBuilder HasTemplate(string sqlTemplate)
        {
            if (sqlTemplate == null) throw new ArgumentNullException(nameof(sqlTemplate));
            this.template = sqlTemplate;
            this.windowFunction = null;
            return this;
        }

        public SqlFunctionBuilder IsPredicate(bool value = true)
        {
            this.isPredicate = value;
            return this;
        }

        public SqlFunctionBuilder IsWindowFunction(SqlWindowFunction function)
        {
            this.windowFunction = function;
            this.template = null;
            return this;
        }

        internal SqlFunctionMetadata Build()
        {
            if (this.windowFunction.HasValue)
                return new SqlWindowFunctionMetadata(this.method, this.windowFunction.Value);
            if (this.template != null)
                return new SqlTemplateFunctionMetadata(
                    this.method,
                    SqlFunctionTemplate.Parse(this.template, this.method.GetParameters().Length, this.method.Name),
                    this.isPredicate,
                    this.functionName);
            if (this.isPredicate)
                throw new InvalidOperationException("IsPredicate currently requires a SQL template.");
            return new SqlFunctionMetadata(this.method, this.functionName);
        }
    }
}
