using Atis.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Atis.Orm
{
    public class DataContextConfiguration : ServiceContextConfiguration
    {
        // nothing extra — just a typed configuration class
        // DB-specific extension methods (UseSqlServer etc.) 
        // will be added in Atis.Orm.SqlServer
    }
}
