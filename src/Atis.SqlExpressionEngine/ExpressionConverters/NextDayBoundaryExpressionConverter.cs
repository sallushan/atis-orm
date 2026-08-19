using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using Atis.Expressions;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.ExpressionExtensions;
using Atis.SqlExpressionEngine.SqlExpressions;

namespace Atis.SqlExpressionEngine.ExpressionConverters
{
    /// <summary>
    ///     <para>Creates the converter for <see cref="NextDayBoundaryExpression"/>.</para>
    /// </summary>
    public class NextDayBoundaryExpressionConverterFactory : LinqToSqlExpressionConverterFactoryBase<NextDayBoundaryExpression>
    {
        public NextDayBoundaryExpressionConverterFactory() : base() { }

        public override IReadOnlyList<Type> GetConverterDependencyTypes()
        {
            return base.GetConverterDependencyTypes().Concat(new[] { typeof(ISqlDataTypeFactory) }).ToArray();
        }

        public override bool TryCreate(IConverterDependencies converterDependencies, Expression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack, out ExpressionConverterBase<Expression, SqlExpression> converter)
        {
            if (expression is NextDayBoundaryExpression nextDayBoundary)
            {
                var dependencies = this.GetConverterDependencies(converterDependencies);
                var sqlDataTypeFactory = converterDependencies.GetRequired<ISqlDataTypeFactory>();
                converter = new NextDayBoundaryExpressionConverter(sqlDataTypeFactory, dependencies, nextDayBoundary, converterStack);
                return true;
            }

            converter = null;
            return false;
        }
    }

    /// <summary>
    ///     <para>
    ///         Converts <see cref="NextDayBoundaryExpression"/> to
    ///         <c>DATEADD(day, 1, CAST(value AS date))</c>, built from the existing date-add and cast nodes so
    ///         every dialect that already renders those renders this too.
    ///     </para>
    /// </summary>
    public class NextDayBoundaryExpressionConverter : LinqToNonSqlQueryConverterBase<NextDayBoundaryExpression>
    {
        private readonly ISqlDataTypeFactory sqlDataTypeFactory;

        public NextDayBoundaryExpressionConverter(ISqlDataTypeFactory sqlDataTypeFactory, LinqToSqlExpressionConverterDependencies context, NextDayBoundaryExpression expression, ExpressionConverterBase<Expression, SqlExpression>[] converterStack)
            : base(context, expression, converterStack)
        {
            this.sqlDataTypeFactory = sqlDataTypeFactory;
        }

        /// <inheritdoc/>
        public override SqlExpression Convert(SqlExpression[] convertedChildren)
        {
            // CAST to date first, so the caller's time-of-day cannot shift the boundary.
            var dateOnly = this.SqlFactory.CreateCast(convertedChildren[0], this.sqlDataTypeFactory.CreateDate());

            return this.SqlFactory.CreateDateAdd(SqlDatePart.Day, this.SqlFactory.CreateLiteral(1), dateOnly);
        }
    }
}
