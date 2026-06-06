using System;
using System.Collections.Generic;
using System.Text;

namespace Atis.Orm
{
    public class SqlTranslationResult
    {
        public string Sql { get; }
        public IReadOnlyList<IQueryParameter> QueryParameters { get; }

        public SqlTranslationResult(string sql, IReadOnlyList<IQueryParameter> queryParameters)
        {
            this.Sql = sql ?? throw new ArgumentNullException(nameof(sql));
            this.QueryParameters = queryParameters ?? throw new ArgumentNullException(nameof(queryParameters));
        }
    }
}
