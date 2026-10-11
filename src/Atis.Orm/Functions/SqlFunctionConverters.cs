using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using Atis.Expressions;
using Atis.Orm.Abstractions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.ExpressionConverters;
using Atis.SqlExpressionEngine.SqlExpressions;

namespace Atis.Orm.Functions
{
    internal static class SqlFunctionConverterDependencies
    {
        public static IReadOnlyList<Type> AddModel(IReadOnlyList<Type> dependencies)
            => dependencies.Concat(new[] { typeof(ISqlFunctionMetadataProvider) }).Distinct().ToArray();

        public static bool TryGetMetadata(
            IConverterDependencies dependencies,
            MethodCallExpression call,
            out SqlFunctionMetadata metadata)
            => dependencies.GetRequired<ISqlFunctionMetadataProvider>()
                           .TryGetSqlFunction(call.Method, out metadata);
    }

    /// <summary>Recognizes model metadata for SQL-template escape hatches.</summary>
    public sealed class SqlFunctionTemplateConverterFactory : LinqToSqlExpressionConverterFactoryBase<MethodCallExpression>
    {
        public override IReadOnlyList<Type> GetConverterDependencyTypes()
            => SqlFunctionConverterDependencies.AddModel(base.GetConverterDependencyTypes());

        public override bool TryCreate(IConverterDependencies converterDependencies, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is MethodCallExpression call
                && SqlFunctionConverterDependencies.TryGetMetadata(converterDependencies, call, out var metadata)
                && metadata is SqlTemplateFunctionMetadata templateMetadata)
            {
                converter = new SqlFunctionTemplateConverter(
                    this.GetConverterDependencies(converterDependencies), call, converterStack, templateMetadata);
                return true;
            }
            converter = null;
            return false;
        }
    }

    /// <summary>Builds a template function node from model-owned, already-parsed metadata.</summary>
    public sealed class SqlFunctionTemplateConverter : LinqToNonSqlQueryConverterBase<MethodCallExpression>
    {
        private readonly SqlTemplateFunctionMetadata metadata;

        internal SqlFunctionTemplateConverter(
            LinqToSqlExpressionConverterDependencies dependencies,
            MethodCallExpression expression,
            ExpressionConverterBase<Expression, SqlExpression>[] converters,
            SqlTemplateFunctionMetadata metadata)
            : base(dependencies, expression, converters)
        {
            this.metadata = metadata;
        }

        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            var arguments = new List<SqlExpression>();
            var argumentSlot = new Dictionary<int, int>();
            var parts = new List<SqlFunctionTemplatePart>();
            var text = new StringBuilder();

            void FlushText()
            {
                if (text.Length == 0)
                    return;
                parts.Add(new SqlFunctionTemplatePart(SqlFunctionTemplatePartKind.Text, text.ToString(), -1));
                text.Clear();
            }

            foreach (var part in this.metadata.Template.Parts)
            {
                switch (part.Kind)
                {
                    case SqlFunctionTemplatePartKind.Text:
                        text.Append(part.Text);
                        break;
                    case SqlFunctionTemplatePartKind.Literal:
                        text.Append(this.LiteralText(convertedChildren[part.ArgumentIndex], part.ArgumentIndex));
                        break;
                    default: // Argument or Condition
                        FlushText();
                        if (!argumentSlot.TryGetValue(part.ArgumentIndex, out var slot))
                        {
                            slot = arguments.Count;
                            arguments.Add(convertedChildren[part.ArgumentIndex]);
                            argumentSlot.Add(part.ArgumentIndex, slot);
                        }
                        parts.Add(new SqlFunctionTemplatePart(part.Kind, null, slot));
                        break;
                }
            }
            FlushText();
            return new SqlTemplateFunctionExpression(this.metadata.FunctionName, parts, arguments, this.metadata.IsCondition);
        }

        private string LiteralText(SqlExpression argument, int index)
        {
            var name = this.Expression.Method.GetParameters()[index].Name;
            if (!(argument is SqlLiteralExpression literal) || literal.LiteralValue == null)
                throw new InvalidOperationException(
                    $"Argument '{name}' of '{this.Expression.Method.Name}' is written into the SQL text, so it must be " +
                    "a constant written in the query (an enum value or a whole number), not a variable or an expression.");

            var value = literal.LiteralValue;
            if (value.GetType().IsEnum)
                return value.ToString();
            switch (Type.GetTypeCode(value.GetType()))
            {
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    return System.Convert.ToString(value, CultureInfo.InvariantCulture);
            }
            throw new InvalidOperationException(
                $"Argument '{name}' of '{this.Expression.Method.Name}' is written into the SQL text, so it must be " +
                $"an enum value or a whole number, but it is a {value.GetType().Name}.");
        }
    }

    /// <summary>Recognizes model metadata for OVER-clause builders and window functions.</summary>
    public sealed class SqlWindowConverterFactory : LinqToSqlExpressionConverterFactoryBase<MethodCallExpression>
    {
        public override IReadOnlyList<Type> GetConverterDependencyTypes()
            => SqlFunctionConverterDependencies.AddModel(base.GetConverterDependencyTypes());

        public override bool TryCreate(IConverterDependencies converterDependencies, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is MethodCallExpression call
                && SqlFunctionConverterDependencies.TryGetMetadata(converterDependencies, call, out var metadata)
                && (metadata is SqlWindowFunctionMetadata || metadata is SqlWindowClauseMetadata))
            {
                converter = new SqlWindowConverter(
                    this.GetConverterDependencies(converterDependencies), call, converterStack, metadata);
                return true;
            }
            converter = null;
            return false;
        }
    }

    /// <summary>Builds OVER-clause and window-function nodes from semantic model metadata.</summary>
    public sealed class SqlWindowConverter : LinqToNonSqlQueryConverterBase<MethodCallExpression>
    {
        private readonly SqlFunctionMetadata metadata;

        internal SqlWindowConverter(
            LinqToSqlExpressionConverterDependencies dependencies,
            MethodCallExpression expression,
            ExpressionConverterBase<Expression, SqlExpression>[] converters,
            SqlFunctionMetadata metadata)
            : base(dependencies, expression, converters)
        {
            this.metadata = metadata;
        }

        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            if (this.metadata is SqlWindowClauseMetadata clause)
            {
                switch (clause.Operation)
                {
                    case SqlWindowClauseOperation.Over:
                        return new SqlOverClauseExpression();
                    case SqlWindowClauseOperation.PartitionBy:
                        return this.RequireOver(convertedChildren[0]).AddPartitionBy(convertedChildren[1]);
                    case SqlWindowClauseOperation.OrderBy:
                        return this.RequireOver(convertedChildren[0]).AddOrderBy(convertedChildren[1], SortDirection.Ascending);
                    case SqlWindowClauseOperation.OrderByDescending:
                        return this.RequireOver(convertedChildren[0]).AddOrderBy(convertedChildren[1], SortDirection.Descending);
                    default: // Argument or Condition
                        throw new NotSupportedException($"OVER-clause operation '{clause.Operation}' is not supported.");
                }
            }

            if (!(this.metadata is SqlWindowFunctionMetadata window))
                throw new InvalidOperationException($"'{this.Expression.Method}' has window metadata of an unsupported type.");

            var over = this.RequireOver(convertedChildren[convertedChildren.Length - 1]);
            return new SqlWindowFunctionExpression(
                window.Function,
                convertedChildren.Take(convertedChildren.Length - 1).ToList(),
                over);
        }

        private SqlOverClauseExpression RequireOver(SqlExpression expression)
        {
            return expression as SqlOverClauseExpression
                ?? throw new InvalidOperationException(
                    $"'Sql.{this.Expression.Method.Name}' needs an OVER clause built inline in the query: " +
                    "Sql.Over().PartitionBy(...).OrderBy(...). A clause kept in a variable cannot be translated.");
        }
    }

    /// <summary>Lowest-priority fallback for ordinary model-registered SQL functions.</summary>
    public sealed class SqlFunctionConverterFactory : LinqToSqlExpressionConverterFactoryBase<MethodCallExpression>
    {
        public override IReadOnlyList<Type> GetConverterDependencyTypes()
            => SqlFunctionConverterDependencies.AddModel(base.GetConverterDependencyTypes());

        public override bool TryCreate(IConverterDependencies converterDependencies, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is MethodCallExpression call
                && SqlFunctionConverterDependencies.TryGetMetadata(converterDependencies, call, out var metadata))
            {
                if (metadata.GetType() != typeof(SqlFunctionMetadata))
                    throw new InvalidOperationException(
                        $"SQL function '{call.Method}' has metadata of type '{metadata.GetType().Name}', but no " +
                        "specialized converter accepted it before the generic SQL-function converter.");
                converter = new SqlFunctionConverter(
                    this.GetConverterDependencies(converterDependencies), call, converterStack, metadata);
                return true;
            }
            converter = null;
            return false;
        }
    }

    /// <summary>Builds a plain name-and-arguments SQL function node.</summary>
    public sealed class SqlFunctionConverter : LinqToNonSqlQueryConverterBase<MethodCallExpression>
    {
        private readonly SqlFunctionMetadata metadata;

        internal SqlFunctionConverter(
            LinqToSqlExpressionConverterDependencies dependencies,
            MethodCallExpression expression,
            ExpressionConverterBase<Expression, SqlExpression>[] converters,
            SqlFunctionMetadata metadata)
            : base(dependencies, expression, converters)
        {
            this.metadata = metadata;
        }

        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            var parameters = this.Expression.Method.GetParameters();
            var arguments = new List<SqlExpression>();
            for (var i = 0; i < convertedChildren.Length; i++)
            {
                var isParamsArray = i == parameters.Length - 1
                                    && parameters[i].IsDefined(typeof(ParamArrayAttribute), false);
                if (!isParamsArray)
                {
                    arguments.Add(convertedChildren[i]);
                    continue;
                }
                if (this.Expression.Arguments[i].NodeType != ExpressionType.NewArrayInit
                    || !(convertedChildren[i] is SqlCollectionExpression collection))
                    throw new InvalidOperationException(
                        $"'{this.Expression.Method.Name}' takes a params list, so its values must be written inline " +
                        "in the query (a captured array cannot be spread into separate SQL arguments).");
                arguments.AddRange(collection.SqlExpressions);
            }
            return new SqlFunctionExpression(this.metadata.FunctionName, arguments);
        }
    }
}
