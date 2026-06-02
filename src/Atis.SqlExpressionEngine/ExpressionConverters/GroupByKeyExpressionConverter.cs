using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    /// <summary>
    ///     <para>
    ///         Factory class for creating GroupByKeyExpressionConverter instances.
    ///     </para>
    /// </summary>
    public class GroupByKeyExpressionConverterFactory : LinqToSqlExpressionConverterFactoryBase<MemberExpression>
    {
        private readonly IReflectionService reflectionService;

        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="GroupByKeyExpressionConverterFactory"/> class.
        ///     </para>
        /// </summary>
        public GroupByKeyExpressionConverterFactory(IReflectionService reflectionService) : base()
        {
            this.reflectionService = reflectionService;
        }


        /// <summary>
        ///     <para>
        ///         Determines whether the specified member expression is a GroupBy key member.
        ///     </para>
        /// </summary>
        /// <param name="memberExpression">The member expression to check.</param>
        /// <returns><c>true</c> if the specified member expression is a GroupBy key member; otherwise, <c>false</c>.</returns>
        protected virtual bool IsGroupByKeyMember(MemberExpression memberExpression)
        {
            if (memberExpression.Member.Name == "Key")
            {
                var parentType = memberExpression.Expression.Type;
                if (this.reflectionService.IsGroupingType(parentType))
                {
                    return true;
                }
            }
            return false;
        }

        /// <inheritdoc />
        public override bool TryCreate(IConverterDependencies converterDependencies, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is MemberExpression memberExpr)
            {
                if (this.IsGroupByKeyMember(memberExpr))
                {
                    var dependencies = this.GetConverterDependencies(converterDependencies);
                    converter = new GroupByKeyExpressionConverter(dependencies, memberExpr, converterStack);
                    return true;
                }
            }
            converter = null;
            return false;
        }
    }

    /// <summary>
    ///     <para>
    ///         Converter class for converting GroupBy key expressions to SQL expressions.
    ///     </para>
    /// </summary>
    public class GroupByKeyExpressionConverter : LinqToNonSqlQueryConverterBase<MemberExpression>
    {
        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="GroupByKeyExpressionConverter"/> class.
        ///     </para>
        /// </summary>
        /// <param name="converterDependencies">The conversion context.</param>
        /// <param name="expression">The expression to be converted.</param>
        /// <param name="converterStack">The stack of converters in use.</param>
        public GroupByKeyExpressionConverter(LinqToSqlExpressionConverterDependencies converterDependencies, MemberExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(converterDependencies, expression, converterStack)
        {
        }

        /// <inheritdoc />
        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            var sqlQuery = convertedChildren[0].CastTo<SqlSelectExpression>();
            return sqlQuery.GroupByClause;
        }
    }
}
