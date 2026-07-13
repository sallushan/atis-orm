using System;
using System.Text;
using Microsoft.Data.SqlClient;

namespace Atis.Orm.Benchmarks.Data
{
    /// <summary>
    /// Creates and seeds a dedicated benchmark database ([AtisOrmBenchDb] by default) so all
    /// ORMs run against identical tables and identical data. Kept separate from the unit-test
    /// database so benchmark seeding volume never affects functional tests.
    ///
    /// Override the server via the ATIS_BENCH_SQL environment variable, e.g.
    ///   set ATIS_BENCH_SQL=Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=True
    /// </summary>
    public static class BenchmarkDatabase
    {
        public const string DatabaseName = "AtisOrmBenchDb";

        private static readonly string ServerConnectionString =
            Environment.GetEnvironmentVariable("ATIS_BENCH_SQL")
            ?? "Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True";

        /// <summary>
        /// Connection string for the Microsoft.Data.SqlClient-based contenders (EF Core, Dapper,
        /// raw ADO.NET) and for DB bootstrap/seeding.
        /// </summary>
        public static string ConnectionString => Build(DatabaseName);

        /// <summary>
        /// Connection string for Atis, whose SqlServer provider uses the legacy System.Data.SqlClient.
        /// Normalized with that provider's own builder so keyword spellings (e.g. TrustServerCertificate)
        /// are emitted in the form its parser accepts — Microsoft.Data.SqlClient's builder emits a
        /// spaced form the 4.9.0 provider rejects.
        /// </summary>
        public static string AtisConnectionString
        {
            get
            {
                var b = new System.Data.SqlClient.SqlConnectionStringBuilder(ServerConnectionString)
                {
                    InitialCatalog = DatabaseName
                };
                return b.ConnectionString;
            }
        }

        private static string Build(string database)
        {
            var b = new SqlConnectionStringBuilder(ServerConnectionString) { InitialCatalog = database };
            return b.ConnectionString;
        }

        /// <summary>
        /// Idempotently ensures the database, tables, and <paramref name="employeeCount"/> employee
        /// rows exist. Re-running with the same count is a no-op after the first call.
        /// </summary>
        public static void EnsureSeeded(int employeeCount = 5000)
        {
            CreateDatabaseIfMissing();
            CreateSchema();
            Seed(employeeCount);
        }

        private static void CreateDatabaseIfMissing()
        {
            using var conn = new SqlConnection(Build("master"));
            conn.Open();
            Exec(conn,
                $"IF DB_ID(N'{DatabaseName}') IS NULL CREATE DATABASE [{DatabaseName}];");
        }

        private static void CreateSchema()
        {
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();
            Exec(conn, @"
                IF OBJECT_ID(N'[dbo].[Department]', N'U') IS NULL
                CREATE TABLE [dbo].[Department] (
                    [DepartmentId] INT IDENTITY(1,1) PRIMARY KEY,
                    [DepartmentName] NVARCHAR(100) NOT NULL,
                    [Location] NVARCHAR(100) NULL,
                    [Budget] DECIMAL(18,2) NULL,
                    [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
                    [IsActive] BIT NOT NULL DEFAULT 1
                );");
            Exec(conn, @"
                IF OBJECT_ID(N'[dbo].[Employee]', N'U') IS NULL
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
                    CONSTRAINT [FK_Bench_Employee_Department] FOREIGN KEY ([DepartmentId])
                        REFERENCES [dbo].[Department]([DepartmentId])
                );
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Bench_Employee_Dept')
                    CREATE INDEX [IX_Bench_Employee_Dept] ON [dbo].[Employee]([DepartmentId], [IsActive]);");
        }

        private static void Seed(int employeeCount)
        {
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();

            if (Count(conn, "Department") == 0)
            {
                Exec(conn, @"
                    INSERT INTO [dbo].[Department] ([DepartmentName],[Location],[Budget],[IsActive])
                    VALUES
                        (N'Engineering', N'Building A', 500000, 1),
                        (N'Human Resources', N'Building B', 150000, 1),
                        (N'Marketing', N'Building A', 300000, 1),
                        (N'Finance', N'Building C', 200000, 1),
                        (N'Research', N'Building D', 750000, 1);");
            }

            var existing = Count(conn, "Employee");
            if (existing >= employeeCount)
                return;

            // Bulk-load the remaining rows via SqlBulkCopy for fast setup.
            var table = new System.Data.DataTable();
            table.Columns.Add("FirstName", typeof(string));
            table.Columns.Add("LastName", typeof(string));
            table.Columns.Add("Email", typeof(string));
            table.Columns.Add("HireDate", typeof(DateTime));
            table.Columns.Add("Salary", typeof(decimal));
            table.Columns.Add("DepartmentId", typeof(int));
            table.Columns.Add("ManagerId", typeof(object));
            table.Columns.Add("IsActive", typeof(bool));
            table.Columns.Add("CreatedDate", typeof(DateTime));
            table.Columns.Add("ModifiedDate", typeof(object));

            var rnd = new Random(12345);
            for (int i = existing + 1; i <= employeeCount; i++)
            {
                table.Rows.Add(
                    "First" + i,
                    "Last" + i,
                    $"user{i}@company.com",
                    new DateTime(2018, 1, 1).AddDays(i % 2000),
                    50000m + (i % 100) * 1000m,
                    (i % 5) + 1,
                    DBNull.Value,
                    i % 7 != 0,           // ~86% active
                    DateTime.Now,
                    DBNull.Value);
            }

            using var bulk = new SqlBulkCopy(conn) { DestinationTableName = "[dbo].[Employee]" };
            foreach (System.Data.DataColumn c in table.Columns)
                bulk.ColumnMappings.Add(c.ColumnName, c.ColumnName);
            bulk.WriteToServer(table);
        }

        private static int Count(SqlConnection conn, string table)
        {
            using var cmd = new SqlCommand($"SELECT COUNT(*) FROM [dbo].[{table}]", conn);
            return (int)cmd.ExecuteScalar();
        }

        private static void Exec(SqlConnection conn, string sql)
        {
            using var cmd = new SqlCommand(sql, conn);
            cmd.ExecuteNonQuery();
        }
    }
}
