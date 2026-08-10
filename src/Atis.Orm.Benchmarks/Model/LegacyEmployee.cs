using System;
using Legacy = global::Atis.ORM;

namespace Atis.Orm.Benchmarks.Model
{
    /// <summary>
    /// <see cref="Employee"/> as the legacy Atis.ORM 9.16.4 engine needs to see it — same table,
    /// same eleven columns, same CLR types. It exists for the same reason <see cref="LegacyPost"/>
    /// does: the legacy engine requires entities to derive from <c>Atis.ORM.Record</c>, and putting
    /// that base class on the shared <see cref="Employee"/> would leak <c>RecordState</c> into the
    /// other ORMs' mappings.
    ///
    /// Only the columns the TopN scenario selects are strictly needed, but the full row is mapped so
    /// the entity is a faithful stand-in rather than a benchmark-shaped shortcut.
    /// </summary>
    [Legacy.Table("dbo.Employee")]
    public class LegacyEmployee : Legacy.Record
    {
        [Legacy.TableColumn("EmployeeId", IsPrimaryKey = true)]
        public int EmployeeId { get; set; }

        [Legacy.TableColumn("FirstName")]
        public string FirstName { get; set; }

        [Legacy.TableColumn("LastName")]
        public string LastName { get; set; }

        [Legacy.TableColumn("Email")]
        public string Email { get; set; }

        [Legacy.TableColumn("HireDate")]
        public DateTime? HireDate { get; set; }

        [Legacy.TableColumn("Salary")]
        public decimal Salary { get; set; }

        [Legacy.TableColumn("DepartmentId")]
        public int? DepartmentId { get; set; }

        [Legacy.TableColumn("ManagerId")]
        public int? ManagerId { get; set; }

        [Legacy.TableColumn("IsActive")]
        public bool IsActive { get; set; }

        [Legacy.TableColumn("CreatedDate")]
        public DateTime CreatedDate { get; set; }

        [Legacy.TableColumn("ModifiedDate")]
        public DateTime? ModifiedDate { get; set; }
    }
}
