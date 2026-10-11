using System;
using System.Collections.Generic;

namespace Atis.Orm.Functions
{
    /// <summary>The kind of one piece of a parsed <see cref="SqlFunctionTemplate"/>.</summary>
    public enum SqlFunctionTemplatePartKind
    {
        /// <summary>Plain SQL text, copied as it is.</summary>
        Text,
        /// <summary><c>{n}</c>: an argument the engine translates.</summary>
        Argument,
        /// <summary><c>[n]</c>: an argument written into the SQL text.</summary>
        Literal,
    }

    /// <summary>One piece of a parsed <see cref="SqlFunctionTemplate"/>.</summary>
    public sealed class SqlFunctionTemplatePart
    {
        internal SqlFunctionTemplatePart(SqlFunctionTemplatePartKind kind, string text, int argumentIndex)
        {
            this.Kind = kind;
            this.Text = text;
            this.ArgumentIndex = argumentIndex;
        }

        /// <summary>What this piece is.</summary>
        public SqlFunctionTemplatePartKind Kind { get; }

        /// <summary>The text, when <see cref="Kind"/> is <see cref="SqlFunctionTemplatePartKind.Text"/>.</summary>
        public string Text { get; }

        /// <summary>The method argument number, for the other two kinds.</summary>
        public int ArgumentIndex { get; }
    }

    /// <summary>
    ///     A <see cref="Annotations.SqlFunctionAttribute.Template"/> string, split into text
    ///     and argument pieces once so it is not scanned again on every translation.
    /// </summary>
    public sealed class SqlFunctionTemplate
    {
        private SqlFunctionTemplate(IReadOnlyList<SqlFunctionTemplatePart> parts)
        {
            this.Parts = parts;
        }

        /// <summary>The pieces, in the order they are written.</summary>
        public IReadOnlyList<SqlFunctionTemplatePart> Parts { get; }

        /// <summary>
        ///     Splits <paramref name="template"/>. Braces or brackets that do not hold only digits are kept
        ///     as plain text, so <c>[dbo]</c> is not mistaken for a placeholder.
        /// </summary>
        /// <param name="template">The template text.</param>
        /// <param name="argumentCount">How many arguments the method has; a larger placeholder number is refused.</param>
        /// <param name="methodName">Used in error messages.</param>
        public static SqlFunctionTemplate Parse(string template, int argumentCount, string methodName)
        {
            if (template == null)
                throw new ArgumentNullException(nameof(template));

            var parts = new List<SqlFunctionTemplatePart>();
            var text = new System.Text.StringBuilder();
            var i = 0;
            while (i < template.Length)
            {
                var c = template[i];
                if ((c == '{' || c == '[') && TryReadPlaceholder(template, i, out var index, out var length))
                {
                    if (index >= argumentCount)
                        throw new InvalidOperationException(
                            $"The SQL template of '{methodName}' uses argument {index}, but the method has only {argumentCount} argument(s).");

                    if (text.Length > 0)
                    {
                        parts.Add(new SqlFunctionTemplatePart(SqlFunctionTemplatePartKind.Text, text.ToString(), -1));
                        text.Clear();
                    }
                    parts.Add(new SqlFunctionTemplatePart(
                        c == '{' ? SqlFunctionTemplatePartKind.Argument : SqlFunctionTemplatePartKind.Literal, null, index));
                    i += length;
                    continue;
                }
                text.Append(c);
                i++;
            }
            if (text.Length > 0)
                parts.Add(new SqlFunctionTemplatePart(SqlFunctionTemplatePartKind.Text, text.ToString(), -1));

            return new SqlFunctionTemplate(parts);
        }

        private static bool TryReadPlaceholder(string template, int start, out int index, out int length)
        {
            index = 0;
            length = 0;
            var close = template[start] == '{' ? '}' : ']';
            var pos = start + 1;
            var digits = 0;
            while (pos < template.Length && template[pos] >= '0' && template[pos] <= '9')
            {
                index = index * 10 + (template[pos] - '0');
                pos++;
                digits++;
            }
            if (digits == 0 || pos >= template.Length || template[pos] != close)
                return false;
            length = pos - start + 1;
            return true;
        }
    }
}
