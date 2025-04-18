﻿using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Atis.SqlExpressionEngine.SqlExpressions
{
    public class SqlSubQueryExpression : SqlQueryExpression
    {
        public SqlSubQueryExpression(SqlQueryExpression sqlQuery) : base(sqlQuery.SqlFactory)
        {
            Copy(sqlQuery, this);
        }

        public override string ToString()
        {
            var dataSourcesAliases = string.Join(", ", this.AllQuerySources.Select(x => DebugAliasGenerator.GetAlias(x)));
            return $"{(this.IsCte ? "Cte-Sub-Query" : "Sub-Query")}: {dataSourcesAliases}";
        }
    }
}
