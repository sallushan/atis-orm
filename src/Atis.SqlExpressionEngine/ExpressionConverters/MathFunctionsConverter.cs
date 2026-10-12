using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    /// <summary>Creates converters for the supported <see cref="Math"/> methods.</summary>
    public sealed class MathFunctionsConverterFactory : LinqToSqlExpressionConverterFactoryBase<MethodCallExpression>
    {
        private static readonly IReadOnlyDictionary<MethodInfo, SqlMathFunction> SupportedMethods = BuildSupportedMethods();

        /// <inheritdoc />
        public override bool TryCreate(
            IConverterDependencies converterDependencies,
            Expression expression,
            ExpressionConverterBase<Expression, SqlExpression>[] converterStack,
            out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is MethodCallExpression methodCall
                && methodCall.Method.DeclaringType == typeof(Math)
                && SupportedMethods.TryGetValue(methodCall.Method, out var function))
            {
                converter = new MathFunctionsConverter(
                    this.GetConverterDependencies(converterDependencies), methodCall, converterStack, function);
                return true;
            }

            converter = null;
            return false;
        }

        private static IReadOnlyDictionary<MethodInfo, SqlMathFunction> BuildSupportedMethods()
        {
            var methods = new Dictionary<MethodInfo, SqlMathFunction>();

            void Add(SqlMathFunction function, string name, params Type[] arguments)
            {
                var method = typeof(Math).GetMethod(name, arguments)
                    ?? throw new InvalidOperationException($"Math.{name} with {arguments.Length} argument(s) was not found.");
                methods.Add(method, function);
            }

            var signed = new[] { typeof(int), typeof(long), typeof(short), typeof(decimal), typeof(double) };
            foreach (var type in signed)
            {
                Add(SqlMathFunction.Abs, nameof(Math.Abs), type);
                Add(SqlMathFunction.Sign, nameof(Math.Sign), type);
            }

            foreach (var type in new[] { typeof(decimal), typeof(double) })
            {
                Add(SqlMathFunction.Ceiling, nameof(Math.Ceiling), type);
                Add(SqlMathFunction.Floor, nameof(Math.Floor), type);
                Add(SqlMathFunction.Truncate, nameof(Math.Truncate), type);
                Add(SqlMathFunction.Round, nameof(Math.Round), type);
                Add(SqlMathFunction.Round, nameof(Math.Round), type, typeof(int));
                Add(SqlMathFunction.Round, nameof(Math.Round), type, typeof(MidpointRounding));
                Add(SqlMathFunction.Round, nameof(Math.Round), type, typeof(int), typeof(MidpointRounding));
            }

            var d = typeof(double);
            Add(SqlMathFunction.Sqrt, nameof(Math.Sqrt), d);
            Add(SqlMathFunction.Power, nameof(Math.Pow), d, d);
            Add(SqlMathFunction.Exp, nameof(Math.Exp), d);
            Add(SqlMathFunction.Log, nameof(Math.Log), d);
            Add(SqlMathFunction.Log, nameof(Math.Log), d, d);
            Add(SqlMathFunction.Log10, nameof(Math.Log10), d);
            Add(SqlMathFunction.Sin, nameof(Math.Sin), d);
            Add(SqlMathFunction.Cos, nameof(Math.Cos), d);
            Add(SqlMathFunction.Tan, nameof(Math.Tan), d);
            Add(SqlMathFunction.Asin, nameof(Math.Asin), d);
            Add(SqlMathFunction.Acos, nameof(Math.Acos), d);
            Add(SqlMathFunction.Atan, nameof(Math.Atan), d);
            Add(SqlMathFunction.Atan2, nameof(Math.Atan2), d, d);

            return methods;
        }

    }

    /// <summary>Converts supported <see cref="Math"/> calls to a provider-overridable math AST node.</summary>
    public sealed class MathFunctionsConverter : LinqToNonSqlQueryConverterBase<MethodCallExpression>
    {
        private readonly SqlMathFunction function;

        internal MathFunctionsConverter(
            LinqToSqlExpressionConverterDependencies dependencies,
            MethodCallExpression expression,
            ExpressionConverterBase<Expression, SqlExpression>[] converterStack,
            SqlMathFunction function)
            : base(dependencies, expression, converterStack)
        {
            this.function = function;
        }

        /// <inheritdoc />
        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            if (this.Expression.Method.Name == nameof(Math.Round))
                return this.ConvertRound(convertedChildren);

            var parameterTypes = this.Expression.Method.GetParameters().Select(p => p.ParameterType).ToArray();
            return this.SqlFactory.CreateMathFunction(this.function, parameterTypes, convertedChildren);
        }

        private SqlExpression ConvertRound(SqlExpression[] convertedChildren)
        {
            var parameters = this.Expression.Method.GetParameters();
            var modeIndex = Array.FindIndex(parameters, p => p.ParameterType == typeof(MidpointRounding));
            var mode = MidpointRounding.ToEven;
            if (modeIndex >= 0)
            {
                var modeExpression = this.Expression.Arguments[modeIndex] as ConstantExpression;
                if (modeExpression == null || !(modeExpression.Value is MidpointRounding constantMode))
                    throw new InvalidOperationException(
                        "The MidpointRounding argument of Math.Round must be a constant written in the query.");
                mode = constantMode;
            }

            var sqlArgumentCount = modeIndex < 0 ? convertedChildren.Length : modeIndex;
            var arguments = convertedChildren.Take(sqlArgumentCount).ToArray();
            var argumentTypes = parameters.Take(sqlArgumentCount).Select(p => p.ParameterType).ToArray();

            return this.SqlFactory.CreateMathFunction(
                SqlMathFunction.Round, argumentTypes, arguments, mode);
        }
    }
}
