using System;
using System.Collections.Generic;
using System.Text;

namespace Atis.Orm.Annotations
{
    [AttributeUsage(AttributeTargets.Class)]
    public class DbTableAttribute : Attribute
    {
        public DbTableAttribute(string tableName)
            : this(tableName, null, null, null)
        {
        }

        public DbTableAttribute(string tableName, string schema)
            : this(tableName, schema, null, null)
        {
        }

        public DbTableAttribute(string tableName, string schema, string database, string server)
        {
            this.TableName = tableName;
            this.Schema = schema;
            this.Database = database;
            this.Server = server;
        }

        public string TableName { get; }
        public string Schema { get; }
        public string Database { get; }
        public string Server { get; }
    }
}
