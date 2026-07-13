using System;
using LinqToDB.Mapping;

namespace Atis.Orm.Benchmarks.Model
{
    /// <summary>
    /// Shared POCO mapped to [dbo].[Department] (seeded by <see cref="Data.BenchmarkDatabase"/>).
    /// </summary>
    [Table("Department", Schema = "dbo")]
    public class Department
    {
        [PrimaryKey, Identity]
        [Column("DepartmentId")]
        public int DepartmentId { get; set; }

        [Column("DepartmentName")]
        public string DepartmentName { get; set; }

        [Column("Location")]
        public string Location { get; set; }

        [Column("Budget")]
        public decimal? Budget { get; set; }

        [Column("CreatedDate")]
        public DateTime CreatedDate { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; }
    }
}
