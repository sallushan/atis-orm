using System;
using System.Collections.Generic;
using System.Text;

namespace Atis.Orm.Annotations
{
    [AttributeUsage(AttributeTargets.Class)]
    public class DbTableAttribute : Attribute
    {
        /// <summary>
        ///     <para>
        ///         Marks the class as an entity and lets the table name default to the class name.
        ///     </para>
        ///     <para>
        ///         Carrying this attribute is what makes a class an entity to the default
        ///         <c>IEntityMetadataBuilder</c> — see its <c>CanBuild</c>. Entities configured through
        ///         <c>OnModelCreating</c> do not need it.
        ///     </para>
        /// </summary>
        public DbTableAttribute()
            : this(null, null, null, null)
        {
        }

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
