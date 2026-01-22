using Atis.Orm;
using Atis.SqlExpressionEngine.SqlExpressions;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    [TestClass]
    public class AtisOrmTests : TestBase
    {
        [TestMethod]
        public async Task Element_factory_basic_test()
        {
            var setup = new TestDatabaseSetup($"Server=.;Integrated Security=true;Encrypt=True;TrustServerCertificate=True");
            await setup.SetupAsync();

            var query = queryProvider.DataSet<TestEntities.Employee>();
            var result = query.Select(x => new { EmpId = x.EmployeeId, NameParts = new { x.FirstName, x.LastName }, x.HireDate }).Top(5);
            
            var sqlExpression = ConvertExpressionToSqlExpression(result.Expression, out var updatedQueryExpression);

            if (sqlExpression is SqlDerivedTableExpression derivedTable)
            {
                string sql;
                var translator = new SqlExpressionTranslator() { IsRowNumberSupported = false, };

                sql = translator.Translate(derivedTable);

                Console.WriteLine(sql);

                var elementFactoryBuilder = new ElementFactoryBuilder();
                var elementFactory = elementFactoryBuilder.CreateElementFactory(updatedQueryExpression, derivedTable);
                
                using var conn = new SqlConnection($"Server=.;Database={TestDatabaseSetup.DatabaseName};Integrated Security=true;Encrypt=True;TrustServerCertificate=True");
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                conn.Open();
                using var reader = cmd.ExecuteReader(System.Data.CommandBehavior.SequentialAccess);
                while (reader.Read())
                {
                    var element = elementFactory(reader);
                    Console.WriteLine(element);
                }
                reader.Close();
                conn.Close();
            }
            else
            {
                Assert.Fail("Expected SqlDerivedTableExpression");
            }
        }
    }
}
