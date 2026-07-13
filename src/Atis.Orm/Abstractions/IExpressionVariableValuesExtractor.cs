using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Atis.Orm.Abstractions
{
    public interface IExpressionVariableValuesExtractor
    {
        /// <summary>
        ///     Returns the values of the variable (parameter) nodes in the expression, in visit order.
        /// </summary>
        IReadOnlyList<object> ExtractVariableValues(Expression sqlExpression);

        /// <summary>
        ///     Returns the variable (parameter) nodes themselves, in visit order. Used to compare the
        ///     variable sequence of the original vs. preprocessed expression (see
        ///     <see cref="IPreprocessingRequirementTester"/>).
        /// </summary>
        IReadOnlyList<Expression> ExtractParameterNodes(Expression sqlExpression);
    }
}
