using Atis.Expressions;
using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Atis.SqlExpressionEngine.Abstractions
{
    public interface ILinqToSqlExpressionTreeConverterFactory
    {
        IExpressionTreeConverter<Expression, SqlExpression> Create();
    }
}
