using System;

namespace Atis.Orm.Benchmarks.Model
{
    /// <summary>
    /// The entity from Dapper's own benchmark suite, reproduced property-for-property
    /// (DapperLib/Dapper, benchmarks/Dapper.Tests.Performance/Post.cs). Keeping the schema
    /// identical is what makes this project's numbers readable against Dapper's published results.
    ///
    /// The shape matters: a wide row with a large <see cref="Text"/> payload and nine nullable
    /// counters is a materialization-heavy target, which is the cost ORMs actually differ on.
    ///
    /// linq2db maps via the attributes below; EF Core and Atis map fluently in their contexts.
    /// Dapper needs no mapping at all — it matches columns to properties by name.
    /// </summary>
    [LinqToDB.Mapping.Table(Name = "Posts")]
    public class Post
    {
        [LinqToDB.Mapping.PrimaryKey, LinqToDB.Mapping.Identity]
        public int Id { get; set; }
        [LinqToDB.Mapping.Column, LinqToDB.Mapping.Nullable]
        public string Text { get; set; }
        [LinqToDB.Mapping.Column, LinqToDB.Mapping.NotNull]
        public DateTime CreationDate { get; set; }
        [LinqToDB.Mapping.Column, LinqToDB.Mapping.NotNull]
        public DateTime LastChangeDate { get; set; }
        [LinqToDB.Mapping.Column, LinqToDB.Mapping.Nullable]
        public int? Counter1 { get; set; }
        [LinqToDB.Mapping.Column, LinqToDB.Mapping.Nullable]
        public int? Counter2 { get; set; }
        [LinqToDB.Mapping.Column, LinqToDB.Mapping.Nullable]
        public int? Counter3 { get; set; }
        [LinqToDB.Mapping.Column, LinqToDB.Mapping.Nullable]
        public int? Counter4 { get; set; }
        [LinqToDB.Mapping.Column, LinqToDB.Mapping.Nullable]
        public int? Counter5 { get; set; }
        [LinqToDB.Mapping.Column, LinqToDB.Mapping.Nullable]
        public int? Counter6 { get; set; }
        [LinqToDB.Mapping.Column, LinqToDB.Mapping.Nullable]
        public int? Counter7 { get; set; }
        [LinqToDB.Mapping.Column, LinqToDB.Mapping.Nullable]
        public int? Counter8 { get; set; }
        [LinqToDB.Mapping.Column, LinqToDB.Mapping.Nullable]
        public int? Counter9 { get; set; }
    }
}
