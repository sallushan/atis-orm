using System.Linq.Expressions;

namespace Atis.Orm
{
    public interface IPreprocessingRequirementTester
    {
        bool IsPreprocessingRequired(Expression originalExpression, Expression preprocessedExpression);
    }
}