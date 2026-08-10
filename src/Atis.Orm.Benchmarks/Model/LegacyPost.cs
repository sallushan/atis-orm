using System;
using Legacy = global::Atis.ORM;

namespace Atis.Orm.Benchmarks.Model
{
    /// <summary>
    /// <see cref="Post"/> as the legacy Atis.ORM 9.16.4 engine needs to see it: same table, same
    /// thirteen columns, same CLR types — so materialization cost is identical and the legacy row in
    /// the summary is comparable to every other contender's.
    ///
    /// It cannot simply be <see cref="Post"/>. The legacy engine requires entities to derive from
    /// <c>Atis.ORM.Record</c>: its column enumeration only skips the inherited <c>RecordState</c>
    /// property for types that are <c>Record</c> subclasses, so a POCO that merely looked like one
    /// would have <c>RecordState</c> emitted as a column and the query would fail. Pushing that base
    /// class onto the shared <see cref="Post"/> instead would leak <c>RecordState</c> into EF Core's
    /// and linq2db's mappings, which is why this is a separate type rather than a shared one.
    ///
    /// The <c>[Table]</c>/<c>[TableColumn]</c> attributes below are <c>Atis.ORM</c>'s own, unrelated to
    /// the <c>LinqToDB.Mapping</c> attributes on <see cref="Post"/>. The legacy engine emits table
    /// names verbatim, hence the schema-qualified "dbo.Posts".
    /// </summary>
    [Legacy.Table("dbo.Posts")]
    public class LegacyPost : Legacy.Record
    {
        // IsPrimaryKey is what DataEntity<T,K>.Get(key) builds its WHERE from. The benchmarks below
        // filter with an explicit predicate instead, but declaring the key keeps the entity honest.
        [Legacy.TableColumn("Id", IsPrimaryKey = true)]
        public int Id { get; set; }

        [Legacy.TableColumn("Text")]
        public string Text { get; set; }

        [Legacy.TableColumn("CreationDate")]
        public DateTime CreationDate { get; set; }

        [Legacy.TableColumn("LastChangeDate")]
        public DateTime LastChangeDate { get; set; }

        [Legacy.TableColumn("Counter1")]
        public int? Counter1 { get; set; }

        [Legacy.TableColumn("Counter2")]
        public int? Counter2 { get; set; }

        [Legacy.TableColumn("Counter3")]
        public int? Counter3 { get; set; }

        [Legacy.TableColumn("Counter4")]
        public int? Counter4 { get; set; }

        [Legacy.TableColumn("Counter5")]
        public int? Counter5 { get; set; }

        [Legacy.TableColumn("Counter6")]
        public int? Counter6 { get; set; }

        [Legacy.TableColumn("Counter7")]
        public int? Counter7 { get; set; }

        [Legacy.TableColumn("Counter8")]
        public int? Counter8 { get; set; }

        [Legacy.TableColumn("Counter9")]
        public int? Counter9 { get; set; }
    }
}
