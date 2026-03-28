using System.Linq.Expressions;

namespace Atis.Orm
{
    public interface IQueryCompiler
    {
        ICompiledQuery Compile(Expression expression);
    }
}