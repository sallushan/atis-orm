//using Atis.SqlExpressionEngine.SqlExpressions;
//using System;
//using System.Data.Common;
//using System.Data.SqlClient;

//namespace Atis.Orm.SqlServer
//{
//    public class SqlQueryParameter : IQueryParameter
//    {
//        public SqlQueryParameter(object value, bool isLiteral, SqlExpression sqlParameterExpression, string parameterName)
//        {
//            this.InitialValue = value;
//            this.IsLiteral = isLiteral;
//            this.SqlParameterExpression = sqlParameterExpression;
//            this.ParameterName = parameterName;
//        }
//        public string ParameterName { get; }
//        public object InitialValue { get; }
//        public bool IsLiteral { get; }
//        public SqlExpression SqlParameterExpression { get; }

//        public DbParameter GetDbParameter()
//        {
//            var value = this.InitialValue ?? DBNull.Value;
//            return new SqlParameter(this.ParameterName, value);
//        }
//    }
//}
