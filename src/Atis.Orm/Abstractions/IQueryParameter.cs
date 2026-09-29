using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

namespace Atis.Orm.Abstractions
{
    public interface IQueryParameter
    {
        object InitialValue { get; }
        bool IsLiteral { get; }

        /// <summary>
        ///     Stable identity of the source variable node (see <c>VariableIdentity</c>) used to rebind this
        ///     parameter's value by lookup on a cache hit. <c>null</c> for literals (which keep
        ///     <see cref="InitialValue"/> and are never re-extracted).
        /// </summary>
        string ParameterIdentity { get; }

        /// <summary>
        ///     Key under which the <see cref="IQueryContext"/> supplies this parameter's value, or <c>null</c>
        ///     for an ordinary parameter. When set, <see cref="InitialValue"/> is meaningless and the value is
        ///     asked for on every execution, the first one included.
        /// </summary>
        string ContextKey { get; }

        SqlExpression SqlParameterExpression { get; }
    }
}
