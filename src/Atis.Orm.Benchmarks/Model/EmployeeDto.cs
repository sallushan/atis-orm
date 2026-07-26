namespace Atis.Orm.Benchmarks.Model
{
    /// <summary>
    /// Shared projection target for the TopN benchmark. Every ORM projects into this exact shape
    /// (a typical "list view" DTO) so materialization cost is compared apples-to-apples.
    ///
    /// The LINQ providers use member-init (<c>new EmployeeDto { … }</c>), not the constructor —
    /// Atis rejects a plain New expression with "Members of the new expression are not set". Dapper
    /// maps columns by name through the parameterless constructor and the settable properties.
    /// </summary>
    public class EmployeeDto
    {
        public EmployeeDto() { }

        public EmployeeDto(int employeeId, string firstName, string lastName, decimal salary)
        {
            EmployeeId = employeeId;
            FirstName = firstName;
            LastName = lastName;
            Salary = salary;
        }

        public int EmployeeId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public decimal Salary { get; set; }
    }
}
