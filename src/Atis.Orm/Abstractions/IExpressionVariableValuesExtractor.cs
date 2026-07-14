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
        ///     Returns the values of the variable (parameter) nodes keyed by their stable identity
        ///     (see <c>VariableIdentity</c>). Used to rebind cached query parameters by lookup on a cache hit,
        ///     which is order-independent (the translator's parameter order may differ from LINQ visit order
        ///     after SqlExpression reshaping). The same variable referenced multiple times collapses to one
        ///     entry; two distinct variables that resolve to the same identity but different values are a
        ///     defect and throw.
        /// </summary>
        IReadOnlyDictionary<string, object> ExtractVariableValuesByIdentity(Expression sqlExpression);

        /// <summary>
        ///     Returns the variable (parameter) nodes themselves, in visit order. Used to compare the
        ///     variable sequence of the original vs. preprocessed expression (see
        ///     <see cref="IPreprocessingRequirementTester"/>).
        /// </summary>
        IReadOnlyList<Expression> ExtractParameterNodes(Expression sqlExpression);
    }
}
