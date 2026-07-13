namespace Atis.Orm.Benchmarks.Model
{
    /// <summary>
    /// Shared projection target for the query benchmark. Every ORM projects into this exact shape
    /// (a typical "list view" DTO) so materialization cost is compared apples-to-apples.
    ///
    /// Has both a constructor (used by the LINQ providers' projection — a New expression, which is
    /// the shape Atis's execution path supports) and settable properties (so Dapper can map columns
    /// by name via the parameterless path).
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
