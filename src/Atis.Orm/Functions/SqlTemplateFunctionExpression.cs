using System;
using System.Collections.Generic;
using System.Linq;

using Atis.SqlExpressionEngine.SqlExpressions;
using Atis.SqlExpressionEngine.Visitors;

namespace Atis.Orm.Functions
{
    /// <summary>
    ///     <para>
    ///         A database function call written from a text template, for example
    ///         <c>dateadd(day, {1}, {2})</c> after the <c>[0]</c> piece was already turned into text.
    ///     </para>
    ///     <para>
    ///         The pieces are only <see cref="SqlFunctionTemplatePartKind.Text"/>, <see cref="SqlFunctionTemplatePartKind.Condition"/> and
    ///         <see cref="SqlFunctionTemplatePartKind.Argument"/>. An <c>Argument</c> piece's
    ///         <see cref="SqlFunctionTemplatePart.ArgumentIndex"/> points into <see cref="Arguments"/>, which
    ///         holds only the arguments the template actually writes. The same argument can be pointed at
    ///         twice; it is then translated twice, once per place, so each place gets its own parameter.
    ///     </para>
    /// </summary>
    public sealed class SqlTemplateFunctionExpression : SqlFunctionExpression
    {
        /// <summary>Creates the node.</summary>
        /// <param name="parts">Text and argument pieces, in output order.</param>
        /// <param name="arguments">The translated arguments the pieces point at.</param>
        /// <param name="isCondition">Whether the result is a condition rather than a value.</param>
        public SqlTemplateFunctionExpression(string functionName, IReadOnlyList<SqlFunctionTemplatePart> parts, IReadOnlyList<SqlExpression> arguments, bool isCondition)
            : base(functionName, arguments)
        {
            if (arguments == null) throw new ArgumentNullException(nameof(arguments));
            this.Parts = parts ?? throw new ArgumentNullException(nameof(parts));
            this.IsCondition = isCondition;

            foreach (var part in parts)
            {
                if (part.Kind == SqlFunctionTemplatePartKind.Literal)
                    throw new ArgumentException("A literal piece must be turned into text before the node is built.", nameof(parts));
                if (part.Kind != SqlFunctionTemplatePartKind.Text && (part.ArgumentIndex < 0 || part.ArgumentIndex >= arguments.Count))
                    throw new ArgumentException($"A piece points at argument {part.ArgumentIndex}, but there are {arguments.Count}.", nameof(parts));
            }
        }

        /// <summary>The pieces, in output order.</summary>
        public IReadOnlyList<SqlFunctionTemplatePart> Parts { get; }

        /// <summary>Whether the result is a condition (like <c>a IN b</c>) rather than a value.</summary>
        public bool IsCondition { get; }

        /// <inheritdoc />
        public override SqlFunctionExpression Update(IEnumerable<SqlExpression> arguments)
        {
            var list = arguments?.ToList() ?? new List<SqlExpression>();
            if (list.SequenceEqual(this.Arguments))
                return this;
            return new SqlTemplateFunctionExpression(this.FunctionName, this.Parts, list, this.IsCondition);
        }

        /// <inheritdoc />
        public override IEnumerable<object> GetHashComponents()
        {
            yield return this.FunctionName;
            yield return this.IsCondition;
            foreach (var part in this.Parts)
            {
                yield return part.Kind;
                yield return part.Kind == SqlFunctionTemplatePartKind.Text ? part.Text : (object)part.ArgumentIndex;
            }
        }

        /// <inheritdoc />
        protected override SqlExpression VisitChildren(SqlExpressionVisitor visitor)
        {
            var visited = new SqlExpression[this.Arguments.Count];
            var changed = false;
            for (var i = 0; i < visited.Length; i++)
            {
                visited[i] = visitor.Visit(this.Arguments[i]);
                changed |= !ReferenceEquals(visited[i], this.Arguments[i]);
            }
            return changed ? new SqlTemplateFunctionExpression(this.FunctionName, this.Parts, visited, this.IsCondition) : this;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return string.Concat(this.Parts.Select(p => p.Kind == SqlFunctionTemplatePartKind.Text
                ? p.Text
                : "{" + p.ArgumentIndex + "}"));
        }
    }
}
