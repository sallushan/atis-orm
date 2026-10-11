using System;
using System.Collections.Generic;
using System.Linq;

using Atis.SqlExpressionEngine.Visitors;

namespace Atis.SqlExpressionEngine.SqlExpressions
{
    /// <summary>Represents a SQL function call.</summary>
    public class SqlFunctionExpression : SqlExpression
    {
        /// <summary>Creates a function call with the supplied name and arguments.</summary>
        public SqlFunctionExpression(string functionName, params SqlExpression[] arguments)
            : this(functionName, arguments.AsEnumerable())
        {
        }

        /// <summary>Creates a function call with the supplied name and arguments.</summary>
        public SqlFunctionExpression(string functionName, IEnumerable<SqlExpression> arguments)
        {
            this.FunctionName = functionName ?? throw new ArgumentNullException(nameof(functionName));
            this.Arguments = arguments?.ToArray() ?? Array.Empty<SqlExpression>();
        }

        /// <inheritdoc />
        public override SqlExpressionType NodeType => SqlExpressionType.FunctionCall;

        /// <summary>The SQL function name.</summary>
        public string FunctionName { get; }

        /// <summary>The function arguments.</summary>
        public IReadOnlyList<SqlExpression> Arguments { get; }

        /// <summary>Returns this node when the arguments are unchanged; otherwise returns an updated node.</summary>
        public virtual SqlFunctionExpression Update(IEnumerable<SqlExpression> arguments)
        {
            if (arguments != null && !(arguments is IReadOnlyList<SqlExpression>))
                arguments = arguments.ToArray();
            if (arguments == this.Arguments
                || (arguments != null && this.Arguments.SequenceEqual(arguments)))
                return this;

            return new SqlFunctionExpression(this.FunctionName, arguments);
        }

        /// <summary>
        ///     Values, besides the child nodes, that decide the SQL this node writes. The hash generator adds
        ///     each one, so two nodes that differ only in these values do not hash the same.
        /// </summary>
        public virtual IEnumerable<object> GetHashComponents()
        {
            yield return this.FunctionName;
        }

        /// <inheritdoc />
        protected internal override SqlExpression Accept(SqlExpressionVisitor visitor)
        {
            return visitor.VisitSqlFunction(this);
        }

        /// <inheritdoc />
        protected internal override SqlExpression VisitChildren(SqlExpressionVisitor visitor)
        {
            var arguments = this.Arguments.Select(visitor.Visit).ToArray();
            return this.Update(arguments);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{this.FunctionName}({string.Join(", ", this.Arguments.Select(a => a.ToString()))})";
        }
    }
}
