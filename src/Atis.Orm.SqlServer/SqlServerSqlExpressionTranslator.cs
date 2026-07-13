using System;
using System.Linq;

using Atis.Orm.Translation;
using Atis.SqlExpressionEngine.SqlExpressions;

namespace Atis.Orm.SqlServer
{
    /// <summary>
    ///     <para>
    ///         SQL Server dialect translator. Overrides the provider-agnostic base to emit
    ///         real T-SQL for constructs whose default rendering is not valid SQL Server syntax.
    ///     </para>
    /// </summary>
    public class SqlServerSqlExpressionTranslator : SqlExpressionTranslatorBase
    {
        /// <inheritdoc />
        protected override string TranslateStringFunction(SqlStringFunctionExpression node)
        {
            var s = this.TranslateExpression(node.StringExpression);
            var args = node.Arguments != null
                ? node.Arguments.Select(a => this.TranslateExpression(a)).ToArray()
                : Array.Empty<string>();

            switch (node.StringFunction)
            {
                case SqlStringFunction.Concat:
                    return $"CONCAT({CombineArgs(s, args)})";
                case SqlStringFunction.Join:
                    // args[0] = separator, s = the value collection (already rendered comma-separated).
                    return $"CONCAT_WS({args[0]}, {s})";
                case SqlStringFunction.JoinAggregate:
                    // s = the aggregated value, args[0] = separator. No ORDER BY is carried by the tree,
                    // so we emit STRING_AGG without a WITHIN GROUP clause.
                    return $"STRING_AGG({s}, {args[0]})";
                case SqlStringFunction.ConcatAggregate:
                    return $"STRING_AGG({s}, '')";
                case SqlStringFunction.ToUpper:
                    return $"UPPER({s})";
                case SqlStringFunction.ToLower:
                    return $"LOWER({s})";
                case SqlStringFunction.Trim:
                    return $"TRIM({s})";
                case SqlStringFunction.TrimStart:
                    return $"LTRIM({s})";
                case SqlStringFunction.TrimEnd:
                    return $"RTRIM({s})";
                case SqlStringFunction.Replace:
                    return $"REPLACE({s}, {args[0]}, {args[1]})";
                case SqlStringFunction.CharLength:
                    return $"LEN({s})";
                case SqlStringFunction.SubString:
                    // .NET Substring is 0-based; T-SQL SUBSTRING is 1-based and requires a length.
                    return args.Length >= 2
                        ? $"SUBSTRING({s}, ({args[0]}) + 1, {args[1]})"
                        : $"SUBSTRING({s}, ({args[0]}) + 1, LEN({s}))";
                case SqlStringFunction.CharIndex:
                    // .NET IndexOf(x): 0-based, returns -1 when not found.
                    // T-SQL CHARINDEX(substring, string): 1-based (operands reversed), returns 0 when not found.
                    return $"CHARINDEX({args[0]}, {s}) - 1";
                default:
                    throw new NotSupportedException($"String function '{node.StringFunction}' is not supported by the SQL Server translator.");
            }
        }

        private static string CombineArgs(string stringExpr, string[] args)
        {
            return args.Length > 0 ? $"{stringExpr}, {string.Join(", ", args)}" : stringExpr;
        }
    }
}
