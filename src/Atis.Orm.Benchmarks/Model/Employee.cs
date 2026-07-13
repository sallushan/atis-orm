using System;
using LinqToDB.Mapping;

namespace Atis.Orm.Benchmarks.Model
{
    /// <summary>
    /// Shared POCO mapped to [dbo].[Employee] (seeded by <see cref="Data.BenchmarkDatabase"/>).
    /// Every ORM materializes into this exact type so hydration cost is compared apples-to-apples.
    /// LinqToDB attributes are read only by linq2db; EF Core and Atis map via their own fluent config.
    /// </summary>
    [Table("Employee", Schema = "dbo")]
    public class Employee
    {
        [PrimaryKey, Identity]
        [Column("EmployeeId")]
        public int EmployeeId { get; set; }

        [Column("FirstName")]
        public string FirstName { get; set; }

        [Column("LastName")]
        public string LastName { get; set; }

        [Column("Email")]
        public string Email { get; set; }

        [Column("HireDate")]
        public DateTime? HireDate { get; set; }

        [Column("Salary")]
        public decimal Salary { get; set; }

        [Column("DepartmentId")]
        public int? DepartmentId { get; set; }

        [Column("ManagerId")]
        public int? ManagerId { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; }

        [Column("CreatedDate")]
        public DateTime CreatedDate { get; set; }

        [Column("ModifiedDate")]
        public DateTime? ModifiedDate { get; set; }
    }
}
