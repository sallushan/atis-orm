using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    [TestClass]
    public class QueryExecutionTests : TestBase
    {
        [TestMethod]
        public void Simple_materialization()
        {
            using var connection = new SqlConnection("server=localhost;database=TestDb;integrated security=true;TrustServerCertificate=True");
            CreateStudentTable(connection);
            PopulatePersonTable(connection);
            var persons = LoadPersonData(connection);
        }

        [TestMethod]
        public void Dynamic_materialization()
        {
            using var connection = new SqlConnection("server=localhost;database=TestDb;integrated security=true;TrustServerCertificate=True");
            CreateStudentTable(connection);
            PopulatePersonTable(connection);
            var persons = LoadData<Person>(connection, "select ID as Id, AGE as Age, FRST_NM as FirstName, LAST_NM as LastName, MID_INIT as MiddleInitial from dbo.Person order by ID;",
                reader => new Person
                {
                    Id = reader.GetInt32(0),
                    Age = reader.GetInt32(1),
                    FirstName = reader.GetString(2),
                    LastName = reader.GetString(3),
                    MiddleInitial = reader.IsDBNull(4) ? null : reader.GetString(4)
                });
        }

        //private Expression<Func<SqlConnection, string, List<Person>>> GetPersonLoadingQuery()
        //{
        //}

        private List<T> LoadData<T>(SqlConnection dbConnection, string script, Func<IDataReader, object> materializer)
        {
            var result = new List<T>();
            using var command = dbConnection.CreateCommand();
            command.CommandText = script;
            command.CommandType = System.Data.CommandType.Text;
            var wasClosed = false;
            if (dbConnection.State != System.Data.ConnectionState.Open)
            {
                wasClosed = true;
                this.stopwatch.Restart();
                dbConnection.Open();
                this.stopwatch.Stop();
                Console.WriteLine($"Connection.Open: {this.stopwatch.ElapsedMilliseconds} ms.");
            }
            this.stopwatch.Restart();
            using var reader = command.ExecuteReader();
            this.stopwatch.Stop();
            Console.WriteLine($"ExecuteReader: {this.stopwatch.ElapsedMilliseconds} ms.");
            this.stopwatch.Restart();
            while (reader.Read())
            {
                var item = (T)materializer(reader);
                result.Add(item);
            }
            this.stopwatch.Stop();
            Console.WriteLine($"Materialization: {this.stopwatch.ElapsedMilliseconds} ms., Count: {result.Count}, Average per line: {(result.Count > 0 ? this.stopwatch.ElapsedMilliseconds / Convert.ToDecimal(result.Count) : 0):N2} ms.");
            if (wasClosed)
            {
                this.stopwatch.Restart();
                dbConnection.Close();
                this.stopwatch.Stop();
                Console.WriteLine($"Connection.Close: {this.stopwatch.ElapsedMilliseconds} ms.");
            }
            Console.WriteLine("Person data loaded successfully.");
            return result;
        }

        private List<Person> LoadPersonData(SqlConnection dbConnection)
        {
            var script = @"select ID as Id, AGE as Age, FRST_NM as FirstName, LAST_NM as LastName, MID_INIT as MiddleInitial from dbo.Person order by ID;";
            var result = new List<Person>();
            using var command = dbConnection.CreateCommand();
            command.CommandText = script;
            command.CommandType = System.Data.CommandType.Text;
            var wasClosed = false;
            if (dbConnection.State != System.Data.ConnectionState.Open)
            {
                wasClosed = true;
                this.stopwatch.Restart();
                dbConnection.Open();
                this.stopwatch.Stop();
                Console.WriteLine($"Connection.Open: {this.stopwatch.ElapsedMilliseconds} ms.");
            }
            this.stopwatch.Restart();
            using var reader = command.ExecuteReader();
            this.stopwatch.Stop();
            Console.WriteLine($"ExecuteReader: {this.stopwatch.ElapsedMilliseconds} ms.");
            this.stopwatch.Restart();
            while (reader.Read())
            {
                var person = new Person
                {
                    Id = reader.GetInt32(0),
                    Age = reader.GetInt32(1),
                    FirstName = reader.GetString(2),
                    LastName = reader.GetString(3),
                    MiddleInitial = reader.IsDBNull(4) ? null : reader.GetString(4)
                };
                result.Add(person);
            }
            this.stopwatch.Stop();
            Console.WriteLine($"Materialization: {this.stopwatch.ElapsedMilliseconds} ms., Count: {result.Count}, Average per line: {(result.Count > 0 ? this.stopwatch.ElapsedMilliseconds / Convert.ToDecimal(result.Count) : 0):N2} ms.");
            if (wasClosed)
            {
                this.stopwatch.Restart();
                dbConnection.Close();
                this.stopwatch.Stop();
                Console.WriteLine($"Connection.Close: {this.stopwatch.ElapsedMilliseconds} ms.");
            }
            Console.WriteLine("Person data loaded successfully.");
            return result;
        }

        private static void ExecuteCommandNonQuery(SqlConnection dbConnection, string commandText)
        {
            using var command = dbConnection.CreateCommand();
            command.CommandText = commandText;
            var wasClosed = false;
            if (dbConnection.State != System.Data.ConnectionState.Open)
            {
                wasClosed = true;
                dbConnection.Open();
            }
            command.ExecuteNonQuery();
            if (wasClosed)
            {
                dbConnection.Close();
            }
        }

        private static void CreateStudentTable(SqlConnection dbConnection)
        {
            var script = @"
DROP TABLE IF EXISTS dbo.Person;

CREATE TABLE dbo.Person
(
    ID INT IDENTITY(1,1) PRIMARY KEY,
    AGE INT NOT NULL,
    FRST_NM NVARCHAR(100) NOT NULL,
    LAST_NM NVARCHAR(100) NOT NULL,
    MID_INIT CHAR(1) NULL
);
";
            ExecuteCommandNonQuery(dbConnection, script);
            Console.WriteLine("Person table created successfully.");
        }

        private static void PopulatePersonTable(SqlConnection dbConnection)
        {
            var script = @"
-- Script to insert 100 records into dbo.Person
SET NOCOUNT ON;

INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (71, N'Hlidlmh', N'Mdluuz', N'D');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (53, N'Kofmcicvt', N'Mpnsnmiiwf', N'V');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (46, N'Uhccgqdwi', N'Lxnuv', NULL);
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (53, N'Wdhjpzas', N'Zapjklirr', NULL);
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (27, N'Dlmkmd', N'Fdmjn', N'D');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (18, N'Evnomp', N'Axnuj', NULL);
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (44, N'Akkeagce', N'Ignotmigkea', NULL);
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (55, N'Xidosh', N'Aenglptyqpr', N'I');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (70, N'Lllsqng', N'Ppqcdmx', N'L');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (39, N'Frlvdln', N'Pbdfzdidfdea', N'L');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (64, N'Awzvitpw', N'Mvera', N'D');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (65, N'Uicyyfjzm', N'Klwrloxev', N'V');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (26, N'Jmyhdxnjh', N'Qlqpsdhzanx', N'V');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (19, N'Cpimtr', N'Qsgkind', N'P');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (44, N'Snatcyxa', N'Syxzmjqpet', N'W');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (39, N'Piapil', N'Kxevnhdp', N'G');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (38, N'Hehlzeh', N'Qtvlrpjzrdtb', N'V');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (38, N'Ubvbfo', N'Zsffuudccqg', N'Y');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (53, N'Lyeupqzsar', N'Jgvwrhf', N'G');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (24, N'Cmyjm', N'Kzaxilmprg', N'W');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (40, N'Tehjade', N'Kkwbkfxnxfr', N'C');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (21, N'Dltlcvpqlv', N'Kvpzrqxj', NULL);
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (61, N'Yiqhh', N'Lkgbyrcyb', NULL);
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (64, N'Hwwut', N'Dfxszndyyzb', NULL);
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (69, N'Mgnoduwqpq', N'Oaaosr', N'P');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (73, N'Nyelmjyyph', N'Avaphnezw', N'E');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (48, N'Nidtrng', N'Xjvtirc', NULL);
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (61, N'Ofyiifyha', N'Ysoxbhiruah', N'P');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (26, N'Pbjkjcbef', N'Qgtwquvxv', N'H');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (41, N'Lckuiowhtq', N'Rgorfnvhhvh', N'X');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (49, N'Taxiq', N'Bfdoxpeword', N'D');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (42, N'Pojha', N'Pbvrntgkwg', NULL);
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (57, N'Rmgjp', N'Odeyiguismp', N'Q');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (34, N'Orqkjfeh', N'Mmewzek', N'I');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (37, N'Xqsfbpn', N'Zskthr', N'S');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (56, N'Ddojfpfo', N'Cyirnhkxjuu', NULL);
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (22, N'Webviffude', N'Fyekkfjaoo', N'J');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (34, N'Jvbmxenhhk', N'Epbxalcz', NULL);
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (74, N'Zcctz', N'Zfklbwltx', N'Q');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (28, N'Hlalqm', N'Xejjcip', N'R');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (30, N'Pzpzj', N'Bfzvtwd', N'P');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (37, N'Fezvqa', N'Mnmhpfj', NULL);
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (38, N'Ybbmywwmo', N'Snqtejvlmw', N'A');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (73, N'Bnyynw', N'Abfvurwstup', N'X');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (33, N'Iajlbk', N'Haceajraxiz', NULL);
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (19, N'Wpjhrtev', N'Vbdjfdlcfrzy', N'K');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (33, N'Jnfrechmkc', N'Tvzhjlugwep', N'V');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (38, N'Otbga', N'Nwmuarxwbv', N'X');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (70, N'Vnovih', N'Vcqftkfa', NULL);
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (51, N'Zrhoo', N'Awkxkwmih', NULL);
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (28, N'Jikjr', N'Oprya', N'Y');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (30, N'Eqagcct', N'Dzzxlmblzp', N'N');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (44, N'Ugeuaoh', N'Oeyyyw', NULL);
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (55, N'Mnvxlptpex', N'Yopfddfdstye', N'P');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (46, N'Gddzbptst', N'Xyeiklrqyiu', N'L');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (73, N'Xigqvppq', N'Khfzkab', N'A');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (30, N'Rtworu', N'Lllmhigkfbkc', N'Q');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (61, N'Hpcok', N'Udkonoza', N'D');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (67, N'Rwlzagxy', N'Vxzpmngk', N'I');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (47, N'Lqzrj', N'Kewpe', N'T');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (55, N'Wjkthw', N'Vsryahflqvq', N'V');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (52, N'Jjqbiqmuic', N'Tlutyzcnoyt', N'L');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (44, N'Qkxaezg', N'Jathgzhs', N'R');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (52, N'Uzlykomaxn', N'Fvsgftlez', NULL);
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (63, N'Rgucnbfqjj', N'Erfswejbmhg', N'G');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (32, N'Jbizszaiv', N'Psxaedutj', N'P');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (52, N'Ztjncy', N'Leoeinafyxm', NULL);
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (70, N'Vexhvb', N'Kodfaxwwcfpi', N'K');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (62, N'Llaehirmhe', N'Vipnkdty', NULL);
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (36, N'Xreusazl', N'Zdnpw', N'Q');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (54, N'Tixvjo', N'Wsewowhswfzp', N'D');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (39, N'Vhqhvxm', N'Fnoktylh', N'W');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (41, N'Mwirfuj', N'Wuxxrd', N'Q');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (75, N'Ebwnzlyg', N'Cwkbsy', NULL);
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (19, N'Mnitxwgi', N'Jgsdzpmnk', N'I');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (60, N'Ngthnh', N'Ldhas', N'J');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (26, N'Ncttxmcry', N'Mfsicll', N'M');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (48, N'Xltvxmuz', N'Sueob', NULL);
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (55, N'Lhtwnteujm', N'Ghgghbrxou', N'G');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (22, N'Vugeil', N'Gmabrriuf', N'F');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (52, N'Utoeai', N'Hvaorcmyf', N'R');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (67, N'Kbmcryh', N'Tmqhyap', NULL);
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (48, N'Bcjnvr', N'Tbillgq', N'P');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (50, N'Enjjp', N'Hvylytxwwjbe', N'I');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (71, N'Iymyqhravj', N'Htdpicry', N'V');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (25, N'Bhaesuv', N'Pilkvbfmusk', N'J');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (24, N'Tqjzaj', N'Lpkxi', NULL);
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (61, N'Ykhajd', N'Ucuydwjiua', N'T');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (74, N'Lbuqcoug', N'Pblbvf', NULL);
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (71, N'Acpqawodmp', N'Lmgsuvn', N'I');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (71, N'Swvfjhy', N'Vjodxuetgoe', N'U');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (67, N'Qzogfhfg', N'Jblqsi', NULL);
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (33, N'Yddimej', N'Gjyca', N'Q');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (41, N'Tpxftwfdb', N'Lkvjibuzjb', NULL);
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (29, N'Divzdxsqd', N'Euxybx', NULL);
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (23, N'Xcyba', N'Kbwmqpyvkps', NULL);
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (46, N'Hdqpcuoq', N'Gazluvh', NULL);
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (62, N'Rtovm', N'Itpqlavckz', N'Q');
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (39, N'Krragjeibp', N'Oyrwcjptfnj', NULL);
INSERT INTO dbo.Person (AGE, FRST_NM, LAST_NM, MID_INIT) VALUES (34, N'Emrlsv', N'Mowciprls', N'R');
";
            ExecuteCommandNonQuery(dbConnection, script);
            Console.WriteLine("Person table populated successfully.");
        }
    }
}
