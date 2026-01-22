using System;

namespace Atis.SqlExpressionEngine.UnitTest.TestEntities
{
    /// <summary>
    /// Represents a department in the organization
    /// </summary>
    public class Department
    {
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public string Location { get; set; }
        public decimal? Budget { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Represents an employee in the organization
    /// </summary>
    public class Employee
    {
        public int EmployeeId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public DateTime? HireDate { get; set; }
        public decimal Salary { get; set; }
        public int? DepartmentId { get; set; }
        public int? ManagerId { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }

    /// <summary>
    /// Represents a project in the organization
    /// </summary>
    public class Project
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal? Budget { get; set; }
        public int? LeadEmployeeId { get; set; }
        public int DepartmentId { get; set; }
        public string Status { get; set; }
    }

    /// <summary>
    /// Represents assignment of employees to projects (many-to-many relationship)
    /// </summary>
    public class ProjectAssignment
    {
        public int AssignmentId { get; set; }
        public int EmployeeId { get; set; }
        public int ProjectId { get; set; }
        public DateTime AssignedDate { get; set; }
        public DateTime? RemovedDate { get; set; }
        public string Role { get; set; }
        public decimal? HoursAllocated { get; set; }
    }

    /// <summary>
    /// Represents an employee's skill
    /// </summary>
    public class EmployeeSkill
    {
        public int SkillId { get; set; }
        public int EmployeeId { get; set; }
        public string SkillName { get; set; }
        public int ProficiencyLevel { get; set; } // 1-5
        public int? YearsOfExperience { get; set; }
        public DateTime? CertifiedDate { get; set; }
    }

    /// <summary>
    /// Represents an audit log entry for tracking changes
    /// </summary>
    public class AuditLog
    {
        public long AuditId { get; set; }
        public string TableName { get; set; }
        public string Operation { get; set; } // INSERT, UPDATE, DELETE
        public int RecordId { get; set; }
        public string OldValues { get; set; }
        public string NewValues { get; set; }
        public DateTime ChangedDate { get; set; }
        public int? ChangedByEmployeeId { get; set; }
    }
}
