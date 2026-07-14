using Atis.SqlExpressionEngine.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

namespace Atis.Orm.Abstractions
{
    public interface IQueryParameter
    {
        string Name { get; }
        object InitialValue { get; }
        bool IsLiteral { get; }

        /// <summary>
        ///     Stable identity of the source variable node (see <c>VariableIdentity</c>) used to rebind this
        ///     parameter's value by lookup on a cache hit. <c>null</c> for literals (which keep
        ///     <see cref="InitialValue"/> and are never re-extracted).
        /// </summary>
        string ParameterIdentity { get; }

        SqlExpression SqlParameterExpression { get; }
    }
}
