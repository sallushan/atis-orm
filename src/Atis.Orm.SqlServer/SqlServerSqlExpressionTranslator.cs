using System;

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
        protected override void TranslateStringFunction(SqlStringFunctionExpression node)
        {
            // Translate children strictly in the order they appear in the output. Each parameter marker
            // is therefore emitted once per textual occurrence, in position: the invariant that works for
            // named (@pN) and positional ("?") dialects alike. When an operand appears twice in the
            // output (e.g. SUBSTRING's LEN(s)), it is translated twice on purpose - one marker per slot.
            void Str() => this.TranslateExpression(node.StringExpression);
            void Arg(int i) => this.TranslateExpression(node.Arguments[i]);
            var argCount = node.Arguments?.Count ?? 0;

            switch (node.StringFunction)
            {
                case SqlStringFunction.Concat:
                    this.Append("CONCAT(");
                    Str();
                    for (var i = 0; i < argCount; i++)
                    {
                        this.Append(", ");
                        Arg(i);
                    }
                    this.Append(")");
                    break;
                case SqlStringFunction.Join:
                    // args[0] = separator, string expression = the value collection (comma-separated).
                    this.Append("CONCAT_WS(");
                    Arg(0);
                    this.Append(", ");
                    Str();
                    this.Append(")");
                    break;
                case SqlStringFunction.JoinAggregate:
                    // string expression = the aggregated value, args[0] = separator. No ORDER BY is carried
                    // by the tree, so we emit STRING_AGG without a WITHIN GROUP clause.
                    this.Append("STRING_AGG(");
                    Str();
                    this.Append(", ");
                    Arg(0);
                    this.Append(")");
                    break;
                case SqlStringFunction.ConcatAggregate:
                    this.Append("STRING_AGG(");
                    Str();
                    this.Append(", '')");
                    break;
                case SqlStringFunction.ToUpper:
                    this.Append("UPPER(");
                    Str();
                    this.Append(")");
                    break;
                case SqlStringFunction.ToLower:
                    this.Append("LOWER(");
                    Str();
                    this.Append(")");
                    break;
                case SqlStringFunction.Trim:
                    this.Append("TRIM(");
                    Str();
                    this.Append(")");
                    break;
                case SqlStringFunction.TrimStart:
                    this.Append("LTRIM(");
                    Str();
                    this.Append(")");
                    break;
                case SqlStringFunction.TrimEnd:
                    this.Append("RTRIM(");
                    Str();
                    this.Append(")");
                    break;
                case SqlStringFunction.Replace:
                    this.Append("REPLACE(");
                    Str();
                    this.Append(", ");
                    Arg(0);
                    this.Append(", ");
                    Arg(1);
                    this.Append(")");
                    break;
                case SqlStringFunction.CharLength:
                    this.Append("LEN(");
                    Str();
                    this.Append(")");
                    break;
                case SqlStringFunction.SubString:
                    // .NET Substring is 0-based; T-SQL SUBSTRING is 1-based and requires a length.
                    this.Append("SUBSTRING(");
                    Str();
                    this.Append(", (");
                    Arg(0);
                    if (argCount >= 2)
                    {
                        this.Append(") + 1, ");
                        Arg(1);
                        this.Append(")");
                    }
                    else
                    {
                        this.Append(") + 1, LEN(");
                        Str();
                        this.Append("))");
                    }
                    break;
                case SqlStringFunction.CharIndex:
                    // .NET IndexOf(x): 0-based, returns -1 when not found.
                    // T-SQL CHARINDEX(substring, string): 1-based (operands reversed), returns 0 when not found.
                    this.Append("CHARINDEX(");
                    Arg(0);
                    this.Append(", ");
                    Str();
                    this.Append(") - 1");
                    break;
                default:
                    throw new NotSupportedException($"String function '{node.StringFunction}' is not supported by the SQL Server translator.");
            }
        }
    }
}
