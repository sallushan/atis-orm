using Atis.Expressions;
using Atis.Orm;
using Atis.SqlExpressionEngine;
using Atis.SqlExpressionEngine.Abstractions;
using Atis.SqlExpressionEngine.Services;
using Atis.SqlExpressionEngine.SqlExpressions;
using Atis.SqlExpressionEngine.UnitTest.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Atis.Orm.SqlServer;

namespace Atis.SqlExpressionEngine.UnitTest
{
    internal class OrmDbContext : DataContext
    {
        public OrmDbContext(DataContextConfiguration config) : base(config) { }

        public OrmDbContext()
        {
        }

        protected override void OnConfiguring(DataContextConfiguration config)
        {
            config.UseSqlServer($"Server=.;Database={TestDatabaseSetup.DatabaseName};Integrated Security=true;Encrypt=True;TrustServerCertificate=True");
            config.UseUnitTestCustomization();
        }

        // Exposes the built (immutable) metadata so tests can assert what the fluent API produced.
        public EntityMetadata GetEntityMetadata<T>()
        {
            this.Model.TryGet(typeof(T), out var metadata);
            return metadata;
        }

        internal static int _onModelCreatingCallCount = 0;
        protected override void OnModelCreating(ModelBuilder mb)
        {
            _onModelCreatingCallCount++;

            mb.Entity<SimulatedExternalEntity>(entity =>
            {
                entity.ToTable("SIM_EXT_TBL")
                        .Column(x => x.PrimaryKey, "PK").MarkAsKey();
                entity.Column(x => x.SomeOtherField).SetColumnName("FLD2");
            });

            mb.Entity<FluentAuthor>(e =>
            {
                e.ToTable("AUTHOR", "dbo");
                e.HasKey(x => x.Id);
                e.Column(x => x.FirstName, "FRST_NM");
                e.Column(x => x.LastName, "LAST_NM");
                // key-based one-to-many
                e.HasMany(x => x.Books, parentKey: a => a.Id, childKey: b => b.AuthorId);
                // key-based one-to-one (this entity is principal)
                e.HasChild(x => x.PrimaryBook, parentKey: a => a.Id, childKey: b => b.AuthorId);
                // key-based optional many-to-one
                e.HasParent(x => x.Country, parentKey: c => c.Id, childKey: a => a.CountryId).Optional();
                // calculated property
                e.Calculated(x => x.FullName, x => x.FirstName + " " + x.LastName);
            });

            mb.Entity<FluentBook>(e =>
            {
                e.ToTable("BOOK");
                e.HasKey(x => x.Id);
                e.Column(x => x.Title, "BOOK_TITLE");
                // explicit-lambda many-to-one (required)
                e.HasParent(x => x.Author, (author, book) => author.Id == book.AuthorId);
            });

            mb.Entity<FluentCountry>(e =>
            {
                e.ToTable("COUNTRY");
                e.HasKey(x => x.Id);
            });
        }
    }
}
