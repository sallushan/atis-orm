using System;
using System.Collections.Generic;
using System.Text;

using Atis.Orm.Abstractions;
namespace Atis.Orm.Translation
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
