using System;
using System.Collections.Generic;
using System.Linq;

namespace Atis.SqlExpressionEngine.SqlExpressions
{
    /// <summary>Math operations understood by the SQL expression engine.</summary>
    public enum SqlMathFunction
    {
        /// <summary>Absolute value.</summary>
        Abs,
        /// <summary>The least integral value greater than or equal to the operand.</summary>
        Ceiling,
        /// <summary>The greatest integral value less than or equal to the operand.</summary>
        Floor,
        /// <summary>The sign of the operand.</summary>
        Sign,
        /// <summary>The integral part of the operand.</summary>
        Truncate,
        /// <summary>Round to a specified precision and midpoint mode.</summary>
        Round,
        /// <summary>Square root.</summary>
        Sqrt,
        /// <summary>A value raised to a power.</summary>
        Power,
        /// <summary>The natural exponential function.</summary>
        Exp,
        /// <summary>A logarithm, optionally with a base.</summary>
        Log,
        /// <summary>A base-10 logarithm.</summary>
        Log10,
        /// <summary>Sine.</summary>
        Sin,
        /// <summary>Cosine.</summary>
        Cos,
        /// <summary>Tangent.</summary>
        Tan,
        /// <summary>Inverse sine.</summary>
        Asin,
        /// <summary>Inverse cosine.</summary>
        Acos,
        /// <summary>Inverse tangent.</summary>
        Atan,
        /// <summary>The angle whose tangent is the quotient of two operands.</summary>
        Atan2,
    }

    /// <summary>
    ///     A math function produced from a <see cref="Math"/> method. The operation is kept as an enum so a SQL
    ///     dialect can choose its own spelling and implementation instead of inferring semantics from a function name.
    /// </summary>
    public sealed class SqlMathFunctionExpression : SqlFunctionExpression
    {
        /// <summary>Creates a math function expression.</summary>
        public SqlMathFunctionExpression(
            SqlMathFunction function,
            IEnumerable<Type> argumentTypes,
            IEnumerable<SqlExpression> arguments,
            MidpointRounding? roundingMode = null)
            : base(function.ToString(), arguments)
        {
            this.Function = function;
            this.ArgumentTypes = argumentTypes?.ToArray() ?? throw new ArgumentNullException(nameof(argumentTypes));
            this.RoundingMode = roundingMode;
        }

        /// <summary>The semantic math operation.</summary>
        public SqlMathFunction Function { get; }

        /// <summary>The CLR parameter types of the selected <see cref="Math"/> overload.</summary>
        public IReadOnlyList<Type> ArgumentTypes { get; }

        /// <summary>The requested midpoint behavior for <see cref="SqlMathFunction.Round"/>; otherwise <c>null</c>.</summary>
        public MidpointRounding? RoundingMode { get; }

        /// <inheritdoc />
        public override SqlFunctionExpression Update(IEnumerable<SqlExpression> arguments)
        {
            if (arguments != null && !(arguments is IReadOnlyList<SqlExpression>))
                arguments = arguments.ToArray();
            if (arguments == this.Arguments
                || (arguments != null && this.Arguments.SequenceEqual(arguments)))
                return this;

            return new SqlMathFunctionExpression(this.Function, this.ArgumentTypes, arguments, this.RoundingMode);
        }

        /// <inheritdoc />
        public override IEnumerable<object> GetHashComponents()
        {
            yield return this.Function;
            yield return this.RoundingMode;
            foreach (var argumentType in this.ArgumentTypes)
                yield return argumentType;
        }
    }
}
