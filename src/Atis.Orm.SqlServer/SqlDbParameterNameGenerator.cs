using Atis.Orm.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Atis.Orm.SqlServer
{
    public class SqlDbParameterNameGenerator : IDbParameterNameGenerator
    {
        public string GenerateParameterName(int index)
        {
            return $"@p{index}";
        }

        public string GenerateSubParameterName(int index, int subIndex)
        {
            return $"{this.GenerateParameterName(index)}_{subIndex}";
        }
    }
}
