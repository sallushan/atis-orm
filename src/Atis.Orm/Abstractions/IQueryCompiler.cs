using System.Linq.Expressions;

namespace Atis.Orm.Abstractions
{
    public interface IQueryCompiler
    {
        ICompiledQuery Compile(Expression expression);
    }
}