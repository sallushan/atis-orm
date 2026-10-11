using System;

namespace Atis.Orm.Annotations
{
    /// <summary>
    ///     Marks a CLR marker method for SQL-function discovery by the ORM model. Query conversion reads
    ///     the resulting model metadata; it does not inspect this attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class SqlFunctionAttribute : Attribute
    {
        /// <summary>
        ///     Optional SQL function name. When omitted, the CLR method name is used. This value is ignored
        ///     when <see cref="Template"/> is supplied because the template controls the complete SQL shape.
        /// </summary>
        public string FunctionName { get; set; }

        /// <summary>
        ///     Optional SQL template escape hatch. Use <c>{n}</c> for a translated argument and <c>[n]</c>
        ///     for an enum or whole-number constant written directly into the SQL text.
        /// </summary>
        public string Template { get; set; }

        /// <summary>Whether a template produces a SQL condition rather than a scalar value.</summary>
        public bool IsPredicate { get; set; }
    }

    /// <summary>
    ///     Marks a static class whose methods are SQL functions. Unannotated methods use their CLR method
    ///     name and translated arguments.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class SqlFunctionClassAttribute : Attribute
    {
    }
}
