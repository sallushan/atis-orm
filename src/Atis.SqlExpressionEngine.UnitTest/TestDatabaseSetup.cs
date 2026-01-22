using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Threading.Tasks;

namespace Atis.SqlExpressionEngine.UnitTest
{
    /// <summary>
    /// Sets up the test database with tables and sample data for Atis.Orm testing.
    /// </summary>
    public class TestDatabaseSetup
    {
        private readonly string _masterConnectionString;
        private readonly string _databaseConnectionString;
        public const string DatabaseName = "AtisOrmTestDb";

        /// <summary>
        /// Initializes a new instance of TestDatabaseSetup.
        /// </summary>
        /// <param name="serverConnectionString">Connection string to SQL Server (without database specified)</param>
        public TestDatabaseSetup(string serverConnectionString)
        {
            if (string.IsNullOrWhiteSpace(serverConnectionString))
                throw new ArgumentNullException(nameof(serverConnectionString));

            _masterConnectionString = BuildConnectionString(serverConnectionString, "master");
            _databaseConnectionString = BuildConnectionString(serverConnectionString, DatabaseName);
        }

        /// <summary>
        /// Gets the connection string to the test database.
        /// </summary>
        public string ConnectionString => _databaseConnectionString;

        /// <summary>
        /// Runs the complete database setup: creates database, tables, and populates sample data.
        /// </summary>
        public void Setup()
        {
            CreateDatabaseIfNotExists();
            CreateTablesIfNotExist();
            PopulateSampleData();
        }

        /// <summary>
        /// Runs the complete database setup asynchronously.
        /// </summary>
        public async Task SetupAsync()
        {
            await CreateDatabaseIfNotExistsAsync();
            await CreateTablesIfNotExistAsync();
            await PopulateSampleDataAsync();
        }

        /// <summary>
        /// Drops and recreates the database with fresh data.
        /// </summary>
        public void Reset()
        {
            DropDatabaseIfExists();
            Setup();
        }

        /// <summary>
        /// Drops and recreates the database with fresh data asynchronously.
        /// </summary>
        public async Task ResetAsync()
        {
            await DropDatabaseIfExistsAsync();
            await SetupAsync();
        }

        #region Database Creation

        private void CreateDatabaseIfNotExists()
        {
            using (var connection = new SqlConnection(_masterConnectionString))
            {
                connection.Open();
                
                var exists = DatabaseExists(connection);
                if (!exists)
                {
                    ExecuteNonQuery(connection, $"CREATE DATABASE [{DatabaseName}]");
                    Console.WriteLine($"Database {DatabaseName} created.");
                }
                else
                {
                    Console.WriteLine($"Database {DatabaseName} already exists.");
                }
            }
        }

        private async Task CreateDatabaseIfNotExistsAsync()
        {
            using (var connection = new SqlConnection(_masterConnectionString))
            {
                await connection.OpenAsync();
                
                var exists = await DatabaseExistsAsync(connection);
                if (!exists)
                {
                    await ExecuteNonQueryAsync(connection, $"CREATE DATABASE [{DatabaseName}]");
                    Console.WriteLine($"Database {DatabaseName} created.");
                }
                else
                {
                    Console.WriteLine($"Database {DatabaseName} already exists.");
                }
            }
        }

        private void DropDatabaseIfExists()
        {
            using (var connection = new SqlConnection(_masterConnectionString))
            {
                connection.Open();
                
                var exists = DatabaseExists(connection);
                if (exists)
                {
                    // Set to single user mode to force disconnect other connections
                    ExecuteNonQuery(connection, $@"
                        ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                        DROP DATABASE [{DatabaseName}];");
                    Console.WriteLine($"Database {DatabaseName} dropped.");
                }
            }
        }

        private async Task DropDatabaseIfExistsAsync()
        {
            using (var connection = new SqlConnection(_masterConnectionString))
            {
                await connection.OpenAsync();
                
                var exists = await DatabaseExistsAsync(connection);
                if (exists)
                {
                    await ExecuteNonQueryAsync(connection, $@"
                        ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                        DROP DATABASE [{DatabaseName}];");
                    Console.WriteLine($"Database {DatabaseName} dropped.");
                }
            }
        }

        private bool DatabaseExists(SqlConnection connection)
        {
            using (var cmd = new SqlCommand("SELECT COUNT(*) FROM sys.databases WHERE name = @name", connection))
            {
                cmd.Parameters.AddWithValue("@name", DatabaseName);
                return (int)cmd.ExecuteScalar() > 0;
            }
        }

        private async Task<bool> DatabaseExistsAsync(SqlConnection connection)
        {
            using (var cmd = new SqlCommand("SELECT COUNT(*) FROM sys.databases WHERE name = @name", connection))
            {
                cmd.Parameters.AddWithValue("@name", DatabaseName);
                return (int)await cmd.ExecuteScalarAsync() > 0;
            }
        }

        #endregion

        #region Table Creation

        private void CreateTablesIfNotExist()
        {
            using (var connection = new SqlConnection(_databaseConnectionString))
            {
                connection.Open();
                
                CreateDepartmentTable(connection);
                CreateEmployeeTable(connection);
                CreateProjectTable(connection);
                CreateProjectAssignmentTable(connection);
                CreateEmployeeSkillTable(connection);
                CreateAuditLogTable(connection);
            }
        }

        private async Task CreateTablesIfNotExistAsync()
        {
            using (var connection = new SqlConnection(_databaseConnectionString))
            {
                await connection.OpenAsync();
                
                await CreateDepartmentTableAsync(connection);
                await CreateEmployeeTableAsync(connection);
                await CreateProjectTableAsync(connection);
                await CreateProjectAssignmentTableAsync(connection);
                await CreateEmployeeSkillTableAsync(connection);
                await CreateAuditLogTableAsync(connection);
            }
        }

        private void CreateDepartmentTable(SqlConnection connection)
        {
            if (!TableExists(connection, "Department"))
            {
                ExecuteNonQuery(connection, @"
                    CREATE TABLE [dbo].[Department] (
                        [DepartmentId] INT IDENTITY(1,1) PRIMARY KEY,
                        [DepartmentName] NVARCHAR(100) NOT NULL,
                        [Location] NVARCHAR(100) NULL,
                        [Budget] DECIMAL(18,2) NULL,
                        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
                        [IsActive] BIT NOT NULL DEFAULT 1
                    )");
                Console.WriteLine("Table Department created.");
            }
        }

        private async Task CreateDepartmentTableAsync(SqlConnection connection)
        {
            if (!await TableExistsAsync(connection, "Department"))
            {
                await ExecuteNonQueryAsync(connection, @"
                    CREATE TABLE [dbo].[Department] (
                        [DepartmentId] INT IDENTITY(1,1) PRIMARY KEY,
                        [DepartmentName] NVARCHAR(100) NOT NULL,
                        [Location] NVARCHAR(100) NULL,
                        [Budget] DECIMAL(18,2) NULL,
                        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
                        [IsActive] BIT NOT NULL DEFAULT 1
                    )");
                Console.WriteLine("Table Department created.");
            }
        }

        private void CreateEmployeeTable(SqlConnection connection)
        {
            if (!TableExists(connection, "Employee"))
            {
                ExecuteNonQuery(connection, @"
                    CREATE TABLE [dbo].[Employee] (
                        [EmployeeId] INT IDENTITY(1,1) PRIMARY KEY,
                        [FirstName] NVARCHAR(50) NOT NULL,
                        [LastName] NVARCHAR(50) NOT NULL,
                        [Email] NVARCHAR(100) NOT NULL,
                        [HireDate] DATE NULL,
                        [Salary] DECIMAL(18,2) NOT NULL,
                        [DepartmentId] INT NULL,
                        [ManagerId] INT NULL,
                        [IsActive] BIT NOT NULL DEFAULT 1,
                        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
                        [ModifiedDate] DATETIME NULL,
                        CONSTRAINT [FK_Employee_Department] FOREIGN KEY ([DepartmentId]) REFERENCES [dbo].[Department]([DepartmentId]),
                        CONSTRAINT [FK_Employee_Manager] FOREIGN KEY ([ManagerId]) REFERENCES [dbo].[Employee]([EmployeeId])
                    )");
                Console.WriteLine("Table Employee created.");
            }
        }

        private async Task CreateEmployeeTableAsync(SqlConnection connection)
        {
            if (!await TableExistsAsync(connection, "Employee"))
            {
                await ExecuteNonQueryAsync(connection, @"
                    CREATE TABLE [dbo].[Employee] (
                        [EmployeeId] INT IDENTITY(1,1) PRIMARY KEY,
                        [FirstName] NVARCHAR(50) NOT NULL,
                        [LastName] NVARCHAR(50) NOT NULL,
                        [Email] NVARCHAR(100) NOT NULL,
                        [HireDate] DATE NULL,
                        [Salary] DECIMAL(18,2) NOT NULL,
                        [DepartmentId] INT NULL,
                        [ManagerId] INT NULL,
                        [IsActive] BIT NOT NULL DEFAULT 1,
                        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
                        [ModifiedDate] DATETIME NULL,
                        CONSTRAINT [FK_Employee_Department] FOREIGN KEY ([DepartmentId]) REFERENCES [dbo].[Department]([DepartmentId]),
                        CONSTRAINT [FK_Employee_Manager] FOREIGN KEY ([ManagerId]) REFERENCES [dbo].[Employee]([EmployeeId])
                    )");
                Console.WriteLine("Table Employee created.");
            }
        }

        private void CreateProjectTable(SqlConnection connection)
        {
            if (!TableExists(connection, "Project"))
            {
                ExecuteNonQuery(connection, @"
                    CREATE TABLE [dbo].[Project] (
                        [ProjectId] INT IDENTITY(1,1) PRIMARY KEY,
                        [ProjectName] NVARCHAR(100) NOT NULL,
                        [Description] NVARCHAR(500) NULL,
                        [StartDate] DATE NOT NULL,
                        [EndDate] DATE NULL,
                        [Budget] DECIMAL(18,2) NULL,
                        [LeadEmployeeId] INT NULL,
                        [DepartmentId] INT NOT NULL,
                        [Status] NVARCHAR(20) NOT NULL DEFAULT 'Active',
                        CONSTRAINT [FK_Project_Employee] FOREIGN KEY ([LeadEmployeeId]) REFERENCES [dbo].[Employee]([EmployeeId]),
                        CONSTRAINT [FK_Project_Department] FOREIGN KEY ([DepartmentId]) REFERENCES [dbo].[Department]([DepartmentId])
                    )");
                Console.WriteLine("Table Project created.");
            }
        }

        private async Task CreateProjectTableAsync(SqlConnection connection)
        {
            if (!await TableExistsAsync(connection, "Project"))
            {
                await ExecuteNonQueryAsync(connection, @"
                    CREATE TABLE [dbo].[Project] (
                        [ProjectId] INT IDENTITY(1,1) PRIMARY KEY,
                        [ProjectName] NVARCHAR(100) NOT NULL,
                        [Description] NVARCHAR(500) NULL,
                        [StartDate] DATE NOT NULL,
                        [EndDate] DATE NULL,
                        [Budget] DECIMAL(18,2) NULL,
                        [LeadEmployeeId] INT NULL,
                        [DepartmentId] INT NOT NULL,
                        [Status] NVARCHAR(20) NOT NULL DEFAULT 'Active',
                        CONSTRAINT [FK_Project_Employee] FOREIGN KEY ([LeadEmployeeId]) REFERENCES [dbo].[Employee]([EmployeeId]),
                        CONSTRAINT [FK_Project_Department] FOREIGN KEY ([DepartmentId]) REFERENCES [dbo].[Department]([DepartmentId])
                    )");
                Console.WriteLine("Table Project created.");
            }
        }

        private void CreateProjectAssignmentTable(SqlConnection connection)
        {
            if (!TableExists(connection, "ProjectAssignment"))
            {
                ExecuteNonQuery(connection, @"
                    CREATE TABLE [dbo].[ProjectAssignment] (
                        [AssignmentId] INT IDENTITY(1,1) PRIMARY KEY,
                        [EmployeeId] INT NOT NULL,
                        [ProjectId] INT NOT NULL,
                        [AssignedDate] DATE NOT NULL DEFAULT GETDATE(),
                        [RemovedDate] DATE NULL,
                        [Role] NVARCHAR(50) NULL,
                        [HoursAllocated] DECIMAL(5,2) NULL,
                        CONSTRAINT [FK_ProjectAssignment_Employee] FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[Employee]([EmployeeId]),
                        CONSTRAINT [FK_ProjectAssignment_Project] FOREIGN KEY ([ProjectId]) REFERENCES [dbo].[Project]([ProjectId])
                    )");
                Console.WriteLine("Table ProjectAssignment created.");
            }
        }

        private async Task CreateProjectAssignmentTableAsync(SqlConnection connection)
        {
            if (!await TableExistsAsync(connection, "ProjectAssignment"))
            {
                await ExecuteNonQueryAsync(connection, @"
                    CREATE TABLE [dbo].[ProjectAssignment] (
                        [AssignmentId] INT IDENTITY(1,1) PRIMARY KEY,
                        [EmployeeId] INT NOT NULL,
                        [ProjectId] INT NOT NULL,
                        [AssignedDate] DATE NOT NULL DEFAULT GETDATE(),
                        [RemovedDate] DATE NULL,
                        [Role] NVARCHAR(50) NULL,
                        [HoursAllocated] DECIMAL(5,2) NULL,
                        CONSTRAINT [FK_ProjectAssignment_Employee] FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[Employee]([EmployeeId]),
                        CONSTRAINT [FK_ProjectAssignment_Project] FOREIGN KEY ([ProjectId]) REFERENCES [dbo].[Project]([ProjectId])
                    )");
                Console.WriteLine("Table ProjectAssignment created.");
            }
        }

        private void CreateEmployeeSkillTable(SqlConnection connection)
        {
            if (!TableExists(connection, "EmployeeSkill"))
            {
                ExecuteNonQuery(connection, @"
                    CREATE TABLE [dbo].[EmployeeSkill] (
                        [SkillId] INT IDENTITY(1,1) PRIMARY KEY,
                        [EmployeeId] INT NOT NULL,
                        [SkillName] NVARCHAR(100) NOT NULL,
                        [ProficiencyLevel] INT NOT NULL CHECK ([ProficiencyLevel] BETWEEN 1 AND 5),
                        [YearsOfExperience] INT NULL,
                        [CertifiedDate] DATE NULL,
                        CONSTRAINT [FK_EmployeeSkill_Employee] FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[Employee]([EmployeeId])
                    )");
                Console.WriteLine("Table EmployeeSkill created.");
            }
        }

        private async Task CreateEmployeeSkillTableAsync(SqlConnection connection)
        {
            if (!await TableExistsAsync(connection, "EmployeeSkill"))
            {
                await ExecuteNonQueryAsync(connection, @"
                    CREATE TABLE [dbo].[EmployeeSkill] (
                        [SkillId] INT IDENTITY(1,1) PRIMARY KEY,
                        [EmployeeId] INT NOT NULL,
                        [SkillName] NVARCHAR(100) NOT NULL,
                        [ProficiencyLevel] INT NOT NULL CHECK ([ProficiencyLevel] BETWEEN 1 AND 5),
                        [YearsOfExperience] INT NULL,
                        [CertifiedDate] DATE NULL,
                        CONSTRAINT [FK_EmployeeSkill_Employee] FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[Employee]([EmployeeId])
                    )");
                Console.WriteLine("Table EmployeeSkill created.");
            }
        }

        private void CreateAuditLogTable(SqlConnection connection)
        {
            if (!TableExists(connection, "AuditLog"))
            {
                ExecuteNonQuery(connection, @"
                    CREATE TABLE [dbo].[AuditLog] (
                        [AuditId] BIGINT IDENTITY(1,1) PRIMARY KEY,
                        [TableName] NVARCHAR(100) NOT NULL,
                        [Operation] NVARCHAR(10) NOT NULL,
                        [RecordId] INT NOT NULL,
                        [OldValues] NVARCHAR(MAX) NULL,
                        [NewValues] NVARCHAR(MAX) NULL,
                        [ChangedDate] DATETIME NOT NULL DEFAULT GETDATE(),
                        [ChangedByEmployeeId] INT NULL,
                        CONSTRAINT [FK_AuditLog_Employee] FOREIGN KEY ([ChangedByEmployeeId]) REFERENCES [dbo].[Employee]([EmployeeId])
                    )");
                Console.WriteLine("Table AuditLog created.");
            }
        }

        private async Task CreateAuditLogTableAsync(SqlConnection connection)
        {
            if (!await TableExistsAsync(connection, "AuditLog"))
            {
                await ExecuteNonQueryAsync(connection, @"
                    CREATE TABLE [dbo].[AuditLog] (
                        [AuditId] BIGINT IDENTITY(1,1) PRIMARY KEY,
                        [TableName] NVARCHAR(100) NOT NULL,
                        [Operation] NVARCHAR(10) NOT NULL,
                        [RecordId] INT NOT NULL,
                        [OldValues] NVARCHAR(MAX) NULL,
                        [NewValues] NVARCHAR(MAX) NULL,
                        [ChangedDate] DATETIME NOT NULL DEFAULT GETDATE(),
                        [ChangedByEmployeeId] INT NULL,
                        CONSTRAINT [FK_AuditLog_Employee] FOREIGN KEY ([ChangedByEmployeeId]) REFERENCES [dbo].[Employee]([EmployeeId])
                    )");
                Console.WriteLine("Table AuditLog created.");
            }
        }

        private bool TableExists(SqlConnection connection, string tableName)
        {
            using (var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM sys.objects WHERE object_id = OBJECT_ID(@name) AND type = 'U'", connection))
            {
                cmd.Parameters.AddWithValue("@name", $"[dbo].[{tableName}]");
                return (int)cmd.ExecuteScalar() > 0;
            }
        }

        private async Task<bool> TableExistsAsync(SqlConnection connection, string tableName)
        {
            using (var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM sys.objects WHERE object_id = OBJECT_ID(@name) AND type = 'U'", connection))
            {
                cmd.Parameters.AddWithValue("@name", $"[dbo].[{tableName}]");
                return (int)await cmd.ExecuteScalarAsync() > 0;
            }
        }

        #endregion

        #region Data Population

        private void PopulateSampleData()
        {
            using (var connection = new SqlConnection(_databaseConnectionString))
            {
                connection.Open();
                
                PopulateDepartments(connection);
                PopulateEmployees(connection);
                PopulateProjects(connection);
                PopulateProjectAssignments(connection);
                PopulateEmployeeSkills(connection);
                PopulateAuditLog(connection);
                
                PrintRecordCounts(connection);
            }
        }

        private async Task PopulateSampleDataAsync()
        {
            using (var connection = new SqlConnection(_databaseConnectionString))
            {
                await connection.OpenAsync();
                
                await PopulateDepartmentsAsync(connection);
                await PopulateEmployeesAsync(connection);
                await PopulateProjectsAsync(connection);
                await PopulateProjectAssignmentsAsync(connection);
                await PopulateEmployeeSkillsAsync(connection);
                await PopulateAuditLogAsync(connection);
                
                await PrintRecordCountsAsync(connection);
            }
        }

        private void PopulateDepartments(SqlConnection connection)
        {
            if (GetRecordCount(connection, "Department") > 0) return;

            ExecuteNonQuery(connection, @"
                SET IDENTITY_INSERT [dbo].[Department] ON;
                
                INSERT INTO [dbo].[Department] ([DepartmentId], [DepartmentName], [Location], [Budget], [CreatedDate], [IsActive])
                VALUES
                    (1, 'Engineering', 'Building A - Floor 3', 500000.00, '2020-01-15', 1),
                    (2, 'Human Resources', 'Building B - Floor 1', 150000.00, '2020-01-15', 1),
                    (3, 'Marketing', 'Building A - Floor 2', 300000.00, '2020-02-01', 1),
                    (4, 'Finance', 'Building C - Floor 1', 200000.00, '2020-02-01', 1),
                    (5, 'Research & Development', 'Building D - Floor 4', 750000.00, '2020-03-01', 1),
                    (6, 'Customer Support', 'Building B - Floor 2', 180000.00, '2020-03-15', 1),
                    (7, 'Sales', 'Building A - Floor 1', 400000.00, '2020-04-01', 1),
                    (8, 'IT Infrastructure', 'Building C - Floor 2', 350000.00, '2020-04-15', 1),
                    (9, 'Legal', 'Building C - Floor 3', 250000.00, '2020-05-01', 1),
                    (10, 'Operations', 'Building B - Floor 3', NULL, '2021-01-01', 0);
                
                SET IDENTITY_INSERT [dbo].[Department] OFF;");
            Console.WriteLine("Sample data inserted into Department.");
        }

        private async Task PopulateDepartmentsAsync(SqlConnection connection)
        {
            if (await GetRecordCountAsync(connection, "Department") > 0) return;

            await ExecuteNonQueryAsync(connection, @"
                SET IDENTITY_INSERT [dbo].[Department] ON;
                
                INSERT INTO [dbo].[Department] ([DepartmentId], [DepartmentName], [Location], [Budget], [CreatedDate], [IsActive])
                VALUES
                    (1, 'Engineering', 'Building A - Floor 3', 500000.00, '2020-01-15', 1),
                    (2, 'Human Resources', 'Building B - Floor 1', 150000.00, '2020-01-15', 1),
                    (3, 'Marketing', 'Building A - Floor 2', 300000.00, '2020-02-01', 1),
                    (4, 'Finance', 'Building C - Floor 1', 200000.00, '2020-02-01', 1),
                    (5, 'Research & Development', 'Building D - Floor 4', 750000.00, '2020-03-01', 1),
                    (6, 'Customer Support', 'Building B - Floor 2', 180000.00, '2020-03-15', 1),
                    (7, 'Sales', 'Building A - Floor 1', 400000.00, '2020-04-01', 1),
                    (8, 'IT Infrastructure', 'Building C - Floor 2', 350000.00, '2020-04-15', 1),
                    (9, 'Legal', 'Building C - Floor 3', 250000.00, '2020-05-01', 1),
                    (10, 'Operations', 'Building B - Floor 3', NULL, '2021-01-01', 0);
                
                SET IDENTITY_INSERT [dbo].[Department] OFF;");
            Console.WriteLine("Sample data inserted into Department.");
        }

        private void PopulateEmployees(SqlConnection connection)
        {
            if (GetRecordCount(connection, "Employee") > 0) return;

            // Insert managers first (no manager reference)
            ExecuteNonQuery(connection, @"
                SET IDENTITY_INSERT [dbo].[Employee] ON;
                
                INSERT INTO [dbo].[Employee] ([EmployeeId], [FirstName], [LastName], [Email], [HireDate], [Salary], [DepartmentId], [ManagerId], [IsActive], [CreatedDate], [ModifiedDate])
                VALUES
                    (1, 'John', 'Smith', 'john.smith@company.com', '2019-01-15', 150000.00, 1, NULL, 1, '2019-01-15', NULL),
                    (2, 'Sarah', 'Johnson', 'sarah.johnson@company.com', '2019-02-01', 120000.00, 2, NULL, 1, '2019-02-01', NULL),
                    (3, 'Michael', 'Williams', 'michael.williams@company.com', '2019-03-10', 130000.00, 3, NULL, 1, '2019-03-10', NULL),
                    (4, 'Emily', 'Brown', 'emily.brown@company.com', '2019-04-20', 125000.00, 4, NULL, 1, '2019-04-20', NULL),
                    (5, 'David', 'Jones', 'david.jones@company.com', '2019-05-15', 140000.00, 5, NULL, 1, '2019-05-15', NULL);
                
                INSERT INTO [dbo].[Employee] ([EmployeeId], [FirstName], [LastName], [Email], [HireDate], [Salary], [DepartmentId], [ManagerId], [IsActive], [CreatedDate], [ModifiedDate])
                VALUES
                    (6, 'Jennifer', 'Davis', 'jennifer.davis@company.com', '2020-01-10', 85000.00, 1, 1, 1, '2020-01-10', NULL),
                    (7, 'Robert', 'Miller', 'robert.miller@company.com', '2020-02-15', 90000.00, 1, 1, 1, '2020-02-15', '2023-06-01'),
                    (8, 'Lisa', 'Wilson', 'lisa.wilson@company.com', '2020-03-20', 75000.00, 2, 2, 1, '2020-03-20', NULL),
                    (9, 'James', 'Moore', 'james.moore@company.com', '2020-04-25', 80000.00, 3, 3, 1, '2020-04-25', NULL),
                    (10, 'Patricia', 'Taylor', 'patricia.taylor@company.com', '2020-05-30', 82000.00, 4, 4, 1, '2020-05-30', NULL),
                    (11, 'Christopher', 'Anderson', 'christopher.anderson@company.com', '2020-06-15', 95000.00, 5, 5, 1, '2020-06-15', NULL),
                    (12, 'Amanda', 'Thomas', 'amanda.thomas@company.com', '2020-07-20', 70000.00, 6, 2, 1, '2020-07-20', NULL),
                    (13, 'Daniel', 'Jackson', 'daniel.jackson@company.com', '2020-08-25', 88000.00, 7, 3, 1, '2020-08-25', NULL),
                    (14, 'Michelle', 'White', 'michelle.white@company.com', '2020-09-30', 92000.00, 8, 1, 1, '2020-09-30', '2024-01-15'),
                    (15, 'Matthew', 'Harris', 'matthew.harris@company.com', '2021-01-10', 78000.00, 1, 1, 1, '2021-01-10', NULL),
                    (16, 'Ashley', 'Martin', 'ashley.martin@company.com', '2021-02-15', 72000.00, 3, 3, 1, '2021-02-15', NULL),
                    (17, 'Joshua', 'Garcia', 'joshua.garcia@company.com', '2021-03-20', 86000.00, 5, 5, 1, '2021-03-20', NULL),
                    (18, 'Stephanie', 'Martinez', 'stephanie.martinez@company.com', '2021-04-25', 68000.00, 6, 2, 1, '2021-04-25', NULL),
                    (19, 'Andrew', 'Robinson', 'andrew.robinson@company.com', '2021-05-30', 94000.00, 1, 1, 1, '2021-05-30', NULL),
                    (20, 'Nicole', 'Clark', 'nicole.clark@company.com', NULL, 65000.00, 2, 2, 1, '2022-01-01', NULL),
                    (21, 'Ryan', 'Rodriguez', 'ryan.rodriguez@company.com', '2022-02-15', 89000.00, 7, 3, 0, '2022-02-15', '2023-12-01'),
                    (22, 'Elizabeth', 'Lewis', 'elizabeth.lewis@company.com', '2022-03-20', 76000.00, 4, 4, 1, '2022-03-20', NULL),
                    (23, 'Brandon', 'Lee', 'brandon.lee@company.com', '2022-04-25', 91000.00, 8, 1, 1, '2022-04-25', NULL),
                    (24, 'Samantha', 'Walker', 'samantha.walker@company.com', '2022-05-30', 73000.00, 9, 4, 1, '2022-05-30', NULL),
                    (25, 'Kevin', 'Hall', 'kevin.hall@company.com', '2023-01-10', 67000.00, 1, 6, 1, '2023-01-10', NULL);
                
                SET IDENTITY_INSERT [dbo].[Employee] OFF;");
            Console.WriteLine("Sample data inserted into Employee.");
        }

        private async Task PopulateEmployeesAsync(SqlConnection connection)
        {
            if (await GetRecordCountAsync(connection, "Employee") > 0) return;

            await ExecuteNonQueryAsync(connection, @"
                SET IDENTITY_INSERT [dbo].[Employee] ON;
                
                INSERT INTO [dbo].[Employee] ([EmployeeId], [FirstName], [LastName], [Email], [HireDate], [Salary], [DepartmentId], [ManagerId], [IsActive], [CreatedDate], [ModifiedDate])
                VALUES
                    (1, 'John', 'Smith', 'john.smith@company.com', '2019-01-15', 150000.00, 1, NULL, 1, '2019-01-15', NULL),
                    (2, 'Sarah', 'Johnson', 'sarah.johnson@company.com', '2019-02-01', 120000.00, 2, NULL, 1, '2019-02-01', NULL),
                    (3, 'Michael', 'Williams', 'michael.williams@company.com', '2019-03-10', 130000.00, 3, NULL, 1, '2019-03-10', NULL),
                    (4, 'Emily', 'Brown', 'emily.brown@company.com', '2019-04-20', 125000.00, 4, NULL, 1, '2019-04-20', NULL),
                    (5, 'David', 'Jones', 'david.jones@company.com', '2019-05-15', 140000.00, 5, NULL, 1, '2019-05-15', NULL);
                
                INSERT INTO [dbo].[Employee] ([EmployeeId], [FirstName], [LastName], [Email], [HireDate], [Salary], [DepartmentId], [ManagerId], [IsActive], [CreatedDate], [ModifiedDate])
                VALUES
                    (6, 'Jennifer', 'Davis', 'jennifer.davis@company.com', '2020-01-10', 85000.00, 1, 1, 1, '2020-01-10', NULL),
                    (7, 'Robert', 'Miller', 'robert.miller@company.com', '2020-02-15', 90000.00, 1, 1, 1, '2020-02-15', '2023-06-01'),
                    (8, 'Lisa', 'Wilson', 'lisa.wilson@company.com', '2020-03-20', 75000.00, 2, 2, 1, '2020-03-20', NULL),
                    (9, 'James', 'Moore', 'james.moore@company.com', '2020-04-25', 80000.00, 3, 3, 1, '2020-04-25', NULL),
                    (10, 'Patricia', 'Taylor', 'patricia.taylor@company.com', '2020-05-30', 82000.00, 4, 4, 1, '2020-05-30', NULL),
                    (11, 'Christopher', 'Anderson', 'christopher.anderson@company.com', '2020-06-15', 95000.00, 5, 5, 1, '2020-06-15', NULL),
                    (12, 'Amanda', 'Thomas', 'amanda.thomas@company.com', '2020-07-20', 70000.00, 6, 2, 1, '2020-07-20', NULL),
                    (13, 'Daniel', 'Jackson', 'daniel.jackson@company.com', '2020-08-25', 88000.00, 7, 3, 1, '2020-08-25', NULL),
                    (14, 'Michelle', 'White', 'michelle.white@company.com', '2020-09-30', 92000.00, 8, 1, 1, '2020-09-30', '2024-01-15'),
                    (15, 'Matthew', 'Harris', 'matthew.harris@company.com', '2021-01-10', 78000.00, 1, 1, 1, '2021-01-10', NULL),
                    (16, 'Ashley', 'Martin', 'ashley.martin@company.com', '2021-02-15', 72000.00, 3, 3, 1, '2021-02-15', NULL),
                    (17, 'Joshua', 'Garcia', 'joshua.garcia@company.com', '2021-03-20', 86000.00, 5, 5, 1, '2021-03-20', NULL),
                    (18, 'Stephanie', 'Martinez', 'stephanie.martinez@company.com', '2021-04-25', 68000.00, 6, 2, 1, '2021-04-25', NULL),
                    (19, 'Andrew', 'Robinson', 'andrew.robinson@company.com', '2021-05-30', 94000.00, 1, 1, 1, '2021-05-30', NULL),
                    (20, 'Nicole', 'Clark', 'nicole.clark@company.com', NULL, 65000.00, 2, 2, 1, '2022-01-01', NULL),
                    (21, 'Ryan', 'Rodriguez', 'ryan.rodriguez@company.com', '2022-02-15', 89000.00, 7, 3, 0, '2022-02-15', '2023-12-01'),
                    (22, 'Elizabeth', 'Lewis', 'elizabeth.lewis@company.com', '2022-03-20', 76000.00, 4, 4, 1, '2022-03-20', NULL),
                    (23, 'Brandon', 'Lee', 'brandon.lee@company.com', '2022-04-25', 91000.00, 8, 1, 1, '2022-04-25', NULL),
                    (24, 'Samantha', 'Walker', 'samantha.walker@company.com', '2022-05-30', 73000.00, 9, 4, 1, '2022-05-30', NULL),
                    (25, 'Kevin', 'Hall', 'kevin.hall@company.com', '2023-01-10', 67000.00, 1, 6, 1, '2023-01-10', NULL);
                
                SET IDENTITY_INSERT [dbo].[Employee] OFF;");
            Console.WriteLine("Sample data inserted into Employee.");
        }

        private void PopulateProjects(SqlConnection connection)
        {
            if (GetRecordCount(connection, "Project") > 0) return;

            ExecuteNonQuery(connection, @"
                SET IDENTITY_INSERT [dbo].[Project] ON;
                
                INSERT INTO [dbo].[Project] ([ProjectId], [ProjectName], [Description], [StartDate], [EndDate], [Budget], [LeadEmployeeId], [DepartmentId], [Status])
                VALUES
                    (1, 'Cloud Migration', 'Migrate on-premise infrastructure to cloud', '2023-01-15', '2024-06-30', 250000.00, 1, 1, 'Active'),
                    (2, 'Employee Portal', 'Build new employee self-service portal', '2023-03-01', '2023-12-31', 150000.00, 6, 1, 'Completed'),
                    (3, 'Brand Refresh', 'Update company branding and marketing materials', '2023-06-01', NULL, 80000.00, 3, 3, 'Active'),
                    (4, 'Data Analytics Platform', 'Build enterprise data analytics solution', '2023-09-01', '2025-03-31', 400000.00, 5, 5, 'Active'),
                    (5, 'Customer CRM Upgrade', 'Upgrade CRM system to latest version', '2024-01-15', '2024-09-30', NULL, 14, 8, 'Active'),
                    (6, 'Mobile App Development', 'Develop mobile app for customers', '2023-04-01', '2023-11-30', 200000.00, 7, 1, 'Completed'),
                    (7, 'Security Audit', 'Comprehensive security assessment', '2024-02-01', '2024-04-30', 50000.00, 23, 8, 'Active'),
                    (8, 'Process Automation', 'Automate manual business processes', '2024-03-01', NULL, 175000.00, 11, 5, 'Active'),
                    (9, 'Training Program', 'Develop employee training curriculum', '2023-07-01', '2024-01-31', 60000.00, 2, 2, 'Completed'),
                    (10, 'Sales Dashboard', 'Real-time sales analytics dashboard', '2024-04-01', '2024-10-31', 90000.00, 13, 7, 'Active');
                
                SET IDENTITY_INSERT [dbo].[Project] OFF;");
            Console.WriteLine("Sample data inserted into Project.");
        }

        private async Task PopulateProjectsAsync(SqlConnection connection)
        {
            if (await GetRecordCountAsync(connection, "Project") > 0) return;

            await ExecuteNonQueryAsync(connection, @"
                SET IDENTITY_INSERT [dbo].[Project] ON;
                
                INSERT INTO [dbo].[Project] ([ProjectId], [ProjectName], [Description], [StartDate], [EndDate], [Budget], [LeadEmployeeId], [DepartmentId], [Status])
                VALUES
                    (1, 'Cloud Migration', 'Migrate on-premise infrastructure to cloud', '2023-01-15', '2024-06-30', 250000.00, 1, 1, 'Active'),
                    (2, 'Employee Portal', 'Build new employee self-service portal', '2023-03-01', '2023-12-31', 150000.00, 6, 1, 'Completed'),
                    (3, 'Brand Refresh', 'Update company branding and marketing materials', '2023-06-01', NULL, 80000.00, 3, 3, 'Active'),
                    (4, 'Data Analytics Platform', 'Build enterprise data analytics solution', '2023-09-01', '2025-03-31', 400000.00, 5, 5, 'Active'),
                    (5, 'Customer CRM Upgrade', 'Upgrade CRM system to latest version', '2024-01-15', '2024-09-30', NULL, 14, 8, 'Active'),
                    (6, 'Mobile App Development', 'Develop mobile app for customers', '2023-04-01', '2023-11-30', 200000.00, 7, 1, 'Completed'),
                    (7, 'Security Audit', 'Comprehensive security assessment', '2024-02-01', '2024-04-30', 50000.00, 23, 8, 'Active'),
                    (8, 'Process Automation', 'Automate manual business processes', '2024-03-01', NULL, 175000.00, 11, 5, 'Active'),
                    (9, 'Training Program', 'Develop employee training curriculum', '2023-07-01', '2024-01-31', 60000.00, 2, 2, 'Completed'),
                    (10, 'Sales Dashboard', 'Real-time sales analytics dashboard', '2024-04-01', '2024-10-31', 90000.00, 13, 7, 'Active');
                
                SET IDENTITY_INSERT [dbo].[Project] OFF;");
            Console.WriteLine("Sample data inserted into Project.");
        }

        private void PopulateProjectAssignments(SqlConnection connection)
        {
            if (GetRecordCount(connection, "ProjectAssignment") > 0) return;

            ExecuteNonQuery(connection, @"
                SET IDENTITY_INSERT [dbo].[ProjectAssignment] ON;
                
                INSERT INTO [dbo].[ProjectAssignment] ([AssignmentId], [EmployeeId], [ProjectId], [AssignedDate], [RemovedDate], [Role], [HoursAllocated])
                VALUES
                    (1, 1, 1, '2023-01-15', NULL, 'Project Lead', 20.00),
                    (2, 6, 1, '2023-01-20', NULL, 'Developer', 40.00),
                    (3, 7, 1, '2023-01-20', NULL, 'Developer', 40.00),
                    (4, 14, 1, '2023-02-01', NULL, 'Infrastructure', 30.00),
                    (5, 19, 1, '2023-03-01', NULL, 'Developer', 35.00),
                    (6, 6, 2, '2023-03-01', '2023-12-31', 'Project Lead', 25.00),
                    (7, 15, 2, '2023-03-15', '2023-12-31', 'Developer', 40.00),
                    (8, 25, 2, '2023-04-01', '2023-12-31', 'Developer', 40.00),
                    (9, 3, 3, '2023-06-01', NULL, 'Project Lead', 15.00),
                    (10, 9, 3, '2023-06-01', NULL, 'Designer', 30.00),
                    (11, 16, 3, '2023-06-15', NULL, 'Content Creator', 35.00),
                    (12, 5, 4, '2023-09-01', NULL, 'Project Lead', 20.00),
                    (13, 11, 4, '2023-09-01', NULL, 'Data Engineer', 40.00),
                    (14, 17, 4, '2023-09-15', NULL, 'Data Scientist', 40.00),
                    (15, 14, 5, '2024-01-15', NULL, 'Project Lead', 25.00),
                    (16, 23, 5, '2024-01-20', NULL, 'Developer', 35.00),
                    (17, 7, 6, '2023-04-01', '2023-11-30', 'Project Lead', 30.00),
                    (18, 6, 6, '2023-04-15', '2023-11-30', 'Developer', 40.00),
                    (19, 19, 6, '2023-05-01', '2023-11-30', 'Developer', 40.00),
                    (20, 23, 7, '2024-02-01', NULL, 'Project Lead', 20.00),
                    (21, 14, 7, '2024-02-01', NULL, 'Security Analyst', 30.00),
                    (22, 11, 8, '2024-03-01', NULL, 'Project Lead', 25.00),
                    (23, 17, 8, '2024-03-15', NULL, 'Automation Engineer', 40.00),
                    (24, 2, 9, '2023-07-01', '2024-01-31', 'Project Lead', 15.00),
                    (25, 8, 9, '2023-07-15', '2024-01-31', 'Training Coordinator', 30.00),
                    (26, 12, 9, '2023-08-01', '2024-01-31', 'Content Developer', 35.00),
                    (27, 13, 10, '2024-04-01', NULL, 'Project Lead', 20.00),
                    (28, 9, 10, '2024-04-15', NULL, 'Analyst', NULL);
                
                SET IDENTITY_INSERT [dbo].[ProjectAssignment] OFF;");
            Console.WriteLine("Sample data inserted into ProjectAssignment.");
        }

        private async Task PopulateProjectAssignmentsAsync(SqlConnection connection)
        {
            if (await GetRecordCountAsync(connection, "ProjectAssignment") > 0) return;

            await ExecuteNonQueryAsync(connection, @"
                SET IDENTITY_INSERT [dbo].[ProjectAssignment] ON;
                
                INSERT INTO [dbo].[ProjectAssignment] ([AssignmentId], [EmployeeId], [ProjectId], [AssignedDate], [RemovedDate], [Role], [HoursAllocated])
                VALUES
                    (1, 1, 1, '2023-01-15', NULL, 'Project Lead', 20.00),
                    (2, 6, 1, '2023-01-20', NULL, 'Developer', 40.00),
                    (3, 7, 1, '2023-01-20', NULL, 'Developer', 40.00),
                    (4, 14, 1, '2023-02-01', NULL, 'Infrastructure', 30.00),
                    (5, 19, 1, '2023-03-01', NULL, 'Developer', 35.00),
                    (6, 6, 2, '2023-03-01', '2023-12-31', 'Project Lead', 25.00),
                    (7, 15, 2, '2023-03-15', '2023-12-31', 'Developer', 40.00),
                    (8, 25, 2, '2023-04-01', '2023-12-31', 'Developer', 40.00),
                    (9, 3, 3, '2023-06-01', NULL, 'Project Lead', 15.00),
                    (10, 9, 3, '2023-06-01', NULL, 'Designer', 30.00),
                    (11, 16, 3, '2023-06-15', NULL, 'Content Creator', 35.00),
                    (12, 5, 4, '2023-09-01', NULL, 'Project Lead', 20.00),
                    (13, 11, 4, '2023-09-01', NULL, 'Data Engineer', 40.00),
                    (14, 17, 4, '2023-09-15', NULL, 'Data Scientist', 40.00),
                    (15, 14, 5, '2024-01-15', NULL, 'Project Lead', 25.00),
                    (16, 23, 5, '2024-01-20', NULL, 'Developer', 35.00),
                    (17, 7, 6, '2023-04-01', '2023-11-30', 'Project Lead', 30.00),
                    (18, 6, 6, '2023-04-15', '2023-11-30', 'Developer', 40.00),
                    (19, 19, 6, '2023-05-01', '2023-11-30', 'Developer', 40.00),
                    (20, 23, 7, '2024-02-01', NULL, 'Project Lead', 20.00),
                    (21, 14, 7, '2024-02-01', NULL, 'Security Analyst', 30.00),
                    (22, 11, 8, '2024-03-01', NULL, 'Project Lead', 25.00),
                    (23, 17, 8, '2024-03-15', NULL, 'Automation Engineer', 40.00),
                    (24, 2, 9, '2023-07-01', '2024-01-31', 'Project Lead', 15.00),
                    (25, 8, 9, '2023-07-15', '2024-01-31', 'Training Coordinator', 30.00),
                    (26, 12, 9, '2023-08-01', '2024-01-31', 'Content Developer', 35.00),
                    (27, 13, 10, '2024-04-01', NULL, 'Project Lead', 20.00),
                    (28, 9, 10, '2024-04-15', NULL, 'Analyst', NULL);
                
                SET IDENTITY_INSERT [dbo].[ProjectAssignment] OFF;");
            Console.WriteLine("Sample data inserted into ProjectAssignment.");
        }

        private void PopulateEmployeeSkills(SqlConnection connection)
        {
            if (GetRecordCount(connection, "EmployeeSkill") > 0) return;

            ExecuteNonQuery(connection, @"
                SET IDENTITY_INSERT [dbo].[EmployeeSkill] ON;
                
                INSERT INTO [dbo].[EmployeeSkill] ([SkillId], [EmployeeId], [SkillName], [ProficiencyLevel], [YearsOfExperience], [CertifiedDate])
                VALUES
                    (1, 1, 'C#', 5, 12, '2015-06-01'),
                    (2, 1, 'SQL Server', 5, 10, '2016-03-15'),
                    (3, 1, 'Azure', 4, 5, '2020-01-10'),
                    (4, 6, 'C#', 4, 5, '2019-08-20'),
                    (5, 6, 'JavaScript', 4, 4, NULL),
                    (6, 6, 'React', 3, 2, NULL),
                    (7, 7, 'Python', 4, 6, '2018-05-15'),
                    (8, 7, 'Machine Learning', 3, 3, NULL),
                    (9, 11, 'Python', 5, 8, '2017-09-01'),
                    (10, 11, 'Data Analysis', 5, 7, '2018-02-20'),
                    (11, 11, 'TensorFlow', 4, 4, '2021-06-15'),
                    (12, 14, 'Network Administration', 4, 6, '2019-11-10'),
                    (13, 14, 'Linux', 4, 7, NULL),
                    (14, 14, 'AWS', 3, 2, NULL),
                    (15, 17, 'Data Science', 4, 4, '2022-01-15'),
                    (16, 17, 'R', 3, 3, NULL),
                    (17, 19, 'C#', 3, 3, NULL),
                    (18, 19, '.NET Core', 3, 2, NULL),
                    (19, 23, 'Cybersecurity', 4, 5, '2021-08-01'),
                    (20, 23, 'Penetration Testing', 3, 3, NULL),
                    (21, 25, 'JavaScript', 2, 1, NULL),
                    (22, 25, 'HTML/CSS', 3, 2, NULL),
                    (23, 9, 'Graphic Design', 4, 6, NULL),
                    (24, 9, 'Adobe Creative Suite', 5, 7, '2018-04-01'),
                    (25, 13, 'Sales', 4, 5, NULL),
                    (26, 13, 'CRM', 3, 4, NULL),
                    (27, 2, 'HR Management', 5, 10, '2014-07-15'),
                    (28, 2, 'Recruitment', 5, 9, NULL),
                    (29, 5, 'Research Methodology', 5, 12, '2013-09-01'),
                    (30, 5, 'Technical Writing', 4, 8, NULL);
                
                SET IDENTITY_INSERT [dbo].[EmployeeSkill] OFF;");
            Console.WriteLine("Sample data inserted into EmployeeSkill.");
        }

        private async Task PopulateEmployeeSkillsAsync(SqlConnection connection)
        {
            if (await GetRecordCountAsync(connection, "EmployeeSkill") > 0) return;

            await ExecuteNonQueryAsync(connection, @"
                SET IDENTITY_INSERT [dbo].[EmployeeSkill] ON;
                
                INSERT INTO [dbo].[EmployeeSkill] ([SkillId], [EmployeeId], [SkillName], [ProficiencyLevel], [YearsOfExperience], [CertifiedDate])
                VALUES
                    (1, 1, 'C#', 5, 12, '2015-06-01'),
                    (2, 1, 'SQL Server', 5, 10, '2016-03-15'),
                    (3, 1, 'Azure', 4, 5, '2020-01-10'),
                    (4, 6, 'C#', 4, 5, '2019-08-20'),
                    (5, 6, 'JavaScript', 4, 4, NULL),
                    (6, 6, 'React', 3, 2, NULL),
                    (7, 7, 'Python', 4, 6, '2018-05-15'),
                    (8, 7, 'Machine Learning', 3, 3, NULL),
                    (9, 11, 'Python', 5, 8, '2017-09-01'),
                    (10, 11, 'Data Analysis', 5, 7, '2018-02-20'),
                    (11, 11, 'TensorFlow', 4, 4, '2021-06-15'),
                    (12, 14, 'Network Administration', 4, 6, '2019-11-10'),
                    (13, 14, 'Linux', 4, 7, NULL),
                    (14, 14, 'AWS', 3, 2, NULL),
                    (15, 17, 'Data Science', 4, 4, '2022-01-15'),
                    (16, 17, 'R', 3, 3, NULL),
                    (17, 19, 'C#', 3, 3, NULL),
                    (18, 19, '.NET Core', 3, 2, NULL),
                    (19, 23, 'Cybersecurity', 4, 5, '2021-08-01'),
                    (20, 23, 'Penetration Testing', 3, 3, NULL),
                    (21, 25, 'JavaScript', 2, 1, NULL),
                    (22, 25, 'HTML/CSS', 3, 2, NULL),
                    (23, 9, 'Graphic Design', 4, 6, NULL),
                    (24, 9, 'Adobe Creative Suite', 5, 7, '2018-04-01'),
                    (25, 13, 'Sales', 4, 5, NULL),
                    (26, 13, 'CRM', 3, 4, NULL),
                    (27, 2, 'HR Management', 5, 10, '2014-07-15'),
                    (28, 2, 'Recruitment', 5, 9, NULL),
                    (29, 5, 'Research Methodology', 5, 12, '2013-09-01'),
                    (30, 5, 'Technical Writing', 4, 8, NULL);
                
                SET IDENTITY_INSERT [dbo].[EmployeeSkill] OFF;");
            Console.WriteLine("Sample data inserted into EmployeeSkill.");
        }

        private void PopulateAuditLog(SqlConnection connection)
        {
            if (GetRecordCount(connection, "AuditLog") > 0) return;

            ExecuteNonQuery(connection, @"
                SET IDENTITY_INSERT [dbo].[AuditLog] ON;
                
                INSERT INTO [dbo].[AuditLog] ([AuditId], [TableName], [Operation], [RecordId], [OldValues], [NewValues], [ChangedDate], [ChangedByEmployeeId])
                VALUES
                    (1, 'Employee', 'INSERT', 25, NULL, '{""FirstName"":""Kevin"",""LastName"":""Hall"",""Salary"":67000}', '2023-01-10 09:00:00', 2),
                    (2, 'Employee', 'UPDATE', 7, '{""Salary"":85000}', '{""Salary"":90000}', '2023-06-01 14:30:00', 1),
                    (3, 'Employee', 'UPDATE', 14, '{""DepartmentId"":1}', '{""DepartmentId"":8}', '2024-01-15 10:15:00', 1),
                    (4, 'Project', 'INSERT', 10, NULL, '{""ProjectName"":""Sales Dashboard"",""Budget"":90000}', '2024-04-01 08:45:00', 3),
                    (5, 'Employee', 'UPDATE', 21, '{""IsActive"":true}', '{""IsActive"":false}', '2023-12-01 16:00:00', 2),
                    (6, 'ProjectAssignment', 'INSERT', 28, NULL, '{""EmployeeId"":9,""ProjectId"":10,""Role"":""Analyst""}', '2024-04-15 11:20:00', 13),
                    (7, 'Department', 'UPDATE', 10, '{""IsActive"":true}', '{""IsActive"":false}', '2021-06-15 09:30:00', NULL),
                    (8, 'EmployeeSkill', 'INSERT', 30, NULL, '{""EmployeeId"":5,""SkillName"":""Technical Writing""}', '2022-08-10 13:45:00', 5);
                
                SET IDENTITY_INSERT [dbo].[AuditLog] OFF;");
            Console.WriteLine("Sample data inserted into AuditLog.");
        }

        private async Task PopulateAuditLogAsync(SqlConnection connection)
        {
            if (await GetRecordCountAsync(connection, "AuditLog") > 0) return;

            await ExecuteNonQueryAsync(connection, @"
                SET IDENTITY_INSERT [dbo].[AuditLog] ON;
                
                INSERT INTO [dbo].[AuditLog] ([AuditId], [TableName], [Operation], [RecordId], [OldValues], [NewValues], [ChangedDate], [ChangedByEmployeeId])
                VALUES
                    (1, 'Employee', 'INSERT', 25, NULL, '{""FirstName"":""Kevin"",""LastName"":""Hall"",""Salary"":67000}', '2023-01-10 09:00:00', 2),
                    (2, 'Employee', 'UPDATE', 7, '{""Salary"":85000}', '{""Salary"":90000}', '2023-06-01 14:30:00', 1),
                    (3, 'Employee', 'UPDATE', 14, '{""DepartmentId"":1}', '{""DepartmentId"":8}', '2024-01-15 10:15:00', 1),
                    (4, 'Project', 'INSERT', 10, NULL, '{""ProjectName"":""Sales Dashboard"",""Budget"":90000}', '2024-04-01 08:45:00', 3),
                    (5, 'Employee', 'UPDATE', 21, '{""IsActive"":true}', '{""IsActive"":false}', '2023-12-01 16:00:00', 2),
                    (6, 'ProjectAssignment', 'INSERT', 28, NULL, '{""EmployeeId"":9,""ProjectId"":10,""Role"":""Analyst""}', '2024-04-15 11:20:00', 13),
                    (7, 'Department', 'UPDATE', 10, '{""IsActive"":true}', '{""IsActive"":false}', '2021-06-15 09:30:00', NULL),
                    (8, 'EmployeeSkill', 'INSERT', 30, NULL, '{""EmployeeId"":5,""SkillName"":""Technical Writing""}', '2022-08-10 13:45:00', 5);
                
                SET IDENTITY_INSERT [dbo].[AuditLog] OFF;");
            Console.WriteLine("Sample data inserted into AuditLog.");
        }

        private int GetRecordCount(SqlConnection connection, string tableName)
        {
            using (var cmd = new SqlCommand($"SELECT COUNT(*) FROM [dbo].[{tableName}]", connection))
            {
                return (int)cmd.ExecuteScalar();
            }
        }

        private async Task<int> GetRecordCountAsync(SqlConnection connection, string tableName)
        {
            using (var cmd = new SqlCommand($"SELECT COUNT(*) FROM [dbo].[{tableName}]", connection))
            {
                return (int)await cmd.ExecuteScalarAsync();
            }
        }

        private void PrintRecordCounts(SqlConnection connection)
        {
            Console.WriteLine();
            Console.WriteLine("=== Database Setup Complete ===");
            Console.WriteLine();
            Console.WriteLine($"{"Table",-25} {"Records",10}");
            Console.WriteLine(new string('-', 36));
            
            string[] tables = { "Department", "Employee", "Project", "ProjectAssignment", "EmployeeSkill", "AuditLog" };
            foreach (var table in tables)
            {
                var count = GetRecordCount(connection, table);
                Console.WriteLine($"{table,-25} {count,10}");
            }
        }

        private async Task PrintRecordCountsAsync(SqlConnection connection)
        {
            Console.WriteLine();
            Console.WriteLine("=== Database Setup Complete ===");
            Console.WriteLine();
            Console.WriteLine($"{"Table",-25} {"Records",10}");
            Console.WriteLine(new string('-', 36));
            
            string[] tables = { "Department", "Employee", "Project", "ProjectAssignment", "EmployeeSkill", "AuditLog" };
            foreach (var table in tables)
            {
                var count = await GetRecordCountAsync(connection, table);
                Console.WriteLine($"{table,-25} {count,10}");
            }
        }

        #endregion

        #region Helper Methods

        private string BuildConnectionString(string baseConnectionString, string database)
        {
            var builder = new SqlConnectionStringBuilder(baseConnectionString)
            {
                InitialCatalog = database
            };
            return builder.ConnectionString;
        }

        private void ExecuteNonQuery(SqlConnection connection, string sql)
        {
            using (var cmd = new SqlCommand(sql, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        private async Task ExecuteNonQueryAsync(SqlConnection connection, string sql)
        {
            using (var cmd = new SqlCommand(sql, connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }
        }

        #endregion
    }
}
