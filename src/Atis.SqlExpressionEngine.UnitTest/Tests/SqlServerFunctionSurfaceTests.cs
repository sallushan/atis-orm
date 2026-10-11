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
            AssertMethod(nameof(SqlServerFunctions.NewID), typeof(Guid));
        }

        [TestMethod]
        public void Surface_contains_the_ported_scalar_functions()
        {
            var s = typeof(string);
            AssertMethod(nameof(SqlServerFunctions.Concat), s, s, s, typeof(string[]));
            AssertMethod(nameof(SqlServerFunctions.Concat_WS), s, s, s, s, typeof(string[]));
            AssertMethod(nameof(SqlServerFunctions.Left), s, s, typeof(int?));
            AssertMethod(nameof(SqlServerFunctions.Right), s, s, typeof(int?));
            AssertMethod(nameof(SqlServerFunctions.Lower), s, s);
            AssertMethod(nameof(SqlServerFunctions.Upper), s, s);
            AssertMethod(nameof(SqlServerFunctions.LTrim), s, s);
            AssertMethod(nameof(SqlServerFunctions.RTrim), s, s);
            AssertMethod(nameof(SqlServerFunctions.Trim), s, s);
            AssertMethod(nameof(SqlServerFunctions.Trim), s, s, s);
            AssertMethod(nameof(SqlServerFunctions.NChar), s, typeof(int?));
            AssertMethod(nameof(SqlServerFunctions.Replace), s, s, s, s);
            AssertMethod(nameof(SqlServerFunctions.Replicate), s, s, typeof(int?));
            AssertMethod(nameof(SqlServerFunctions.Reverse), s, s);
            AssertMethod(nameof(SqlServerFunctions.Space), s, typeof(int?));
            AssertMethod(nameof(SqlServerFunctions.String_Escape), s, s, s);
            AssertMethod(nameof(SqlServerFunctions.Stuff), s, s, typeof(int?), typeof(int?), s);
            AssertMethod(nameof(SqlServerFunctions.SubString), s, s, typeof(int?), typeof(int?));
            AssertMethod(nameof(SqlServerFunctions.Translate), s, s, s, s);
            AssertMethod(nameof(SqlServerFunctions.Unicode), typeof(int?), s);

            // Same-type-as-argument functions have one overload per numeric type.
            foreach (var name in new[] { nameof(SqlServerFunctions.Abs), nameof(SqlServerFunctions.Ceiling), nameof(SqlServerFunctions.Floor), nameof(SqlServerFunctions.Sign) })
                foreach (var t in new[] { typeof(int?), typeof(long?), typeof(decimal?), typeof(double?) })
                    AssertMethod(name, t, t);
            AssertMethod(nameof(SqlServerFunctions.Round), typeof(decimal?), typeof(decimal?), typeof(int?));
            AssertMethod(nameof(SqlServerFunctions.Round), typeof(double?), typeof(double?), typeof(int?), typeof(int?));
            AssertMethod(nameof(SqlServerFunctions.Power), typeof(decimal?), typeof(decimal?), typeof(decimal?));
            AssertMethod(nameof(SqlServerFunctions.Power), typeof(double?), typeof(double?), typeof(double?));
            AssertMethod(nameof(SqlServerFunctions.Degrees), typeof(decimal?), typeof(decimal?));
            AssertMethod(nameof(SqlServerFunctions.Radians), typeof(double?), typeof(double?));
            // Functions that always return float.
            foreach (var name in new[] { nameof(SqlServerFunctions.Acos), nameof(SqlServerFunctions.Asin), nameof(SqlServerFunctions.Atan), nameof(SqlServerFunctions.Cos),
                                         nameof(SqlServerFunctions.Cot), nameof(SqlServerFunctions.Exp), nameof(SqlServerFunctions.Log), nameof(SqlServerFunctions.Log10),
                                         nameof(SqlServerFunctions.Sin), nameof(SqlServerFunctions.Sqrt), nameof(SqlServerFunctions.Square), nameof(SqlServerFunctions.Tan) })
                AssertMethod(name, typeof(double?), typeof(double?));
            AssertMethod(nameof(SqlServerFunctions.Atn2), typeof(double?), typeof(double?), typeof(double?));
            AssertMethod(nameof(SqlServerFunctions.Log), typeof(double?), typeof(double?), typeof(int?));
            AssertMethod(nameof(SqlServerFunctions.Pi), typeof(double?));
            AssertMethod(nameof(SqlServerFunctions.Rand), typeof(double?));

            var d = typeof(DateTime?);
            var i = typeof(int?);
            AssertMethod(nameof(SqlServerFunctions.DateFromParts), d, i, i, i);
            AssertMethod(nameof(SqlServerFunctions.DateTimeFromParts), d, i, i, i, i, i, i, i);
            AssertMethod(nameof(SqlServerFunctions.DateTime2FromParts), d, i, i, i, i, i, i, i, typeof(int));
            AssertMethod(nameof(SqlServerFunctions.TimeFromParts), typeof(TimeSpan?), i, i, i, i, typeof(int));
            AssertMethod(nameof(SqlServerFunctions.Day), i, d);
            AssertMethod(nameof(SqlServerFunctions.Month), i, d);
            AssertMethod(nameof(SqlServerFunctions.Year), i, d);
            AssertMethod(nameof(SqlServerFunctions.EOMonth), d, d);
            AssertMethod(nameof(SqlServerFunctions.EOMonth), d, d, i);
            AssertMethod(nameof(SqlServerFunctions.GetDate), typeof(DateTime));
            AssertMethod(nameof(SqlServerFunctions.GetUTCDate), typeof(DateTime));
            AssertMethod(nameof(SqlServerFunctions.SysDateTime), typeof(DateTime));
            AssertMethod(nameof(SqlServerFunctions.SysUTCDateTime), typeof(DateTime));

            AssertMethod(nameof(SqlServerFunctions.CastToBit), typeof(bool?), typeof(object));
            AssertMethod(nameof(SqlServerFunctions.CastToDate), d, typeof(object));
            AssertMethod(nameof(SqlServerFunctions.Current_User), s);
            AssertMethod(nameof(SqlServerFunctions.User), s);
            AssertMethod(nameof(SqlServerFunctions.User_Name), s);
        }

        [TestMethod]
        public void Generic_logical_functions_exist()
        {
            Assert.IsNotNull(typeof(SqlServerFunctions).GetMethod(nameof(SqlServerFunctions.Iif)));
            Assert.IsNotNull(typeof(SqlServerFunctions).GetMethod(nameof(SqlServerFunctions.Choose)));
            Assert.IsNotNull(typeof(SqlServerFunctions).GetMethod(nameof(SqlServerFunctions.IsNull)));
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
