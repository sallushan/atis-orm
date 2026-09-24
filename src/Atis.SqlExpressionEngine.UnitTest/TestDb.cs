using Microsoft.Data.SqlClient;

namespace Atis.SqlExpressionEngine.UnitTest
{
    /// <summary>
    ///     <para>
    ///         The <c>TestDb</c> database the DB-layer tests run against — the ones that drive
    ///         <c>IDbCommunication</c> directly, with no ORM model. It is separate from
    ///         <see cref="TestDatabaseSetup.DatabaseName"/>, which holds the model's tables.
    ///     </para>
    ///     <para>
    ///         Only the database is created here. Each test class creates the tables it uses in its own
    ///         initialization, so this only has to make sure there is a database to create them in.
    ///     </para>
    /// </summary>
    internal static class TestDb
    {
        public const string Name = "TestDb";

        private const string MasterConnectionString =
            "server=localhost;database=master;integrated security=true;TrustServerCertificate=True";

        private static readonly object Gate = new object();
        private static bool ensured;

        /// <summary>Creates the database if it does not exist. Checks the server once per test run.</summary>
        public static void EnsureCreated()
        {
            lock (Gate)
            {
                if (ensured)
                    return;

                using (var connection = new SqlConnection(MasterConnectionString))
                using (var command = new SqlCommand($"IF DB_ID(N'{Name}') IS NULL CREATE DATABASE [{Name}];", connection))
                {
                    connection.Open();
                    command.ExecuteNonQuery();
                }
                ensured = true;
            }
        }
    }
}
