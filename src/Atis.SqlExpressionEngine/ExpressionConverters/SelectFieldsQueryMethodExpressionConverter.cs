using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.Internal;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    /// <summary>Creates the converter for <see cref="QueryExtensions.SelectFields{T}"/> calls.</summary>
    public class SelectFieldsQueryMethodExpressionConverterFactory : QueryMethodExpressionConverterFactoryBase
    {
        public SelectFieldsQueryMethodExpressionConverterFactory() : base()
        {
        }

        /// <inheritdoc />
        protected override bool IsQueryMethodCall(MethodCallExpression methodCallExpression)
        {
            return methodCallExpression.Method.DeclaringType == typeof(QueryExtensions)
                   &&
                   methodCallExpression.Method.Name == nameof(QueryExtensions.SelectFields);
        }

        /// <inheritdoc />
        protected override ExpressionConverterBase<Expression, SqlExpression> CreateConverter(IConverterDependencies converterDependencies, MethodCallExpression methodCallExpression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
        {
            var d = this.GetConverterDependencies(converterDependencies);
            return new SelectFieldsQueryMethodExpressionConverter(d, methodCallExpression, converterStack);
        }
    }

    /// <summary>
    ///     <para>
    ///         Converts <see cref="QueryExtensions.SelectFields{T}"/> into an ordinary projection.
    ///     </para>
    ///     <para>
    ///         The method has the same shape as <c>Queryable.Select</c> — a source and one lambda over
    ///         the element — so everything about how the lambda's parameter is bound to the source is
    ///         inherited unchanged. The one thing this adds is turning the selector's array into a
    ///         shape the rest of the pipeline already understands.
    ///     </para>
    ///     <para>
    ///         An array has no member names, and a projection needs them: they become the column
    ///         aliases, and from there the keys of the dictionary each row is read into. So each
    ///         element is paired with the member it selects and the whole thing is rebuilt as a
    ///         <see cref="SqlMemberInitExpression"/> — after which this is indistinguishable from a
    ///         <c>Select</c> into a named type, which is the point. The select list, the query shape
    ///         and the element factory all follow from it with no special case anywhere.
    ///     </para>
    /// </summary>
    public class SelectFieldsQueryMethodExpressionConverter : QueryMethodExpressionConverterBase
    {
        private const int FieldsArgumentIndex = 1;

        public SelectFieldsQueryMethodExpressionConverter(LinqToSqlExpressionConverterDependencies dependencies, MethodCallExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(dependencies, expression, converterStack)
        {
        }

        /// <inheritdoc />
        protected override SqlExpression Convert(SqlSelectExpression sqlQuery, SqlExpression[] arguments)
        {
            sqlQuery.ApplyProjection(this.CreateFieldShape(arguments[0]));
            return sqlQuery;
        }

        /// <summary>
        ///     Rebuilds the converted array as a member-named shape. The bindings are read from two
        ///     places at once: the value from the converted array, and the name from the original
        ///     expression, which is the only side that still knows which member was written.
        /// </summary>
        private SqlMemberInitExpression CreateFieldShape(SqlExpression convertedFields)
        {
            var fieldCollection = convertedFields.CastTo<SqlCollectionExpression>(
                $"Argument {FieldsArgumentIndex + 1} of {nameof(QueryExtensions.SelectFields)} must select an object array of fields.");
            var fieldsLambda = this.Expression.GetArgLambdaRequired(FieldsArgumentIndex);
            var fieldArray = fieldsLambda.Body as NewArrayExpression
                ?? throw new InvalidOperationException(
                    $"Argument {FieldsArgumentIndex + 1} of {nameof(QueryExtensions.SelectFields)} must have the form 'x => new object[] {{ x.Field1, x.Field2 }}'.");
            var convertedFieldList = fieldCollection.SqlExpressions.ToArray();

            if (fieldArray.Expressions.Count == 0)
            {
                throw new InvalidOperationException(
                    $"A {nameof(QueryExtensions.SelectFields)} selector must contain at least one field.");
            }
            if (convertedFieldList.Length != fieldArray.Expressions.Count)
            {
                throw new InvalidOperationException(
                    $"The number of converted fields ({convertedFieldList.Length}) does not match the number selected ({fieldArray.Expressions.Count}).");
            }

            var bindings = new SqlMemberAssignment[fieldArray.Expressions.Count];
            for (var i = 0; i < fieldArray.Expressions.Count; i++)
            {
                bindings[i] = new SqlMemberAssignment(
                    SelectedMemberAlias.Resolve(fieldArray.Expressions[i], i, nameof(QueryExtensions.SelectFields)),
                    convertedFieldList[i]);
            }

            // The constructor rejects two bindings claiming one name, which is what makes
            // 'x => new object[] { x.Dept.Name, x.School.Name }' an error rather than a dropped column.
            return new SqlMemberInitExpression(bindings);
        }
    }
}
