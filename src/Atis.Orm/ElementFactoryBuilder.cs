using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using System.Text;

namespace Atis.Orm
{
    public class ElementFactoryBuilder : IElementFactoryBuilder
    {
        public Func<IDataReader, object> CreateElementFactory(Expression expression, SqlExpression sqlExpression)
        {
            if (sqlExpression is SqlDerivedTableExpression derivedTableSqlExpression)
            {
                return CreateElementFactoryForDerivedTable(expression, derivedTableSqlExpression);
            }

            throw new NotSupportedException($"SQL expression of type {sqlExpression.GetType().Name} is not supported.");
        }

        private Func<IDataReader, object> CreateElementFactoryForDerivedTable(Expression expression, SqlDerivedTableExpression derivedTable)
        {
            // Implement the logic to create the element factory for derived tables
            throw new NotImplementedException();
        }
    }
}
