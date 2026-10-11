using System;
using System.Reflection;

using Atis.Orm.Abstractions;
using Atis.Orm.Annotations;
using Atis.Orm.Functions;

namespace Atis.Orm.Metadata
{
    /// <summary>
    ///     Converts optional annotations and built-in <see cref="Sql"/> conventions into model metadata.
    ///     Query conversion never reads the annotations itself.
    /// </summary>
    public sealed class SqlFunctionAnnotationReader : ISqlFunctionAnnotationReader
    {
        public bool TryGetSqlFunction(MethodInfo method, out SqlFunctionMetadata metadata)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));
            method = SqlFunctionMetadata.Normalize(method);

            if (!method.IsStatic)
            {
                metadata = null;
                return false;
            }

            var attribute = method.GetCustomAttribute<SqlFunctionAttribute>(inherit: false);
            var inFunctionClass = method.DeclaringType != null
                                  && method.DeclaringType.GetCustomAttribute<SqlFunctionClassAttribute>(inherit: false) != null;
            if (attribute == null && !inFunctionClass)
            {
                metadata = null;
                return false;
            }

            if (method.DeclaringType == typeof(Sql))
            {
                if (Enum.TryParse(method.Name, out SqlWindowClauseOperation clauseOperation))
                {
                    metadata = new SqlWindowClauseMetadata(method, clauseOperation);
                    return true;
                }
                if (Enum.TryParse(method.Name, out SqlWindowFunction windowFunction))
                {
                    metadata = new SqlWindowFunctionMetadata(method, windowFunction);
                    return true;
                }
            }

            var templateText = attribute?.Template;
            if (templateText != null)
            {
                metadata = new SqlTemplateFunctionMetadata(
                    method,
                    SqlFunctionTemplate.Parse(templateText, method.GetParameters().Length, method.Name),
                    attribute.IsPredicate);
                return true;
            }

            metadata = new SqlFunctionMetadata(method, attribute?.FunctionName);
            return true;
        }
    }
}
