using System.Collections.Generic;
using System.Data.Common;
using Atis.Orm.Abstractions;

public class DbCommandInfo
{
    public string Sql { get; }
    public IReadOnlyList<DbParameter> DbParameters { get; }

    public DbCommandInfo(string sql, IReadOnlyList<DbParameter> dbParameters)
    {
        this.Sql = sql ?? throw new System.ArgumentNullException(nameof(sql));
        this.DbParameters = dbParameters;
    }
}
public interface IDbCommandBuilder
{
    DbCommandInfo BuildDbCommand(string sqlTemplate, IReadOnlyList<IQueryParameter> queryParameters, bool useInitialValues);
}