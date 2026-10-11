using System;
using System.Linq;
using System.Reflection;

using Atis.Orm.SqlServer;

namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    [TestClass]
    public class SqlServerFunctionSurfaceTests
    {
        [TestMethod]
        public void Surface_contains_every_expected_overload()
        {
            AssertMethod(nameof(SqlServerFunctions.Ascii), typeof(int?), typeof(string));
            AssertMethod(nameof(SqlServerFunctions.Char), typeof(string), typeof(int?));
            AssertMethod(nameof(SqlServerFunctions.Difference), typeof(int?), typeof(string), typeof(string));
            AssertMethod(nameof(SqlServerFunctions.PatIndex), typeof(int?), typeof(string), typeof(string));
            AssertMethod(nameof(SqlServerFunctions.QuoteName), typeof(string), typeof(string));
            AssertMethod(nameof(SqlServerFunctions.QuoteName), typeof(string), typeof(string), typeof(string));
            AssertMethod(nameof(SqlServerFunctions.Soundex), typeof(string), typeof(string));
            AssertMethod(nameof(SqlServerFunctions.Str), typeof(string), typeof(decimal?));
            AssertMethod(nameof(SqlServerFunctions.Str), typeof(string), typeof(decimal?), typeof(int?));
            AssertMethod(nameof(SqlServerFunctions.Str), typeof(string), typeof(decimal?), typeof(int?), typeof(int?));

            AssertMethod(nameof(SqlServerFunctions.Rand), typeof(double?), typeof(int?));
            AssertMethod(nameof(SqlServerFunctions.Checksum_Agg), typeof(int?), typeof(object));
            AssertMethod(nameof(SqlServerFunctions.Checksum_AggDistinct), typeof(int?), typeof(object));

            AssertMethod(nameof(SqlServerFunctions.CastToVarchar), typeof(string), typeof(object));
            AssertMethod(nameof(SqlServerFunctions.CastToVarchar), typeof(string), typeof(object), typeof(int));
            AssertMethod(nameof(SqlServerFunctions.CastToNvarchar), typeof(string), typeof(object));
            AssertMethod(nameof(SqlServerFunctions.CastToNvarchar), typeof(string), typeof(object), typeof(int));
            AssertMethod(nameof(SqlServerFunctions.CastToVarbinary), typeof(byte[]), typeof(object));
            AssertMethod(nameof(SqlServerFunctions.CastToVarbinary), typeof(byte[]), typeof(object), typeof(int));
            AssertMethod(nameof(SqlServerFunctions.CastToDateTime), typeof(DateTime?), typeof(object));
            AssertMethod(nameof(SqlServerFunctions.ConvertToVarchar), typeof(string), typeof(object), typeof(int?));
            AssertMethod(nameof(SqlServerFunctions.ConvertToVarchar), typeof(string), typeof(object), typeof(int?), typeof(int));
            AssertMethod(nameof(SqlServerFunctions.ConvertToNvarchar), typeof(string), typeof(object), typeof(int?));
            AssertMethod(nameof(SqlServerFunctions.ConvertToNvarchar), typeof(string), typeof(object), typeof(int?), typeof(int));
            AssertMethod(nameof(SqlServerFunctions.ConvertToInt), typeof(int?), typeof(object), typeof(int?));
            AssertMethod(nameof(SqlServerFunctions.ConvertToDecimal), typeof(decimal?), typeof(object), typeof(int?));
            AssertMethod(nameof(SqlServerFunctions.ConvertToDecimal), typeof(decimal?), typeof(object), typeof(int?), typeof(int), typeof(int));
            AssertMethod(nameof(SqlServerFunctions.ConvertToBit), typeof(bool?), typeof(object), typeof(int?));
            AssertMethod(nameof(SqlServerFunctions.ConvertToVarbinary), typeof(byte[]), typeof(object), typeof(int?));
            AssertMethod(nameof(SqlServerFunctions.ConvertToVarbinary), typeof(byte[]), typeof(object), typeof(int?), typeof(int));
            AssertMethod(nameof(SqlServerFunctions.ConvertToDateTime), typeof(DateTime?), typeof(object), typeof(int?));
            AssertMethod(nameof(SqlServerFunctions.ConvertToDate), typeof(DateTime?), typeof(object), typeof(int?));

            AssertMethod(nameof(SqlServerFunctions.DataLength), typeof(long?), typeof(object));
            AssertMethod(nameof(SqlServerFunctions.DateName), typeof(string), typeof(DatePart), typeof(DateTime?));
            AssertMethod(nameof(SqlServerFunctions.IsDate), typeof(int?), typeof(object));

            AssertMethod(nameof(SqlServerFunctions.Database_Principal_ID), typeof(int?), typeof(string));
            AssertMethod(nameof(SqlServerFunctions.Has_DBAccess), typeof(int?), typeof(string));
            AssertMethod(nameof(SqlServerFunctions.Is_Member), typeof(int?), typeof(string));
            AssertMethod(nameof(SqlServerFunctions.Is_RoleMember), typeof(int?), typeof(string));
            AssertMethod(nameof(SqlServerFunctions.Is_RoleMember), typeof(int?), typeof(string), typeof(string));
            AssertMethod(nameof(SqlServerFunctions.Is_SrvRoleMember), typeof(int?), typeof(string));
            AssertMethod(nameof(SqlServerFunctions.Is_SrvRoleMember), typeof(int?), typeof(string), typeof(string));
            AssertMethod(nameof(SqlServerFunctions.Original_Login), typeof(string));
            AssertMethod(nameof(SqlServerFunctions.Permissions), typeof(int?), typeof(int?));
            AssertMethod(nameof(SqlServerFunctions.Permissions), typeof(int?), typeof(int?), typeof(string));
            AssertMethod(nameof(SqlServerFunctions.Session_User), typeof(string));
            AssertMethod(nameof(SqlServerFunctions.SUser_Id), typeof(int?));
            AssertMethod(nameof(SqlServerFunctions.SUser_Id), typeof(int?), typeof(string));
            AssertMethod(nameof(SqlServerFunctions.SUser_Name), typeof(string), typeof(int?));
            AssertMethod(nameof(SqlServerFunctions.SUser_SID), typeof(byte[]));
            AssertMethod(nameof(SqlServerFunctions.SUser_SID), typeof(byte[]), typeof(string));
            AssertMethod(nameof(SqlServerFunctions.SUser_SID), typeof(byte[]), typeof(string), typeof(int?));
            AssertMethod(nameof(SqlServerFunctions.SUser_SName), typeof(string));
            AssertMethod(nameof(SqlServerFunctions.SUser_SName), typeof(string), typeof(byte[]));
            AssertMethod(nameof(SqlServerFunctions.System_User), typeof(string));
            AssertMethod(nameof(SqlServerFunctions.User_Name), typeof(string), typeof(int?));

            AssertMethod(nameof(SqlServerFunctions.Binary_Checksum), typeof(int?));
            AssertMethod(nameof(SqlServerFunctions.Binary_Checksum), typeof(int?), typeof(object), typeof(object[]));
            AssertMethod(nameof(SqlServerFunctions.Checksum), typeof(int?));
            AssertMethod(nameof(SqlServerFunctions.Checksum), typeof(int?), typeof(object), typeof(object[]));
            AssertMethod(nameof(SqlServerFunctions.IsNumeric), typeof(int), typeof(object));
        }

        private static void AssertMethod(string name, Type returnType, params Type[] parameterTypes)
        {
            var method = typeof(SqlServerFunctions).GetMethod(
                name,
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: parameterTypes,
                modifiers: null);

            Assert.IsNotNull(method, $"Missing SQL Server function: {name}({string.Join(", ", parameterTypes.Select(x => x.Name))})");
            Assert.AreEqual(returnType, method.ReturnType, $"Wrong return type for {method}.");
        }
    }
}
