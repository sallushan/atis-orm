using Atis.Orm;
using Atis.Orm.Abstractions;
using Atis.Orm.Annotations;
using Atis.Orm.DataAccess;
using Atis.Orm.SqlServer;
using Atis.Orm.Translation;
using Atzonix.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Data;
using System.Data.Common;
using System.Linq;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    /// <summary>
    ///     <para>
    ///         A provider's <see cref="ISqlNaming"/> reaches every statement a save sends: the ones the
    ///         query translator writes and the chunk statements the persister writes itself.
    ///     </para>
    ///     <para>
    ///         The table and its columns are named so that they only work quoted — a space in the table
    ///         and key names, and a column called <c>Order</c>. The default naming writes them bare, which
    ///         SQL Server rejects, so a statement that skipped the naming service would fail here
    ///         instead of passing by luck.
    ///     </para>
    /// </summary>
    [TestClass]
    public class SqlNamingExecutionTests
    {
        private const string ServerConnectionString =
            "Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True";

        [TestInitialize]
        public void EnsureDatabase() => new TestDatabaseSetup(ServerConnectionString).Setup();

        [DbTable("Streamed Naming Doc", "dbo")]
        public class NamingDocument : Record
        {
            [PrimaryKey]
            [DbIdentityColumn]
            [DbColumn("Doc Id")]
            public int Id { get; set; }

            [DbColumn("Order")]
            public string OrderNo { get; set; }

            [DbColumn("File Content")]
            public byte[] Content { get; set; }
        }

        /// <summary>Brackets every part, as a SQL Server provider that quotes identifiers would.</summary>
        private sealed class BracketNaming : SqlNaming
        {
            public override string GetQualifiedTableName(Atis.SqlExpressionEngine.SqlExpressions.SqlTable table)
            {
                var parts = new[] { table.Server, table.Database, table.Schema, table.TableName };
                return string.Join(".", parts.Where(x => !string.IsNullOrEmpty(x)).Select(Bracket));
            }

            public override string GetColumnName(string columnName) => Bracket(columnName);

            private static string Bracket(string name) => "[" + name.Replace("]", "]]") + "]";
        }

        /// <summary>Swaps in <see cref="BracketNaming"/>. Its own extension type gives the context its own provider and query cache.</summary>
        private sealed class BracketNamingExtension : IServiceContextExtension
        {
            public void AddServices(IServiceCollection services)
                => services.Replace(ServiceDescriptor.Singleton<ISqlNaming, BracketNaming>());
        }

        private sealed class BracketNamingDbContext : Atis.Orm.DataContext
        {
            protected override void OnConfiguring(DataContextConfiguration config)
            {
                config.UseSqlServer($"Server=.;Database={TestDatabaseSetup.DatabaseName};Integrated Security=true;Encrypt=True;TrustServerCertificate=True");
                config.AddOrUpdateExtension(new BracketNamingExtension());
            }

            public IDbCommunication GetDbCommunication() => this.DbCommunication;
        }

        [TestMethod]
        public void Queries_and_streamed_chunks_both_spell_names_through_the_provider_naming()
        {
            var db = new BracketNamingDbContext();
            db.TransactionWithRollback(() =>
            {
                db.GetDbCommunication().ExecuteNonQueryCommand(
                    "CREATE TABLE dbo.[Streamed Naming Doc] (" +
                    "[Doc Id] INT IDENTITY(1,1) PRIMARY KEY, [Order] NVARCHAR(50) NULL, [File Content] VARBINARY(MAX) NULL)",
                    Array.Empty<DbParameter>(),
                    CommandType.Text);

                var content = new byte[2500];
                for (var i = 0; i < content.Length; i++)
                    content[i] = (byte)(i % 251);
                var document = new NamingDocument { OrderNo = "A-1", Content = content, RecordState = RecordState.Added };
                var chunks = 0;

                // The insert comes from the translator; the three chunks come from the persister.
                db.SaveWithProgress(document, _ => chunks++, chunkSizeBytes: 1000);

                Assert.AreEqual(3, chunks);
                var stored = db.CreateQuery<NamingDocument>()
                               .Where(x => x.Id == document.Id)
                               .Select(x => new { x.OrderNo, x.Content })
                               .ToList();
                Assert.AreEqual(1, stored.Count);
                Assert.AreEqual("A-1", stored[0].OrderNo);
                CollectionAssert.AreEqual(content, stored[0].Content);
            });
        }
    }
}
