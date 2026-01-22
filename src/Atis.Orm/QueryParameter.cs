using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Atis.Orm
{
    /// <summary>
    ///     <para>
    ///         Represents a query parameter used in SQL translation.
    ///     </para>
    ///     <para>
    ///         This class holds the parameter name, initial value, and a reference to the source SQL expression.
    ///     </para>
    /// </summary>
    public class QueryParameter : IQueryParameter
    {
        /// <summary>
        ///     <para>
        ///         Initializes a new instance of the <see cref="QueryParameter"/> class.
        ///     </para>
        /// </summary>
        /// <param name="name">The parameter name (e.g., "@p0").</param>
        /// <param name="initialValue">The initial value of the parameter at translation time.</param>
        /// <param name="isLiteral">
        ///     <para>
        ///         A flag indicating whether this parameter represents a literal value.
        ///     </para>
        ///     <para>
        ///         If <c>true</c>, the <paramref name="initialValue"/> will be used directly at execution time.
        ///     </para>
        ///     <para>
        ///         If <c>false</c>, the value will be re-extracted from the LINQ expression at execution time.
        ///     </para>
        /// </param>
        /// <param name="sqlParameterExpression">The source SQL expression (either <see cref="SqlLiteralExpression"/> or <see cref="SqlParameterExpression"/>).</param>
        public QueryParameter(string name, object initialValue, bool isLiteral, SqlExpression sqlParameterExpression)
        {
            this.Name = name;
            this.InitialValue = initialValue;
            this.IsLiteral = isLiteral;
            this.SqlParameterExpression = sqlParameterExpression;
        }

        /// <inheritdoc />
        public string Name { get; }

        /// <inheritdoc />
        public object InitialValue { get; }

        /// <inheritdoc />
        public bool IsLiteral { get; }

        /// <inheritdoc />
        public SqlExpression SqlParameterExpression { get; }
    }
}