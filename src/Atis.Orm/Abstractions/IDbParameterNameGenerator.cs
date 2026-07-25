using System;
using System.Collections.Generic;
using System.Text;

namespace Atis.Orm.Abstractions
{
    public interface IDbParameterNameGenerator
    {
        string GenerateParameterName(int index);
        string GenerateSubParameterName(int index, int subIndex);
    }
}
