using Atis.SqlExpressionEngine;
using System;
using System.Collections.Generic;
using System.Text;

namespace Atis.Orm
{
    /// <summary>
    /// 
    /// </summary>
    public interface IEntityMetadataBuilder
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        EntityMetadata Build(Type type);
    }
}
